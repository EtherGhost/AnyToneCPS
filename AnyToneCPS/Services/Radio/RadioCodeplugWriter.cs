using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AnyToneCPS.Services.Radio;

/// <summary>Outcome of writing a whole <see cref="RadioCodeplugRawSnapshot"/>
/// back to the radio.</summary>
public sealed record CodeplugWriteResult
{
    public required bool Success { get; init; }
    public string? Error { get; init; }

    /// <summary>(region address, byte offset within that region) pairs where
    /// the read-back didn't match what was written. Empty means a clean
    /// verify. Only meaningful when <see cref="Success"/> is true.</summary>
    public IReadOnlyList<(int Address, int Offset)> Mismatches { get; init; } = [];
}

/// <summary>
/// Writes an entire <see cref="RadioCodeplugRawSnapshot"/> back to the radio -
/// every captured region, patched or not - then mandatorily reopens and
/// re-reads every region to verify. Replaces the retired per-record
/// <c>RadioChannelWriter</c>/<c>RadioEncryptionKeyWriter</c>, which both
/// wrote a single narrow record and were found (2026-07-18, twice
/// independently) to silently erase neighboring flash sharing the same
/// physical erase block. This always rewrites everything captured rather
/// than just the patched region - mirroring the vendor CPS's own
/// proven-safe behavior
/// (<c>Device::writeOtherData()</c> in `xbenkozx/anytone-cps`).
///
/// Callers are expected to capture a snapshot immediately before calling
/// this (via <see cref="RadioCodeplugRawSnapshotReader.Capture"/>, then
/// <see cref="RadioCodeplugPatcher"/> to apply the one intended change) -
/// never reuse a snapshot captured a while ago, for the same "always read
/// fresh" reason the retired <c>RadioChannelWriter</c> already documented.
/// </summary>
public static class RadioCodeplugWriter
{
    /// <summary>How long to wait before re-reading a region that showed a
    /// byte mismatch, before trusting that mismatch as real. Added
    /// 2026-07-18 after a live test on a large (~399-region/79KB) batch
    /// write reported mismatches that read back correctly moments later on
    /// a completely independent, later check - the mandatory 15s post-write
    /// reopen wait (tuned for much smaller single/few-record writes) isn't
    /// necessarily enough for the radio's flash to finish settling after a
    /// large batch write. Distinguishes "genuinely wrong" from "still
    /// settling" with actual re-read evidence instead of guessing at a
    /// longer blanket delay.</summary>
    private const int MismatchRecheckDelayMs = 3000;

    public static CodeplugWriteResult Write(
        IRadioConnection connection,
        string portName,
        RadioCodeplugRawSnapshot snapshot,
        IProgress<string>? progress = null)
    {
        RadioProtocolLog.Write($"=== RadioCodeplugWriter.Write starting: {snapshot.Regions.Count} regions, port '{portName}' ===");
        foreach (var planned in snapshot.Regions)
        {
            RadioProtocolLog.Write($"  planned region 0x{planned.Address:X8}, {planned.Data.Length}B");
        }

        // Checked before opening any connection - a pure in-memory sanity
        // check that the write plan doesn't have the exact fragmentation
        // bug this whole snapshot mechanism exists to prevent (see
        // AssertNoFragmentedTables's own doc comment). Failing fast here,
        // never having touched the radio, is far better than writing
        // something that silently loses data.
        RadioCodeplugRawSnapshotReader.AssertNoFragmentedTables(snapshot);

        if (!RadioWriteVerification.TryOpenInitial(connection, portName, progress, out var openError))
        {
            RadioProtocolLog.Write($"=== RadioCodeplugWriter.Write ABORTED: initial open failed: {openError} ===");
            return new CodeplugWriteResult { Success = false, Error = $"Could not open port '{portName}' (gave up after {RadioWriteVerification.MaxWaitMs}ms waiting for the radio to respond): {openError}" };
        }

        var writeCompleted = false;
        try
        {
            var identity = connection.Identify();
            if (!identity.IsRecognizedD890UV)
            {
                RadioProtocolLog.Write($"=== RadioCodeplugWriter.Write ABORTED: unrecognized radio model='{identity.Model}' version='{identity.Version}' ===");
                return new CodeplugWriteResult
                {
                    Success = false,
                    Error = $"Unrecognized radio (model='{identity.Model}', version='{identity.Version}'). Expected D890UV V100. Refusing to write."
                };
            }

            var total = snapshot.Regions.Count;
            var written = 0;
            foreach (var region in snapshot.Regions)
            {
                connection.WriteMemory(region.Address, region.Data);
                written++;
                if (written % 20 == 0 || written == total)
                {
                    progress?.Report($"Writing... ({written}/{total} regions)");
                }
            }

            RadioProtocolLog.Write($"=== All {total} regions written, closing to trigger reboot+reconnect ===");
            writeCompleted = true;

            // Mandatory verification - but NOT on this same connection (see
            // RadioWriteVerification.PollIntervalMs's doc comment).
            connection.Close();
            progress?.Report("Write sent - waiting for the radio to reconnect before verifying...");

            if (RadioWriteVerification.ReopenAndIdentifyForVerify(connection, portName, progress, out var reopenError) is null)
            {
                RadioProtocolLog.Write($"=== RadioCodeplugWriter.Write: verify reopen FAILED: {reopenError} ===");
                return new CodeplugWriteResult
                {
                    Success = false,
                    Error = $"Write was sent, but could not reopen the connection to verify it ({reopenError}). Do a plain Read From Radio to check the codeplug's actual state."
                };
            }

            RadioProtocolLog.Write("=== Verify reopen succeeded, reading every region back ===");
            var mismatches = new List<(int, int)>();
            var verified = 0;
            foreach (var region in snapshot.Regions)
            {
                verified++;
                if (verified % 20 == 0 || verified == total)
                {
                    progress?.Report($"Verifying... ({verified}/{total} regions)");
                }

                var readBack = connection.ReadMemoryStrict(region.Address, region.Length);
                var regionMismatches = FindMismatches(region.Data, readBack);

                if (regionMismatches.Count > 0)
                {
                    RadioProtocolLog.Write($"  region 0x{region.Address:X8} MISMATCH on first verify read at offset(s): {string.Join(",", regionMismatches)}");
                    foreach (var offset in regionMismatches)
                    {
                        RadioProtocolLog.Write($"    offset {offset}: intended 0x{region.Data[offset]:X2}, read back 0x{readBack[offset]:X2}");
                    }

                    // Don't trust a mismatch on the first read - re-check
                    // this specific region once more after a short settle
                    // before treating it as real (see MismatchRecheckDelayMs's
                    // doc comment).
                    progress?.Report($"Region 0x{region.Address:X7} showed {regionMismatches.Count} differing byte(s) - re-checking...");
                    Thread.Sleep(MismatchRecheckDelayMs);
                    var recheck = connection.ReadMemoryStrict(region.Address, region.Length);
                    regionMismatches = FindMismatches(region.Data, recheck);
                    RadioProtocolLog.Write($"  region 0x{region.Address:X8} recheck: {(regionMismatches.Count == 0 ? "settled, no real mismatch" : $"STILL MISMATCHED at offset(s): {string.Join(",", regionMismatches)}")}");
                }

                mismatches.AddRange(regionMismatches.Select(offset => (region.Address, offset)));
            }

            RadioProtocolLog.Write($"=== RadioCodeplugWriter.Write COMPLETE: success, {mismatches.Count} real mismatch byte(s) ===");
            return new CodeplugWriteResult { Success = true, Mismatches = mismatches };
        }
        catch (RadioWriteFailedException ex)
        {
            RadioProtocolLog.Write($"=== RadioCodeplugWriter.Write FAILED (RadioWriteFailedException at 0x{ex.Address:X8}): {ex.Message} ===");
            return new CodeplugWriteResult { Success = false, Error = ex.Message };
        }
        catch (RadioReadVerificationFailedException ex)
        {
            RadioProtocolLog.Write($"=== RadioCodeplugWriter.Write FAILED (RadioReadVerificationFailedException, writeCompleted={writeCompleted}): {ex.Message} ===");
            var error = writeCompleted
                ? $"Write was sent, but the verification read was unreliable and could not confirm it ({ex.Message}). Do a plain Read From Radio to check the codeplug's actual state before writing again."
                : $"Could not reliably read the codeplug's current state before writing - aborted without writing anything ({ex.Message}). Try again.";
            return new CodeplugWriteResult { Success = false, Error = error };
        }
        finally
        {
            connection.Close();
        }
    }

    private static List<int> FindMismatches(byte[] intended, byte[] readBack)
    {
        var mismatches = new List<int>();
        for (var i = 0; i < intended.Length; i++)
        {
            if (intended[i] != readBack[i])
            {
                mismatches.Add(i);
            }
        }

        return mismatches;
    }
}

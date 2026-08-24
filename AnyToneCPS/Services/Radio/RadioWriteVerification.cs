using System;
using System.Diagnostics;
using System.Threading;

namespace AnyToneCPS.Services.Radio;

/// <summary>
/// Shared open/reopen-with-retry logic used by <see cref="RadioCodeplugWriter"/>
/// (and, historically, by the now-retired per-record <c>RadioChannelWriter</c>/
/// <c>RadioEncryptionKeyWriter</c>, whose original inline retry loops this was
/// extracted from 2026-07-18) - this is the same subtle timing/retry logic,
/// not just similar, so it lives in one place rather than drifting apart
/// across writers.
/// </summary>
public static class RadioWriteVerification
{
    /// <summary>The radio reboots/re-enumerates its USB after ANY session
    /// close - a plain read as much as a write (confirmed 2026-07-18 by
    /// direct user observation; a read on the SAME still-open connection
    /// used for a write also returns stale pre-write bytes indefinitely).
    /// Rather than blindly sleeping a fixed duration before ever trying to
    /// reopen (which either wastes time when the radio comes back sooner,
    /// or risks being too short when it doesn't), poll at this interval -
    /// each failed <see cref="IRadioConnection.TryOpen"/> attempt already
    /// costs about a second on its own (the PROGRAM handshake's own read
    /// timeout), so polling this often adds negligible extra overhead while
    /// reconnecting as soon as the radio is actually ready (2026-07-19 -
    /// sense readiness instead of guessing a wait).</summary>
    public const int PollIntervalMs = 2000;

    /// <summary>Ceiling on how long to keep polling before giving up -
    /// generous enough to cover the slowest reboot observed (comfortably
    /// above the old fixed 15s single-wait baseline that was "confirmed
    /// sufficient"), while the common case resolves far faster than this
    /// via polling.</summary>
    public const int MaxWaitMs = 45000;

    /// <summary>Opens <paramref name="connection"/> for the first time in a
    /// write (or read) orchestration, polling until the radio responds or
    /// <see cref="MaxWaitMs"/> is exceeded.</summary>
    public static bool TryOpenInitial(IRadioConnection connection, string portName, IProgress<string>? progress, out string? error)
    {
        return PollUntilOpen(connection, portName, progress, "Port busy, waiting for the radio...", out error);
    }

    /// <summary>Waits for the radio's USB re-enumeration, then reopens a
    /// fresh session and re-identifies it - mandatory before any post-write
    /// verification read (see <see cref="TryOpenInitial"/>'s doc comment for
    /// why the same connection can't be reused). Returns the confirmed
    /// identity on success, or null (with <paramref name="error"/> set) if
    /// reopening timed out, or the radio wasn't recognized once reopened.</summary>
    public static RadioIdentity? ReopenAndIdentifyForVerify(IRadioConnection connection, string portName, IProgress<string>? progress, out string? error)
    {
        progress?.Report("Radio is rebooting - waiting to reconnect before verifying...");
        if (!PollUntilOpen(connection, portName, progress, "Waiting for the radio to connect...", out error))
        {
            return null;
        }

        var identity = connection.Identify();
        if (!identity.IsRecognizedD890UV)
        {
            error = $"radio wasn't recognized on reopening to verify (model='{identity.Model}', version='{identity.Version}')";
            return null;
        }

        return identity;
    }

    private static bool PollUntilOpen(IRadioConnection connection, string portName, IProgress<string>? progress, string waitingMessage, out string? error)
    {
        var stopwatch = Stopwatch.StartNew();
        var reportedWaiting = false;
        var attempt = 0;
        while (true)
        {
            attempt++;
            if (connection.TryOpen(portName, out error))
            {
                RadioProtocolLog.Write($"PollUntilOpen: succeeded on attempt {attempt} after {stopwatch.ElapsedMilliseconds}ms");
                return true;
            }

            RadioProtocolLog.Write($"PollUntilOpen: attempt {attempt} failed at {stopwatch.ElapsedMilliseconds}ms: {error}");

            if (stopwatch.ElapsedMilliseconds >= MaxWaitMs)
            {
                RadioProtocolLog.Write($"PollUntilOpen: giving up after {stopwatch.ElapsedMilliseconds}ms ({attempt} attempts)");
                return false;
            }

            if (!reportedWaiting)
            {
                progress?.Report(waitingMessage);
                reportedWaiting = true;
            }

            Thread.Sleep(PollIntervalMs);
        }
    }
}

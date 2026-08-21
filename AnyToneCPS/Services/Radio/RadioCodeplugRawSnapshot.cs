using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using AnyToneCPS.Models;
using AnyToneCPS.Services.Radio.Codecs;

namespace AnyToneCPS.Services.Radio;

/// <summary>
/// One physical, contiguous region of radio memory captured verbatim (no
/// decoding). <see cref="Address"/> is always distinct within a single
/// <see cref="RadioCodeplugRawSnapshot"/> - see that type's doc comment for
/// why (aliased regions like 0x3500000 are captured once, at the largest
/// length needed).
/// </summary>
public sealed record CodeplugRawRegion(int Address, byte[] Data)
{
    public int Length => Data.Length;
}

/// <summary>
/// A raw, undecoded snapshot of every region of radio memory this app reads
/// for a full codeplug view (everything <see cref="RadioCodeplugReader"/>
/// reads except Digital Contacts, which stays opt-in/excluded).
///
/// Exists because narrow single-record writes were found (2026-07-18) to
/// silently erase neighboring flash sharing the same physical erase block -
/// twice, independently: channel writes previously erased neighboring
/// channels, and an encryption-key write erased the whole AES/ARC4/Basic
/// region. The reference project's own source confirms the vendor CPS never
/// does narrow writes either - it always regenerates and rewrites every
/// populated record of every entity type together (see
/// <c>Device::writeOtherData()</c> in `xbenkozx/anytone-cps`). This type is
/// the read-side half of matching that: capture literally everything raw,
/// so a write can patch one region and re-send every other region completely
/// unchanged, exactly as read - never fabricating or omitting a single byte
/// this app doesn't already read today.
///
/// Known residual limitation: this only captures what
/// <see cref="RadioCodeplugReader"/> already reads (plus one explicitly-added
/// extra region - see <see cref="RadioCodeplugRawSnapshotReader"/>'s doc
/// comment on the encryption-key preamble table gap). It is not a proven
/// exhaustive map of every byte in whatever the true underlying flash erase
/// block(s) actually span - only as good as this app's current
/// understanding of the memory map.
/// </summary>
public sealed class RadioCodeplugRawSnapshot
{
    public required IReadOnlyList<CodeplugRawRegion> Regions { get; init; }

    /// <summary>Finds the captured region containing <paramref name="address"/>,
    /// or null if no captured region covers it.</summary>
    public CodeplugRawRegion? FindRegionContaining(int address)
    {
        return Regions.FirstOrDefault(r => address >= r.Address && address < r.Address + r.Length);
    }
}

/// <summary>
/// Captures a <see cref="RadioCodeplugRawSnapshot"/> by walking the exact
/// same sequence of reads as <see cref="RadioCodeplugReader.Read"/> - same
/// addresses, same per-entity bitmap/stride math - but recording raw bytes
/// keyed by address instead of decoding. Deliberately does NOT call
/// `RadioCodeplugReader` or share code with it beyond the address constants
/// in <see cref="D890UvMemoryMap"/>, to avoid disturbing the already-shipped,
/// well-tested read path while this new snapshot mechanism is still being
/// proven on real hardware.
/// </summary>
public static class RadioCodeplugRawSnapshotReader
{
    private const int ChannelBitmapBytes = 0x200;
    private const int ZoneBitmapBytes = 0x20;
    private const int ZoneSlotCount = ZoneBitmapBytes * 8;
    private const int ZoneChannelsRecordBytes = 0x200;

    /// <summary><paramref name="additionalChannelIndices"/>: channel indices to
    /// capture even if the presence bitmap says they're not populated yet -
    /// needed to write a genuinely brand-new channel (see
    /// <see cref="RadioCodeplugPatcher.ApplyChannelPatch"/>'s doc comment):
    /// without this, there's no captured region to safely read-modify-write
    /// against, since this app never fabricates bytes it hasn't actually read
    /// from the radio. Already-populated indices are silently ignored (no
    /// harm in requesting one that's already captured). <paramref name="progress"/>
    /// only ever reports the initial open's retry status today (see
    /// <see cref="RadioWriteVerification.TryOpenInitial"/>) - there's no
    /// per-region progress reporting yet.</summary>
    public static RadioCodeplugRawSnapshot Capture(IRadioConnection connection, string portName, IEnumerable<int>? additionalChannelIndices = null, IProgress<string>? progress = null)
    {
        // Same retry discipline the write path already uses - the radio
        // physically reboots after closing ANY session (read or write, per
        // direct user observation), not just after a write, so a bare
        // single-attempt open here is exactly as fragile as it would be for
        // a post-write reopen.
        if (!RadioWriteVerification.TryOpenInitial(connection, portName, progress, out var openError))
        {
            throw new InvalidOperationException($"Could not open port '{portName}' (gave up after {RadioWriteVerification.MaxWaitMs}ms waiting for the radio to respond): {openError}");
        }

        try
        {
            var identity = connection.Identify();
            if (!identity.IsRecognizedD890UV)
            {
                throw new InvalidOperationException(
                    $"Unrecognized radio (model='{identity.Model}', version='{identity.Version}'). Expected D890UV V100. Refusing to read memory.");
            }

            return CaptureFromOpenConnection(connection, additionalChannelIndices);
        }
        finally
        {
            connection.Close();
        }
    }

    /// <summary>Core capture logic, assuming <paramref name="connection"/> is
    /// already open and identified as a real D890UV. Extracted 2026-08-01 -
    /// see <see cref="RadioCodeplugReader.ReadFromOpenConnection"/>'s doc
    /// comment for why (lets a caller run a
    /// <see cref="RadioCodeplugReader.ReadFromOpenConnection"/> and this
    /// Capture back-to-back on one open session, instead of the radio
    /// rebooting/re-enumerating between two separate full sessions). Does
    /// not open, identify, or close the connection - the caller owns all of
    /// that (see <see cref="Capture"/>, above, for the standalone version
    /// that does).</summary>
    public static RadioCodeplugRawSnapshot CaptureFromOpenConnection(IRadioConnection connection, IEnumerable<int>? additionalChannelIndices = null)
    {
        var regions = new Dictionary<int, byte[]>();

        byte[] CaptureRegion(int address, int length)
        {
            // Not every entity's stride is itself a multiple of 16 (e.g.
            // TalkgroupDataOffset = 0xc8 = 200, so every other talkgroup
            // record starts 8 bytes off a 16-byte boundary) - but
            // IRadioConnection.WriteMemory requires both address and length
            // to be exact multiples of 16. Snap outward to the smallest
            // aligned span that fully contains the requested range, so
            // whatever gets captured here is always directly re-writable
            // without needing special-case handling per entity.
            var alignedAddress = address & ~0xF;
            var alignedEnd = (address + length + 0xF) & ~0xF;
            var alignedLength = alignedEnd - alignedAddress;

            // Dedupe aliased/overlapping regions (e.g. 0x3500000 is the base
            // address for 3 logically-distinct reads at 3 different lengths)
            // by keeping whichever capture is longest - a longer read at the
            // same aligned start address always covers everything a shorter
            // one would.
            byte[] alignedData;
            if (regions.TryGetValue(alignedAddress, out var existing) && existing.Length >= alignedLength)
            {
                alignedData = existing;
            }
            else
            {
                // Strict, not lenient: unlike a normal display-only read,
                // every byte captured here gets written straight back to the
                // radio - a tolerated checksum mismatch here would silently
                // write corrupted data into a region nobody even meant to
                // touch. Matches the same strict-read discipline
                // RadioChannelWriter already uses for its RMW base read.
                alignedData = connection.ReadMemoryStrict(alignedAddress, alignedLength);
                regions[alignedAddress] = alignedData;
            }

            var offsetWithinAligned = address - alignedAddress;
            return alignedData.AsSpan(offsetWithinAligned, length).ToArray();
        }

        {
            // --- Channels ---
            var channelBitmap = CaptureRegion(D890UvMemoryMap.ChannelSet, ChannelBitmapBytes);
            var populatedChannelIndices = new HashSet<int>(EnumerateSetBits(channelBitmap));
            foreach (var idx in populatedChannelIndices)
            {
                CaptureRegion(RadioCodeplugPatcher.ChannelAddress(idx), 0x80);
            }

            if (additionalChannelIndices is not null)
            {
                foreach (var idx in additionalChannelIndices)
                {
                    if (!populatedChannelIndices.Contains(idx))
                    {
                        CaptureRegion(RadioCodeplugPatcher.ChannelAddress(idx), 0x80);
                    }
                }
            }

            // --- Zones ---
            var zoneBitmap = CaptureRegion(D890UvMemoryMap.ZoneSet, ZoneBitmapBytes);
            var zoneIndices = EnumerateSetBits(zoneBitmap);
            CaptureRegion(D890UvMemoryMap.ZoneAChannel, ZoneSlotCount * 2);
            CaptureRegion(D890UvMemoryMap.ZoneBChannel, ZoneSlotCount * 2);
            CaptureRegion(D890UvMemoryMap.ZoneHide, ZoneBitmapBytes);
            foreach (var idx in zoneIndices)
            {
                CaptureRegion(D890UvMemoryMap.ZonesName + idx * D890UvMemoryMap.ZoneDataOffset, D890UvMemoryMap.ZoneDataLength);
                CaptureRegion(D890UvMemoryMap.ZoneChannels + idx * ZoneChannelsRecordBytes, ZoneChannelsRecordBytes);
            }

            // --- Simple bitmap-driven entities (RadioIds, Talkgroups, ScanLists, RoamingChannels/Zones, ReceiveGroupLists) ---
            CaptureSimpleEntities(CaptureRegion, D890UvMemoryMap.RadioIdSet, D890UvMemoryMap.RadioIdData, D890UvMemoryMap.RadioIdDataOffset, D890UvMemoryMap.RadioIdDataLength, invertedBitmap: false, bitmapBytes: 0x20);
            CaptureSimpleEntities(CaptureRegion, D890UvMemoryMap.TalkgroupSet, D890UvMemoryMap.TalkgroupData, D890UvMemoryMap.TalkgroupDataOffset, D890UvMemoryMap.TalkgroupDataLength, invertedBitmap: true, bitmapBytes: 0x4F0);
            CaptureSimpleEntities(CaptureRegion, D890UvMemoryMap.ScanListSet, D890UvMemoryMap.ScanListData, D890UvMemoryMap.ScanListDataOffset, D890UvMemoryMap.ScanListDataLength, invertedBitmap: false, bitmapBytes: 0x20);
            CaptureSimpleEntities(CaptureRegion, D890UvMemoryMap.RoamingChannelSet, D890UvMemoryMap.RoamingChannelData, D890UvMemoryMap.RoamingChannelDataOffset, D890UvMemoryMap.RoamingChannelDataLength, invertedBitmap: false, bitmapBytes: 0x20);
            CaptureSimpleEntities(CaptureRegion, D890UvMemoryMap.RoamingZoneSet, D890UvMemoryMap.RoamingZoneData, D890UvMemoryMap.RoamingZoneDataOffset, D890UvMemoryMap.RoamingZoneDataLength, invertedBitmap: false, bitmapBytes: 0x20);
            CaptureSimpleEntities(CaptureRegion, D890UvMemoryMap.ReceiveGroupSet, D890UvMemoryMap.ReceiveGroupData, D890UvMemoryMap.ReceiveGroupDataOffset, D890UvMemoryMap.ReceiveGroupDataLength, invertedBitmap: false, bitmapBytes: 0x20);

            // --- Auto Repeater Offsets (flat, no bitmap) ---
            CaptureRegion(D890UvMemoryMap.AutoRepeaterData, 0x3F0);

            // --- QDC 1200 Setting (flat, no bitmap/presence list found in
            // either live capture - see Qdc1200IdCodec's own doc comment) ---
            CaptureRegion(D890UvMemoryMap.Qdc1200IdData, Qdc1200IdCodec.SlotCount * Qdc1200IdCodec.RecordLength);
            CaptureRegion(D890UvMemoryMap.Qdc1200SettingsData, Qdc1200SettingsCodec.RecordLength);

            // --- Hot Key (Analog Quick Call/State Information/Hot Key
            // itself) - all three are flat arrays, no bitmap ---
            CaptureRegion(D890UvMemoryMap.AnalogQuickCallData, AnalogQuickCallCodec.SlotCount * AnalogQuickCallCodec.RecordLength);
            CaptureRegion(D890UvMemoryMap.StateInformationData, StateInformationCodec.SlotCount * StateInformationCodec.RecordLength);
            CaptureRegion(D890UvMemoryMap.HotKeyData, HotKeyCodec.KeyCount * HotKeyCodec.RecordLength);

            // --- QDC Address Book (flat, no bitmap - see QdcAddressCodec's
            // own doc comment) ---
            CaptureRegion(D890UvMemoryMap.QdcAddressData, QdcAddressCodec.SlotCount * QdcAddressCodec.RecordLength);

            // --- 5Tone Settings (ID table flat, no bitmap of its own -
            // always captured in full rather than bitmap-driven, same
            // reasoning as Zone's own A-Channel/B-Channel/Hide arrays:
            // the whole table is only 0x1900 bytes, cheap enough to always
            // read whole and sidesteps threading "additional indices" for
            // brand-new rows through Capture the way ApplyChannelPatch's
            // own new-channel support needs to) ---
            CaptureRegion(D890UvMemoryMap.FiveToneIdData, CodeplugLimits.FiveToneIdMax * FiveToneIdCodec.RecordLength);
            // Row 100's own leftover storage (see D890UvMemoryMap's own
            // doc comment) - never patched by this app, captured only so
            // an RMW write doesn't corrupt whatever's already there.
            CaptureRegion(D890UvMemoryMap.FiveToneIdRow100ReservedData, D890UvMemoryMap.FiveToneIdRow100ReservedLength);
            // Also covers PTT ID Starting (BOT)'s real fields, at internal
            // offset 0x30 - see D890UvMemoryMap.FiveToneBotSettingsData.
            CaptureRegion(D890UvMemoryMap.FiveToneDecodeEncodeData, D890UvMemoryMap.FiveToneDecodeEncodeRecordLength);
            CaptureRegion(D890UvMemoryMap.FiveToneEotData, D890UvMemoryMap.FiveToneEotRecordLength);
            CaptureRegion(D890UvMemoryMap.FiveToneInfoIdData, D890UvMemoryMap.FiveToneInfoIdSlotCount * D890UvMemoryMap.FiveToneInfoIdSlotStride);

            // --- 2Tone Settings (unlike 5Tone above, this HAS a real
            // presence bitmap per table - see D890UvMemoryMap.TwoToneEncodeBitmap's
            // own doc comment - both tables still always captured in full
            // regardless, same cheap-enough-to-not-bother reasoning) ---
            CaptureRegion(D890UvMemoryMap.TwoToneEncodeData, CodeplugLimits.TwoToneEncodeMax * D890UvMemoryMap.TwoToneEncodeRecordLength);
            CaptureRegion(D890UvMemoryMap.TwoToneDecodeData, CodeplugLimits.TwoToneDecodeMax * D890UvMemoryMap.TwoToneDecodeRecordLength);
            CaptureRegion(D890UvMemoryMap.TwoToneEncodeBitmap, 0x10);
            CaptureRegion(D890UvMemoryMap.TwoToneDecodeBitmap, 0x10);
            CaptureRegion(D890UvMemoryMap.TwoToneEncodeSettingsData, TwoToneEncodeSettingsCodec.RecordLength);

            // --- DTMF Settings (settings/BOT/EOT/Remotely Kill/Remotely
            // Stun are one contiguous 0x50-byte cluster, but captured as 5
            // separate calls for clarity, same style as 5Tone's own
            // BOT/singleton/EOT cluster above. M1-M16 and Transmitting Time
            // are 2 completely separate regions - see
            // D890UvMemoryMap.DtmfSettingsData's own doc comment.) ---
            CaptureRegion(D890UvMemoryMap.DtmfSettingsData, D890UvMemoryMap.DtmfSettingsRecordLength);
            CaptureRegion(D890UvMemoryMap.DtmfBotData, D890UvMemoryMap.DtmfSettingsRecordLength);
            CaptureRegion(D890UvMemoryMap.DtmfEotData, D890UvMemoryMap.DtmfSettingsRecordLength);
            CaptureRegion(D890UvMemoryMap.DtmfRemotelyKillData, D890UvMemoryMap.DtmfSettingsRecordLength);
            CaptureRegion(D890UvMemoryMap.DtmfRemotelyStunData, D890UvMemoryMap.DtmfSettingsRecordLength);
            CaptureRegion(D890UvMemoryMap.DtmfEncodeData, DtmfEncodeCodec.SlotCount * D890UvMemoryMap.DtmfEncodeRecordLength);
            CaptureRegion(D890UvMemoryMap.DtmfTransmittingTimeIndexData, 1);

            // --- Analog Address Book ---
            var analogIdList = CaptureRegion(D890UvMemoryMap.AnalogBookId, D890UvMemoryMap.AnalogBookIdLength);
            foreach (var b in analogIdList)
            {
                if (b != 0xff)
                {
                    CaptureRegion(D890UvMemoryMap.AnalogBookData + b * D890UvMemoryMap.AnalogBookDataStride, D890UvMemoryMap.AnalogBookDataLength);
                }
            }

            // --- GPS Roaming (flat, no bitmap) ---
            CaptureRegion(D890UvMemoryMap.GpsRoamingData, D890UvMemoryMap.GpsRoamingDataLength);

            // --- Whitelists ---
            CaptureWhitelist(CaptureRegion, D890UvMemoryMap.TalkgroupWhitelistData);
            CaptureWhitelist(CaptureRegion, D890UvMemoryMap.DigitalContactWhitelistData);

            // --- Prefabricated SMS (linked-list walk) ---
            CapturePrefabricatedSms(CaptureRegion);

            // --- AM Air ---
            var amAirBitmap = CaptureRegion(D890UvMemoryMap.AmAirSet, 0x20);
            foreach (var idx in EnumerateSetBits(amAirBitmap))
            {
                CaptureRegion(D890UvMemoryMap.AmAirData + idx * D890UvMemoryMap.AmAirDataStride, D890UvMemoryMap.AmAirDataLength);
            }
            CaptureRegion(D890UvMemoryMap.AmAirVfo, D890UvMemoryMap.AmAirDataLength);

            // --- AM Zones ---
            var amZoneBitmap = CaptureRegion(D890UvMemoryMap.AmZoneSet, 0x10);
            CaptureRegion(D890UvMemoryMap.AmZoneAChannel, D890UvMemoryMap.AmZoneCount * 2);
            foreach (var idx in EnumerateSetBits(amZoneBitmap))
            {
                if (idx >= D890UvMemoryMap.AmZoneCount)
                {
                    continue;
                }

                CaptureRegion(D890UvMemoryMap.AmZoneData + idx * D890UvMemoryMap.AmZoneDataStride, D890UvMemoryMap.AmZoneDataLength);
                CaptureRegion(D890UvMemoryMap.AmZoneScan + idx * D890UvMemoryMap.AmZoneScanStride, D890UvMemoryMap.AmZoneScanLength);
            }

            // --- FM Channels ---
            var fmMeta = CaptureRegion(D890UvMemoryMap.FmMeta, D890UvMemoryMap.FmMetaLength);
            for (var idx = 0; idx < D890UvMemoryMap.FmChannelCount; idx++)
            {
                var byteIndex = idx / 8;
                var bit = idx % 8;
                var active = (fmMeta[D890UvMemoryMap.FmActiveMaskOffset + byteIndex] & (1 << bit)) != 0;
                if (active)
                {
                    CaptureRegion(D890UvMemoryMap.FmChannelData + idx * D890UvMemoryMap.FmChannelDataStride, 0x40);
                }
            }

            // --- Master ID ---
            CaptureRegion(D890UvMemoryMap.MasterIdData, 0x40);

            // --- Talk Alias Settings ---
            CaptureRegion(D890UvMemoryMap.TalkAliasSettingsBase, D890UvMemoryMap.TalkAliasSettingsReadLength);

            // --- Alarm Settings ---
            CaptureRegion(D890UvMemoryMap.AlarmSettingsData3483000, 0x30);
            CaptureRegion(D890UvMemoryMap.AlarmSettingsData3482e00, 0x10);
            CaptureRegion(D890UvMemoryMap.AlarmSettingsData3500000, 0x50);

            // --- APRS Settings ---
            CaptureRegion(D890UvMemoryMap.AprsSettingsMainData, 0x260);
            CaptureRegion(D890UvMemoryMap.AprsReceiveFilterData, D890UvMemoryMap.AprsReceiveFilterDataLength);

            // --- Optional Settings (0x3500000 alias resolved to the largest read - 0x160) ---
            CaptureRegion(D890UvMemoryMap.OptionalSettingsData3500000, OptionalSettingsCodec.MainDataLength);
            CaptureRegion(D890UvMemoryMap.OptionalSettingsData3500900, OptionalSettingsCodec.SecondaryDataLength);
            CaptureRegion(D890UvMemoryMap.OptionalSettingsData3501280, OptionalSettingsCodec.TertiaryDataLength);

            // --- Encryption keys ---
            CaptureRegion(D890UvMemoryMap.AesEncryptionKeyData, D890UvMemoryMap.AesEncryptionKeyStride * D890UvMemoryMap.AesEncryptionKeyMaxSlots);
            CaptureRegion(D890UvMemoryMap.Arc4EncryptionKeyData, D890UvMemoryMap.Arc4EncryptionKeyStride * D890UvMemoryMap.Arc4EncryptionKeyMaxSlots);
            // Known gap (2026-07-18 session finding): an unrelated "01 01 02 02..."
            // preamble/index table sits at 0x3585000, between ARC4's read bound
            // (0x3584000 + 64*0x40 = 0x3585000) and Basic's start (0x3585100) -
            // not decoded by any entity, but very plausibly in the same physical
            // erase block as AES/ARC4/Basic. Captured here purely for byte-for-byte
            // preservation, never decoded/interpreted.
            CaptureRegion(0x3585000, 0x100);
            CaptureRegion(D890UvMemoryMap.BasicEncryptionCodeData, D890UvMemoryMap.BasicEncryptionCodeStride * D890UvMemoryMap.BasicEncryptionCodeMaxSlots);

            // Digital Contacts deliberately excluded - matches its existing
            // opt-in-only status on read, and the reference project's own
            // separate DIGITAL_CONTACTS write toggle.

            return new RadioCodeplugRawSnapshot { Regions = regions.Select(kv => new CodeplugRawRegion(kv.Key, kv.Value)).OrderBy(r => r.Address).ToList() };
        }
    }

    /// <summary>
    /// Cheaply extends an already-captured snapshot with any of
    /// <paramref name="radioIndices"/> not yet covered - a small, targeted
    /// read (just the missing channel records), NOT a full re-capture.
    /// Exists so writing a brand-new channel doesn't require re-reading the
    /// whole ~80KB codeplug footprint just to pick up one 128-byte record:
    /// see <c>MainViewModel.RadioWrite.cs</c>, which caches one snapshot
    /// across an entire session (from the last Read or Write) rather than
    /// capturing fresh before every write, matching the vendor CPS's own
    /// behavior (`Device::writeOtherData()` never re-reads before writing
    /// either). Already-covered indices are silently skipped. Returns
    /// <paramref name="snapshot"/> unchanged (same reference) if nothing was
    /// missing, so callers can skip opening a connection at all in the
    /// common case.
    /// </summary>
    public static RadioCodeplugRawSnapshot AddMissingChannels(RadioCodeplugRawSnapshot snapshot, IRadioConnection connection, string portName, IEnumerable<int> radioIndices)
    {
        var missing = radioIndices
            .Distinct()
            .Where(idx => snapshot.FindRegionContaining(RadioCodeplugPatcher.ChannelAddress(idx)) is null)
            .ToList();

        if (missing.Count == 0)
        {
            return snapshot;
        }

        if (!RadioWriteVerification.TryOpenInitial(connection, portName, progress: null, out var openError))
        {
            throw new InvalidOperationException($"Could not open port '{portName}' (gave up after {RadioWriteVerification.MaxWaitMs}ms waiting for the radio to respond): {openError}");
        }

        var newRegions = new List<CodeplugRawRegion>(snapshot.Regions);
        try
        {
            var identity = connection.Identify();
            if (!identity.IsRecognizedD890UV)
            {
                throw new InvalidOperationException(
                    $"Unrecognized radio (model='{identity.Model}', version='{identity.Version}'). Expected D890UV V100. Refusing to read memory.");
            }

            foreach (var idx in missing)
            {
                var address = RadioCodeplugPatcher.ChannelAddress(idx);
                var data = connection.ReadMemoryStrict(address, ChannelCodec.RecordLength);
                newRegions.Add(new CodeplugRawRegion(address, data));
            }
        }
        finally
        {
            connection.Close();
        }

        return new RadioCodeplugRawSnapshot { Regions = newRegions };
    }

    /// <summary>
    /// Same purpose as <see cref="AddMissingChannels"/>, for zones: a brand-
    /// new zone's Name/ChannelMembers regions were never captured (the
    /// initial <see cref="Capture"/> only reads those for already-populated
    /// zone indices - see the "--- Zones ---" section above), so writing one
    /// needs this small top-up read first. A-Channel/B-Channel/Hide are
    /// NOT re-checked here - those are always captured in full for every
    /// possible zone slot regardless of population, so they never need
    /// topping up.
    /// </summary>
    public static RadioCodeplugRawSnapshot AddMissingZones(RadioCodeplugRawSnapshot snapshot, IRadioConnection connection, string portName, IEnumerable<int> radioIndices)
    {
        var missing = radioIndices
            .Distinct()
            .Where(idx => snapshot.FindRegionContaining(D890UvMemoryMap.ZonesName + idx * D890UvMemoryMap.ZoneDataOffset) is null)
            .ToList();

        if (missing.Count == 0)
        {
            return snapshot;
        }

        if (!RadioWriteVerification.TryOpenInitial(connection, portName, progress: null, out var openError))
        {
            throw new InvalidOperationException($"Could not open port '{portName}' (gave up after {RadioWriteVerification.MaxWaitMs}ms waiting for the radio to respond): {openError}");
        }

        var newRegions = new List<CodeplugRawRegion>(snapshot.Regions);
        try
        {
            var identity = connection.Identify();
            if (!identity.IsRecognizedD890UV)
            {
                throw new InvalidOperationException(
                    $"Unrecognized radio (model='{identity.Model}', version='{identity.Version}'). Expected D890UV V100. Refusing to read memory.");
            }

            foreach (var idx in missing)
            {
                var nameAddress = D890UvMemoryMap.ZonesName + idx * D890UvMemoryMap.ZoneDataOffset;
                newRegions.Add(new CodeplugRawRegion(nameAddress, connection.ReadMemoryStrict(nameAddress, D890UvMemoryMap.ZoneDataLength)));

                var channelsAddress = D890UvMemoryMap.ZoneChannels + idx * ZoneChannelsRecordBytes;
                newRegions.Add(new CodeplugRawRegion(channelsAddress, connection.ReadMemoryStrict(channelsAddress, ZoneChannelsRecordBytes)));
            }
        }
        finally
        {
            connection.Close();
        }

        return new RadioCodeplugRawSnapshot { Regions = newRegions };
    }

    /// <summary>
    /// Same purpose as <see cref="AddMissingChannels"/>/<see cref="AddMissingZones"/>,
    /// for scan lists - a single contiguous record per index (unlike Zone's
    /// 4 separate arrays), so only one region ever needs topping up.
    /// </summary>
    public static RadioCodeplugRawSnapshot AddMissingScanLists(RadioCodeplugRawSnapshot snapshot, IRadioConnection connection, string portName, IEnumerable<int> radioIndices)
    {
        var missing = radioIndices
            .Distinct()
            .Where(idx => snapshot.FindRegionContaining(RadioCodeplugPatcher.ScanListAddress(idx)) is null)
            .ToList();

        if (missing.Count == 0)
        {
            return snapshot;
        }

        if (!RadioWriteVerification.TryOpenInitial(connection, portName, progress: null, out var openError))
        {
            throw new InvalidOperationException($"Could not open port '{portName}' (gave up after {RadioWriteVerification.MaxWaitMs}ms waiting for the radio to respond): {openError}");
        }

        var newRegions = new List<CodeplugRawRegion>(snapshot.Regions);
        try
        {
            var identity = connection.Identify();
            if (!identity.IsRecognizedD890UV)
            {
                throw new InvalidOperationException(
                    $"Unrecognized radio (model='{identity.Model}', version='{identity.Version}'). Expected D890UV V100. Refusing to read memory.");
            }

            foreach (var idx in missing)
            {
                var address = RadioCodeplugPatcher.ScanListAddress(idx);
                newRegions.Add(new CodeplugRawRegion(address, connection.ReadMemoryStrict(address, ScanListCodec.RecordLength)));
            }
        }
        finally
        {
            connection.Close();
        }

        return new RadioCodeplugRawSnapshot { Regions = newRegions };
    }

    /// <summary>Same purpose as <see cref="AddMissingScanLists"/>, for AM Air
    /// channels. AmAirAddress(VfoIndex) happens to land exactly on
    /// <see cref="D890UvMemoryMap.AmAirVfo"/> (256 slots * 0x40 stride past
    /// AmAirData), and that region is always captured unconditionally by
    /// <see cref="Capture"/>/<see cref="CaptureFromOpenConnection"/>
    /// regardless of the bitmap, so the VFO index naturally never shows up
    /// as "missing" here - no special case needed.</summary>
    public static RadioCodeplugRawSnapshot AddMissingAmAir(RadioCodeplugRawSnapshot snapshot, IRadioConnection connection, string portName, IEnumerable<int> radioIndices)
    {
        var missing = radioIndices
            .Distinct()
            .Where(idx => snapshot.FindRegionContaining(RadioCodeplugPatcher.AmAirAddress(idx)) is null)
            .ToList();

        if (missing.Count == 0)
        {
            return snapshot;
        }

        if (!RadioWriteVerification.TryOpenInitial(connection, portName, progress: null, out var openError))
        {
            throw new InvalidOperationException($"Could not open port '{portName}' (gave up after {RadioWriteVerification.MaxWaitMs}ms waiting for the radio to respond): {openError}");
        }

        var newRegions = new List<CodeplugRawRegion>(snapshot.Regions);
        try
        {
            var identity = connection.Identify();
            if (!identity.IsRecognizedD890UV)
            {
                throw new InvalidOperationException(
                    $"Unrecognized radio (model='{identity.Model}', version='{identity.Version}'). Expected D890UV V100. Refusing to read memory.");
            }

            foreach (var idx in missing)
            {
                var address = RadioCodeplugPatcher.AmAirAddress(idx);
                newRegions.Add(new CodeplugRawRegion(address, connection.ReadMemoryStrict(address, AmAirCodec.RecordLength)));
            }
        }
        finally
        {
            connection.Close();
        }

        return new RadioCodeplugRawSnapshot { Regions = newRegions };
    }

    /// <summary>Same purpose as <see cref="AddMissingAmAir"/>, for Analog
    /// Address Book entries.</summary>
    public static RadioCodeplugRawSnapshot AddMissingAnalogAddresses(RadioCodeplugRawSnapshot snapshot, IRadioConnection connection, string portName, IEnumerable<int> radioIndices)
    {
        var missing = radioIndices
            .Distinct()
            .Where(idx => snapshot.FindRegionContaining(RadioCodeplugPatcher.AnalogAddressAddress(idx)) is null)
            .ToList();

        if (missing.Count == 0)
        {
            return snapshot;
        }

        if (!RadioWriteVerification.TryOpenInitial(connection, portName, progress: null, out var openError))
        {
            throw new InvalidOperationException($"Could not open port '{portName}' (gave up after {RadioWriteVerification.MaxWaitMs}ms waiting for the radio to respond): {openError}");
        }

        var newRegions = new List<CodeplugRawRegion>(snapshot.Regions);
        try
        {
            var identity = connection.Identify();
            if (!identity.IsRecognizedD890UV)
            {
                throw new InvalidOperationException(
                    $"Unrecognized radio (model='{identity.Model}', version='{identity.Version}'). Expected D890UV V100. Refusing to read memory.");
            }

            foreach (var idx in missing)
            {
                var address = RadioCodeplugPatcher.AnalogAddressAddress(idx);
                newRegions.Add(new CodeplugRawRegion(address, connection.ReadMemoryStrict(address, AnalogAddressCodec.RecordLength)));
            }
        }
        finally
        {
            connection.Close();
        }

        return new RadioCodeplugRawSnapshot { Regions = newRegions };
    }

    /// <summary>Same purpose as <see cref="AddMissingScanLists"/>, for
    /// Radio IDs.</summary>
    public static RadioCodeplugRawSnapshot AddMissingRadioIds(RadioCodeplugRawSnapshot snapshot, IRadioConnection connection, string portName, IEnumerable<int> radioIndices)
    {
        var missing = radioIndices
            .Distinct()
            .Where(idx => snapshot.FindRegionContaining(RadioCodeplugPatcher.RadioIdAddress(idx)) is null)
            .ToList();

        if (missing.Count == 0)
        {
            return snapshot;
        }

        if (!RadioWriteVerification.TryOpenInitial(connection, portName, progress: null, out var openError))
        {
            throw new InvalidOperationException($"Could not open port '{portName}' (gave up after {RadioWriteVerification.MaxWaitMs}ms waiting for the radio to respond): {openError}");
        }

        var newRegions = new List<CodeplugRawRegion>(snapshot.Regions);
        try
        {
            var identity = connection.Identify();
            if (!identity.IsRecognizedD890UV)
            {
                throw new InvalidOperationException(
                    $"Unrecognized radio (model='{identity.Model}', version='{identity.Version}'). Expected D890UV V100. Refusing to read memory.");
            }

            foreach (var idx in missing)
            {
                var address = RadioCodeplugPatcher.RadioIdAddress(idx);
                newRegions.Add(new CodeplugRawRegion(address, connection.ReadMemoryStrict(address, RadioIdCodec.RecordLength)));
            }
        }
        finally
        {
            connection.Close();
        }

        return new RadioCodeplugRawSnapshot { Regions = newRegions };
    }

    /// <summary>Same purpose as <see cref="AddMissingRadioIds"/>, for
    /// Talkgroups - a small, targeted top-up read for any brand-new
    /// Talkgroup row not yet covered by the cached snapshot.</summary>
    public static RadioCodeplugRawSnapshot AddMissingTalkgroups(RadioCodeplugRawSnapshot snapshot, IRadioConnection connection, string portName, IEnumerable<int> radioIndices)
    {
        var missing = radioIndices
            .Distinct()
            .Where(idx => snapshot.FindRegionContaining(RadioCodeplugPatcher.TalkgroupAddress(idx)) is null)
            .ToList();

        if (missing.Count == 0)
        {
            return snapshot;
        }

        if (!RadioWriteVerification.TryOpenInitial(connection, portName, progress: null, out var openError))
        {
            throw new InvalidOperationException($"Could not open port '{portName}' (gave up after {RadioWriteVerification.MaxWaitMs}ms waiting for the radio to respond): {openError}");
        }

        var newRegions = new List<CodeplugRawRegion>(snapshot.Regions);
        try
        {
            var identity = connection.Identify();
            if (!identity.IsRecognizedD890UV)
            {
                throw new InvalidOperationException(
                    $"Unrecognized radio (model='{identity.Model}', version='{identity.Version}'). Expected D890UV V100. Refusing to read memory.");
            }

            foreach (var idx in missing)
            {
                var address = RadioCodeplugPatcher.TalkgroupAddress(idx);
                newRegions.Add(new CodeplugRawRegion(address, connection.ReadMemoryStrict(address, TalkgroupCodec.RecordLength)));
            }
        }
        finally
        {
            connection.Close();
        }

        return new RadioCodeplugRawSnapshot { Regions = newRegions };
    }

    /// <summary>Same purpose as <see cref="AddMissingRadioIds"/>, for
    /// Receive Group Lists - a small, targeted top-up read for any brand-new
    /// Receive Group List row not yet covered by the cached snapshot.</summary>
    public static RadioCodeplugRawSnapshot AddMissingReceiveGroupLists(RadioCodeplugRawSnapshot snapshot, IRadioConnection connection, string portName, IEnumerable<int> radioIndices)
    {
        var missing = radioIndices
            .Distinct()
            .Where(idx => snapshot.FindRegionContaining(RadioCodeplugPatcher.ReceiveGroupListAddress(idx)) is null)
            .ToList();

        if (missing.Count == 0)
        {
            return snapshot;
        }

        if (!RadioWriteVerification.TryOpenInitial(connection, portName, progress: null, out var openError))
        {
            throw new InvalidOperationException($"Could not open port '{portName}' (gave up after {RadioWriteVerification.MaxWaitMs}ms waiting for the radio to respond): {openError}");
        }

        var newRegions = new List<CodeplugRawRegion>(snapshot.Regions);
        try
        {
            var identity = connection.Identify();
            if (!identity.IsRecognizedD890UV)
            {
                throw new InvalidOperationException(
                    $"Unrecognized radio (model='{identity.Model}', version='{identity.Version}'). Expected D890UV V100. Refusing to read memory.");
            }

            foreach (var idx in missing)
            {
                var address = RadioCodeplugPatcher.ReceiveGroupListAddress(idx);
                newRegions.Add(new CodeplugRawRegion(address, connection.ReadMemoryStrict(address, ReceiveGroupListCodec.RecordLength)));
            }
        }
        finally
        {
            connection.Close();
        }

        return new RadioCodeplugRawSnapshot { Regions = newRegions };
    }

    /// <summary>Same purpose as <see cref="AddMissingRadioIds"/>, for
    /// Roaming Channels - a small, targeted top-up read for any brand-new
    /// Roaming Channel row not yet covered by the cached snapshot.</summary>
    public static RadioCodeplugRawSnapshot AddMissingRoamingChannels(RadioCodeplugRawSnapshot snapshot, IRadioConnection connection, string portName, IEnumerable<int> radioIndices)
    {
        var missing = radioIndices
            .Distinct()
            .Where(idx => snapshot.FindRegionContaining(RadioCodeplugPatcher.RoamingChannelAddress(idx)) is null)
            .ToList();

        if (missing.Count == 0)
        {
            return snapshot;
        }

        if (!RadioWriteVerification.TryOpenInitial(connection, portName, progress: null, out var openError))
        {
            throw new InvalidOperationException($"Could not open port '{portName}' (gave up after {RadioWriteVerification.MaxWaitMs}ms waiting for the radio to respond): {openError}");
        }

        var newRegions = new List<CodeplugRawRegion>(snapshot.Regions);
        try
        {
            var identity = connection.Identify();
            if (!identity.IsRecognizedD890UV)
            {
                throw new InvalidOperationException(
                    $"Unrecognized radio (model='{identity.Model}', version='{identity.Version}'). Expected D890UV V100. Refusing to read memory.");
            }

            foreach (var idx in missing)
            {
                var address = RadioCodeplugPatcher.RoamingChannelAddress(idx);
                newRegions.Add(new CodeplugRawRegion(address, connection.ReadMemoryStrict(address, RoamingChannelCodec.RecordLength)));
            }
        }
        finally
        {
            connection.Close();
        }

        return new RadioCodeplugRawSnapshot { Regions = newRegions };
    }

    /// <summary>Same purpose as <see cref="AddMissingRoamingChannels"/>, for
    /// Roaming Zones - a small, targeted top-up read for any brand-new
    /// Roaming Zone row not yet covered by the cached snapshot.</summary>
    public static RadioCodeplugRawSnapshot AddMissingRoamingZones(RadioCodeplugRawSnapshot snapshot, IRadioConnection connection, string portName, IEnumerable<int> radioIndices)
    {
        var missing = radioIndices
            .Distinct()
            .Where(idx => snapshot.FindRegionContaining(RadioCodeplugPatcher.RoamingZoneAddress(idx)) is null)
            .ToList();

        if (missing.Count == 0)
        {
            return snapshot;
        }

        if (!RadioWriteVerification.TryOpenInitial(connection, portName, progress: null, out var openError))
        {
            throw new InvalidOperationException($"Could not open port '{portName}' (gave up after {RadioWriteVerification.MaxWaitMs}ms waiting for the radio to respond): {openError}");
        }

        var newRegions = new List<CodeplugRawRegion>(snapshot.Regions);
        try
        {
            var identity = connection.Identify();
            if (!identity.IsRecognizedD890UV)
            {
                throw new InvalidOperationException(
                    $"Unrecognized radio (model='{identity.Model}', version='{identity.Version}'). Expected D890UV V100. Refusing to read memory.");
            }

            foreach (var idx in missing)
            {
                var address = RadioCodeplugPatcher.RoamingZoneAddress(idx);
                newRegions.Add(new CodeplugRawRegion(address, connection.ReadMemoryStrict(address, RoamingZoneCodec.RecordLength)));
            }
        }
        finally
        {
            connection.Close();
        }

        return new RadioCodeplugRawSnapshot { Regions = newRegions };
    }

    /// <summary>Same purpose as <see cref="AddMissingAmAir"/>, for AM
    /// Zones - tops up both the main record (AmZoneData) and the separate
    /// scan-channel bitmask (AmZoneScan) for any zone not yet captured.
    /// AChannel is never missing: Capture/CaptureFromOpenConnection always
    /// grabs the full 16-zone AmZoneAChannel region in one shot regardless
    /// of the presence bitmap.</summary>
    public static RadioCodeplugRawSnapshot AddMissingAmZones(RadioCodeplugRawSnapshot snapshot, IRadioConnection connection, string portName, IEnumerable<int> radioIndices)
    {
        var missing = radioIndices
            .Distinct()
            .Where(idx => snapshot.FindRegionContaining(RadioCodeplugPatcher.AmZoneAddress(idx)) is null)
            .ToList();

        if (missing.Count == 0)
        {
            return snapshot;
        }

        if (!RadioWriteVerification.TryOpenInitial(connection, portName, progress: null, out var openError))
        {
            throw new InvalidOperationException($"Could not open port '{portName}' (gave up after {RadioWriteVerification.MaxWaitMs}ms waiting for the radio to respond): {openError}");
        }

        var newRegions = new List<CodeplugRawRegion>(snapshot.Regions);
        try
        {
            var identity = connection.Identify();
            if (!identity.IsRecognizedD890UV)
            {
                throw new InvalidOperationException(
                    $"Unrecognized radio (model='{identity.Model}', version='{identity.Version}'). Expected D890UV V100. Refusing to read memory.");
            }

            foreach (var idx in missing)
            {
                var address = RadioCodeplugPatcher.AmZoneAddress(idx);
                newRegions.Add(new CodeplugRawRegion(address, connection.ReadMemoryStrict(address, AmZoneCodec.RecordLength)));

                var scanChannelAddress = D890UvMemoryMap.AmZoneScan + idx * D890UvMemoryMap.AmZoneScanStride;
                newRegions.Add(new CodeplugRawRegion(scanChannelAddress, connection.ReadMemoryStrict(scanChannelAddress, D890UvMemoryMap.AmZoneScanLength)));
            }
        }
        finally
        {
            connection.Close();
        }

        return new RadioCodeplugRawSnapshot { Regions = newRegions };
    }

    /// <summary>Tops up whatever CapturePrefabricatedSms's own (possibly
    /// shorter) walk didn't reach: chain node addresses 0..
    /// <paramref name="requiredChainLength"/>-1 (a growing chain needs a
    /// node beyond what the last read's walk visited), and the text-record
    /// addresses for <paramref name="slotIdsNeedingTextRegion"/> (dirty or
    /// about-to-be-deleted slots, which might be brand new and never
    /// captured at all).</summary>
    public static RadioCodeplugRawSnapshot AddMissingPrefabricatedSms(RadioCodeplugRawSnapshot snapshot, IRadioConnection connection, string portName, int requiredChainLength, IEnumerable<int> slotIdsNeedingTextRegion)
    {
        var missingNodeIndices = Enumerable.Range(0, requiredChainLength)
            .Where(i => snapshot.FindRegionContaining(D890UvMemoryMap.PrefabSmsSet + i * PrefabricatedSmsCodec.SetEntryLength) is null)
            .ToList();
        var missingTextSlots = slotIdsNeedingTextRegion
            .Distinct()
            .Where(id => snapshot.FindRegionContaining(PrefabricatedSmsCodec.ComputeAddress(id)) is null)
            .ToList();

        if (missingNodeIndices.Count == 0 && missingTextSlots.Count == 0)
        {
            return snapshot;
        }

        if (!RadioWriteVerification.TryOpenInitial(connection, portName, progress: null, out var openError))
        {
            throw new InvalidOperationException($"Could not open port '{portName}' (gave up after {RadioWriteVerification.MaxWaitMs}ms waiting for the radio to respond): {openError}");
        }

        var newRegions = new List<CodeplugRawRegion>(snapshot.Regions);
        try
        {
            var identity = connection.Identify();
            if (!identity.IsRecognizedD890UV)
            {
                throw new InvalidOperationException(
                    $"Unrecognized radio (model='{identity.Model}', version='{identity.Version}'). Expected D890UV V100. Refusing to read memory.");
            }

            foreach (var i in missingNodeIndices)
            {
                var address = D890UvMemoryMap.PrefabSmsSet + i * PrefabricatedSmsCodec.SetEntryLength;
                newRegions.Add(new CodeplugRawRegion(address, connection.ReadMemoryStrict(address, PrefabricatedSmsCodec.SetEntryLength)));
            }

            foreach (var id in missingTextSlots)
            {
                var address = PrefabricatedSmsCodec.ComputeAddress(id);
                newRegions.Add(new CodeplugRawRegion(address, connection.ReadMemoryStrict(address, D890UvMemoryMap.PrefabSmsDataLength)));
            }
        }
        finally
        {
            connection.Close();
        }

        return new RadioCodeplugRawSnapshot { Regions = newRegions };
    }

    /// <summary>Same purpose as <see cref="AddMissingAmAir"/>, for FM
    /// broadcast channels - only the per-channel record can ever be missing.
    /// FmMeta (the active/scan bitmaps <see cref="RadioCodeplugPatcher.ApplyFmChannelPatch"/>
    /// patches bits within) is always captured whole by Capture/
    /// CaptureFromOpenConnection regardless of which channels are active, so
    /// it never needs topping up here.</summary>
    public static RadioCodeplugRawSnapshot AddMissingFmChannels(RadioCodeplugRawSnapshot snapshot, IRadioConnection connection, string portName, IEnumerable<int> radioIndices)
    {
        var missing = radioIndices
            .Distinct()
            .Where(idx => snapshot.FindRegionContaining(RadioCodeplugPatcher.FmChannelAddress(idx)) is null)
            .ToList();

        if (missing.Count == 0)
        {
            return snapshot;
        }

        if (!RadioWriteVerification.TryOpenInitial(connection, portName, progress: null, out var openError))
        {
            throw new InvalidOperationException($"Could not open port '{portName}' (gave up after {RadioWriteVerification.MaxWaitMs}ms waiting for the radio to respond): {openError}");
        }

        var newRegions = new List<CodeplugRawRegion>(snapshot.Regions);
        try
        {
            var identity = connection.Identify();
            if (!identity.IsRecognizedD890UV)
            {
                throw new InvalidOperationException(
                    $"Unrecognized radio (model='{identity.Model}', version='{identity.Version}'). Expected D890UV V100. Refusing to read memory.");
            }

            foreach (var idx in missing)
            {
                var address = RadioCodeplugPatcher.FmChannelAddress(idx);
                newRegions.Add(new CodeplugRawRegion(address, connection.ReadMemoryStrict(address, FmChannelCodec.RecordLength)));
            }
        }
        finally
        {
            connection.Close();
        }

        return new RadioCodeplugRawSnapshot { Regions = newRegions };
    }

    private static void CaptureSimpleEntities(
        Func<int, int, byte[]> captureRegion,
        int bitmapAddress,
        int dataBase,
        int stride,
        int recordLength,
        bool invertedBitmap,
        int bitmapBytes)
    {
        var bitmap = captureRegion(bitmapAddress, bitmapBytes);
        var indices = invertedBitmap ? EnumerateUnsetBits(bitmap) : EnumerateSetBits(bitmap);
        foreach (var idx in indices)
        {
            captureRegion(dataBase + idx * stride, recordLength);
        }
    }

    /// <summary>Captures the WHOLE fixed-size whitelist region in one shot
    /// (unlike the early-exit-on-blank-block walk <see cref="RadioCodeplugReader"/>
    /// uses for the live UI read) - <see cref="RadioCodeplugPatcher.ApplyTalkgroupWhitelistPatch"/>/
    /// <see cref="RadioCodeplugPatcher.ApplyDigitalContactWhitelistPatch"/>
    /// need the full region present in the snapshot to patch against, since
    /// they always re-encode and rewrite the entire list, not just the
    /// currently-populated prefix.</summary>
    private static void CaptureWhitelist(Func<int, int, byte[]> captureRegion, int baseAddress)
    {
        captureRegion(baseAddress, TalkgroupWhitelistCodec.MaxBlocks * TalkgroupWhitelistCodec.BlockLength);
    }

    private static void CapturePrefabricatedSms(Func<int, int, byte[]> captureRegion)
    {
        var seen = new bool[PrefabricatedSmsCodec.SlotCount];
        byte current = 0;

        for (var hop = 0; hop <= PrefabricatedSmsCodec.MaxHops; hop++)
        {
            var address = D890UvMemoryMap.PrefabSmsSet + current * PrefabricatedSmsCodec.SetEntryLength;
            var entry = captureRegion(address, PrefabricatedSmsCodec.SetEntryLength);

            if (!PrefabricatedSmsCodec.TryDecodeSetEntry(entry, out var next, out var id))
            {
                break;
            }

            if (id == PrefabricatedSmsCodec.EndMarker || id >= PrefabricatedSmsCodec.SlotCount || seen[id])
            {
                break;
            }

            seen[id] = true;
            var smsAddress = PrefabricatedSmsCodec.ComputeAddress(id);
            captureRegion(smsAddress, D890UvMemoryMap.PrefabSmsDataLength);

            if (next == PrefabricatedSmsCodec.EndMarker)
            {
                break;
            }

            current = next;
        }
    }

    private static List<int> EnumerateSetBits(byte[] bitmap)
    {
        var indices = new List<int>();
        for (var byteIndex = 0; byteIndex < bitmap.Length; byteIndex++)
        {
            var b = bitmap[byteIndex];
            if (b == 0)
            {
                continue;
            }

            for (var bit = 0; bit < 8; bit++)
            {
                if ((b & (1 << bit)) != 0)
                {
                    indices.Add(byteIndex * 8 + bit);
                }
            }
        }

        return indices;
    }

    private static List<int> EnumerateUnsetBits(byte[] bitmap)
    {
        var indices = new List<int>();
        for (var byteIndex = 0; byteIndex < bitmap.Length; byteIndex++)
        {
            var b = bitmap[byteIndex];
            for (var bit = 0; bit < 8; bit++)
            {
                if ((b & (1 << bit)) == 0)
                {
                    indices.Add(byteIndex * 8 + bit);
                }
            }
        }

        return indices;
    }
}

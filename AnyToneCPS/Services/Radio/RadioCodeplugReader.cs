using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using AnyToneCPS.Services.Radio.Codecs;

namespace AnyToneCPS.Services.Radio;

/// <summary>Progress notification for a codeplug read, suitable for
/// reporting from a background <see cref="System.Threading.Tasks.Task"/>
/// via <see cref="IProgress{T}"/> into a ViewModel.</summary>
public sealed record RadioReadProgress(string Message, int Current, int Total);

/// <summary>Result of reading a full codeplug from the radio. Read-only -
/// nothing here is ever written back to the radio.</summary>
public sealed class RadioCodeplugReadResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public RadioIdentity? Identity { get; init; }
    public IReadOnlyList<ChannelCodec.DecodedChannel> Channels { get; init; } = [];
    public IReadOnlyList<ZoneCodec.DecodedZone> Zones { get; init; } = [];
    public IReadOnlyList<RadioIdCodec.DecodedRadioId> RadioIds { get; init; } = [];
    public IReadOnlyList<TalkgroupCodec.DecodedTalkgroup> Talkgroups { get; init; } = [];
    public IReadOnlyList<ScanListCodec.DecodedScanList> ScanLists { get; init; } = [];
    public IReadOnlyList<RoamingChannelCodec.DecodedRoamingChannel> RoamingChannels { get; init; } = [];
    public IReadOnlyList<RoamingZoneCodec.DecodedRoamingZone> RoamingZones { get; init; } = [];
    public IReadOnlyList<ReceiveGroupListCodec.DecodedReceiveGroupList> ReceiveGroupLists { get; init; } = [];
    public IReadOnlyList<AutoRepeaterOffsetCodec.DecodedAutoRepeaterOffset> AutoRepeaterOffsets { get; init; } = [];
    public IReadOnlyList<AnalogAddressCodec.DecodedAnalogAddress> AnalogAddresses { get; init; } = [];
    public IReadOnlyList<GpsRoamingCodec.DecodedGpsRoaming> GpsRoamingEntries { get; init; } = [];
    public IReadOnlyList<TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist> TalkgroupWhitelist { get; init; } = [];
    public IReadOnlyList<TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist> DigitalContactWhitelist { get; init; } = [];
    public IReadOnlyList<PrefabricatedSmsCodec.DecodedPrefabricatedSms> PrefabricatedSms { get; init; } = [];
    public IReadOnlyList<AmAirCodec.DecodedAmAir> AmAirChannels { get; init; } = [];
    public IReadOnlyList<AmZoneCodec.DecodedAmZone> AmZones { get; init; } = [];
    public IReadOnlyList<FmChannelCodec.DecodedFmChannel> FmChannels { get; init; } = [];
    public IReadOnlyList<AnalogQuickCallCodec.DecodedAnalogQuickCall> AnalogQuickCalls { get; init; } = [];
    public IReadOnlyList<string> StateInformation { get; init; } = [];
    public IReadOnlyList<HotKeyCodec.DecodedHotKey> HotKeys { get; init; } = [];
    public IReadOnlyList<Qdc1200IdCodec.DecodedQdc1200Id> Qdc1200Ids { get; init; } = [];
    public Qdc1200SettingsCodec.DecodedQdc1200Settings? Qdc1200Settings { get; init; }
    public IReadOnlyList<QdcAddressCodec.DecodedQdcAddress> QdcAddresses { get; init; } = [];
    public IReadOnlyList<FiveToneIdCodec.DecodedFiveToneId> FiveToneIds { get; init; } = [];
    public FiveToneSettingsCodec.DecodedFiveToneSettings? FiveToneSettings { get; init; }
    public FiveToneSettingsCodec.DecodedFiveToneBotEot? FiveToneBot { get; init; }
    public FiveToneSettingsCodec.DecodedFiveToneBotEot? FiveToneEot { get; init; }

    /// <summary>One entry per slot, index 0 = Information ID NO. 1, index
    /// 15 = Information ID NO. 16 - see D890UvMemoryMap.FiveToneInfoIdData's
    /// own doc comment for why this stays capped at 16 rather than
    /// scaling with FiveToneIds' own 100-row cap. RadioReadMapper.
    /// MapFiveToneIds matches these to rows by Number.</summary>
    public IReadOnlyList<FiveToneInfoIdSlotCodec.DecodedFiveToneInfoIdSlot> FiveToneInfoIdSlots { get; init; } = [];
    public IReadOnlyList<TwoToneEncodeCodec.DecodedTwoToneEncode> TwoToneEncodeEntries { get; init; } = [];
    public IReadOnlyList<TwoToneDecodeCodec.DecodedTwoToneDecode> TwoToneDecodeEntries { get; init; } = [];
    public TwoToneEncodeSettingsCodec.DecodedTwoToneEncodeSettings? TwoToneEncodeSettings { get; init; }
    public DtmfSettingsCodec.DecodedDtmfSettings? DtmfSettings { get; init; }
    public string DtmfBot { get; init; } = "";
    public string DtmfEot { get; init; } = "";
    public string DtmfRemotelyKill { get; init; } = "";
    public string DtmfRemotelyStun { get; init; } = "";
    public int DtmfTransmittingTimeMs { get; init; } = 50;
    public IReadOnlyList<DtmfEncodeCodec.DecodedDtmfEncode> DtmfEncodeEntries { get; init; } = [];
    public LocalInfoCodec.DecodedLocalInfo? LocalInfo { get; init; }
    public MasterIdCodec.DecodedMasterId? MasterId { get; init; }
    public TalkAliasSettingsCodec.DecodedTalkAliasSettings? TalkAliasSettings { get; init; }
    public AlarmSettingsCodec.DecodedAlarmSettings? AlarmSettings { get; init; }
    public AprsSettingsCodec.DecodedAprsSettings? AprsSettings { get; init; }
    public OptionalSettingsCodec.DecodedOptionalSettings? OptionalSettings { get; init; }
    public IReadOnlyList<AprsReceiveFilterCodec.DecodedAprsReceiveFilter> AprsReceiveFilters { get; init; } = [];
    public IReadOnlyList<DigitalContactCodec.DecodedDigitalContact> DigitalContacts { get; init; } = [];
    public IReadOnlyList<EncryptionKeyCodec.DecodedEncryptionKey> AesEncryptionKeys { get; init; } = [];
    public IReadOnlyList<EncryptionKeyCodec.DecodedEncryptionKey> Arc4EncryptionKeys { get; init; } = [];
    public IReadOnlyList<EncryptionKeyCodec.DecodedEncryptionCode> BasicEncryptionCodes { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}

/// <summary>
/// Orchestrates a full, read-only codeplug dump from a D890UV: handshake,
/// identity gate, then channel and zone data via <see cref="IRadioConnection"/>.
/// Contains no protocol bytes of its own - all wire-format knowledge lives in
/// the connection implementation and the codecs in Services/Radio/Codecs.
/// </summary>
public static class RadioCodeplugReader
{
    // Reference project reads 0x200 bytes for the channel-populated bitmap,
    // which covers 4096 possible channel slots (more than the 4002 the
    // D890UV actually supports).
    private const int ChannelBitmapBytes = 0x200;

    // 0x20 bytes covers 256 zone slots, comfortably more than the 250 the
    // D890UV supports; the per-zone data regions below are sized to match.
    private const int ZoneBitmapBytes = 0x20;
    private const int ZoneSlotCount = ZoneBitmapBytes * 8;

    public static RadioCodeplugReadResult Read(
        IRadioConnection connection,
        string portName,
        IProgress<RadioReadProgress>? progress = null,
        bool includeDigitalContacts = false,
        bool includeEncryptionKeys = false)
    {
        var warnings = new List<string>();
        void OnWarning(string message) => warnings.Add(message);

        connection.Warning += OnWarning;
        try
        {
            // A bare single-attempt TryOpen was a real gap here (found
            // 2026-07-19 building a back-to-back read/verify tool): the
            // radio reboots/re-enumerates after ANY session close (read or
            // write), so a second read started immediately after a first
            // one closes can transiently fail the PROGRAM handshake. Same
            // retry-with-wait already used by the write path/raw snapshot
            // capture - see RadioWriteVerification's doc comments.
            if (!RadioWriteVerification.TryOpenInitial(connection, portName, progress is null ? null : new Progress<string>(message => progress.Report(new RadioReadProgress(message, 0, 1))), out var openError))
            {
                return Failure($"Could not open port '{portName}': {openError}", warnings);
            }

            progress?.Report(new RadioReadProgress("Identifying radio...", 0, 1));
            var identity = connection.Identify();
            if (!identity.IsRecognizedD890UV)
            {
                return Failure(
                    $"Unrecognized radio (model='{identity.Model}', version='{identity.Version}'). " +
                    "Expected D890UV V100. Refusing to read memory.",
                    warnings,
                    identity);
            }

            return ReadFromOpenConnection(connection, identity, warnings, progress, includeDigitalContacts, includeEncryptionKeys);
        }
        finally
        {
            connection.Warning -= OnWarning;
            connection.Close();
        }
    }

    /// <summary>Core read logic, assuming <paramref name="connection"/> is
    /// already open and identified as a real D890UV. Extracted 2026-08-01 so
    /// a caller that also needs a
    /// <see cref="RadioCodeplugRawSnapshotReader.CaptureFromOpenConnection"/>
    /// right afterward (see <c>MainViewModel.Radio.cs</c>'s
    /// <c>ReadFromRadioAsync</c>) can run both on ONE open session instead of
    /// two separate <see cref="Read"/>+<see cref="RadioCodeplugRawSnapshotReader.Capture"/>
    /// calls - the radio reboots/re-enumerates its USB after EVERY session
    /// close (read or write, see <see cref="RadioWriteVerification"/>'s doc
    /// comments), so doing this as two full sessions cost a full extra
    /// reboot-and-reopen wait (up to <see cref="RadioWriteVerification.MaxWaitMs"/>)
    /// for no reason - both walks read the exact same set of addresses
    /// anyway (see <see cref="RadioCodeplugRawSnapshot"/>'s own doc comment).
    /// Does not open, identify, subscribe to <see cref="IRadioConnection.Warning"/>,
    /// or close the connection - <paramref name="warnings"/> is the caller's
    /// own list to append into (not owned here), so a caller combining this
    /// with other work can still end up with one merged Warnings list. See
    /// <see cref="Read"/>, above, for the standalone version that manages all
    /// of that itself.</summary>
    public static RadioCodeplugReadResult ReadFromOpenConnection(
        IRadioConnection connection,
        RadioIdentity identity,
        List<string> warnings,
        IProgress<RadioReadProgress>? progress,
        bool includeDigitalContacts,
        bool includeEncryptionKeys)
    {
        {
            var channels = ReadChannels(connection, progress);
            var zones = ReadZones(connection, progress);
            var radioIds = ReadSimpleEntities(
                connection, progress, "radio IDs",
                D890UvMemoryMap.RadioIdSet, D890UvMemoryMap.RadioIdData,
                D890UvMemoryMap.RadioIdDataOffset, D890UvMemoryMap.RadioIdDataLength,
                RadioIdCodec.Decode, invertedBitmap: false);
            var talkgroups = ReadSimpleEntities(
                connection, progress, "talkgroups",
                D890UvMemoryMap.TalkgroupSet, D890UvMemoryMap.TalkgroupData,
                D890UvMemoryMap.TalkgroupDataOffset, D890UvMemoryMap.TalkgroupDataLength,
                TalkgroupCodec.Decode, invertedBitmap: true, bitmapBytes: 0x4F0);
            var scanLists = ReadSimpleEntities(
                connection, progress, "scan lists",
                D890UvMemoryMap.ScanListSet, D890UvMemoryMap.ScanListData,
                D890UvMemoryMap.ScanListDataOffset, D890UvMemoryMap.ScanListDataLength,
                ScanListCodec.Decode, invertedBitmap: false);
            var roamingChannels = ReadSimpleEntities(
                connection, progress, "roaming channels",
                D890UvMemoryMap.RoamingChannelSet, D890UvMemoryMap.RoamingChannelData,
                D890UvMemoryMap.RoamingChannelDataOffset, D890UvMemoryMap.RoamingChannelDataLength,
                RoamingChannelCodec.Decode, invertedBitmap: false);
            var roamingZones = ReadSimpleEntities(
                connection, progress, "roaming zones",
                D890UvMemoryMap.RoamingZoneSet, D890UvMemoryMap.RoamingZoneData,
                D890UvMemoryMap.RoamingZoneDataOffset, D890UvMemoryMap.RoamingZoneDataLength,
                RoamingZoneCodec.Decode, invertedBitmap: false);
            var receiveGroupLists = ReadSimpleEntities(
                connection, progress, "receive group lists",
                D890UvMemoryMap.ReceiveGroupSet, D890UvMemoryMap.ReceiveGroupData,
                D890UvMemoryMap.ReceiveGroupDataOffset, D890UvMemoryMap.ReceiveGroupDataLength,
                ReceiveGroupListCodec.Decode, invertedBitmap: false);
            var autoRepeaterOffsets = ReadAutoRepeaterOffsets(connection, progress);
            var analogAddresses = ReadAnalogAddresses(connection, progress);
            var gpsRoamingEntries = ReadGpsRoaming(connection, progress);
            var talkgroupWhitelist = ReadWhitelist(connection, progress, D890UvMemoryMap.TalkgroupWhitelistData, "talkgroup whitelist");
            var digitalContactWhitelist = ReadWhitelist(connection, progress, D890UvMemoryMap.DigitalContactWhitelistData, "digital contact whitelist");
            var prefabricatedSms = ReadPrefabricatedSms(connection, progress);
            var amAirChannels = ReadAmAir(connection, progress);
            var amZones = ReadAmZones(connection, progress);
            var fmChannels = ReadFmChannels(connection, progress);
            var analogQuickCalls = ReadAnalogQuickCalls(connection, progress);
            var stateInformation = ReadStateInformation(connection, progress);
            var hotKeys = ReadHotKeys(connection, progress);
            var qdc1200Ids = ReadQdc1200Ids(connection, progress);
            progress?.Report(new RadioReadProgress("Reading QDC 1200 settings...", 0, 1));
            var qdc1200SettingsData = connection.ReadMemory(D890UvMemoryMap.Qdc1200SettingsData, Qdc1200SettingsCodec.RecordLength);
            var qdc1200Settings = Qdc1200SettingsCodec.Decode(qdc1200SettingsData);
            var qdcAddresses = ReadQdcAddresses(connection, progress);

            progress?.Report(new RadioReadProgress("Reading 5Tone settings...", 0, 1));
            var fiveToneSingletonData = connection.ReadMemory(D890UvMemoryMap.FiveToneDecodeEncodeData, D890UvMemoryMap.FiveToneDecodeEncodeRecordLength);
            var fiveToneSettings = FiveToneSettingsCodec.DecodeSingleton(fiveToneSingletonData);
            var fiveToneSelfIdLength = fiveToneSettings.SelfId.Length;
            // BOT's real fields live inside the singleton buffer already
            // read above (see D890UvMemoryMap.FiveToneBotSettingsData's
            // own doc comment) - no separate read needed.
            var fiveToneBotOffset = D890UvMemoryMap.FiveToneBotSettingsData - D890UvMemoryMap.FiveToneDecodeEncodeData;
            var fiveToneBotData = fiveToneSingletonData.AsSpan(fiveToneBotOffset, D890UvMemoryMap.FiveToneBotSettingsLength);
            var fiveToneBot = FiveToneSettingsCodec.DecodeBot(fiveToneBotData, fiveToneSelfIdLength);
            var fiveToneEotData = connection.ReadMemory(D890UvMemoryMap.FiveToneEotData, D890UvMemoryMap.FiveToneEotRecordLength);
            var fiveToneEot = FiveToneSettingsCodec.DecodeEot(fiveToneEotData, fiveToneSelfIdLength);
            var fiveToneIds = ReadFiveToneIds(connection, progress, fiveToneSingletonData, fiveToneSelfIdLength);
            var fiveToneInfoIdSlots = ReadFiveToneInfoIdSlots(connection, progress);

            var twoToneEncodeEntries = ReadSimpleEntities(
                connection, progress, "2Tone Encode entries",
                D890UvMemoryMap.TwoToneEncodeBitmap, D890UvMemoryMap.TwoToneEncodeData,
                D890UvMemoryMap.TwoToneEncodeRecordLength, D890UvMemoryMap.TwoToneEncodeRecordLength,
                TwoToneEncodeCodec.Decode, invertedBitmap: false, bitmapBytes: 0x10);
            var twoToneDecodeEntries = ReadSimpleEntities(
                connection, progress, "2Tone Decode entries",
                D890UvMemoryMap.TwoToneDecodeBitmap, D890UvMemoryMap.TwoToneDecodeData,
                D890UvMemoryMap.TwoToneDecodeRecordLength, D890UvMemoryMap.TwoToneDecodeRecordLength,
                TwoToneDecodeCodec.Decode, invertedBitmap: false, bitmapBytes: 0x10);
            progress?.Report(new RadioReadProgress("Reading 2Tone Encode settings...", 0, 1));
            var twoToneEncodeSettingsData = connection.ReadMemory(D890UvMemoryMap.TwoToneEncodeSettingsData, TwoToneEncodeSettingsCodec.RecordLength);
            var twoToneEncodeSettings = TwoToneEncodeSettingsCodec.Decode(twoToneEncodeSettingsData);

            progress?.Report(new RadioReadProgress("Reading DTMF settings...", 0, 1));
            var dtmfSettingsData = connection.ReadMemory(D890UvMemoryMap.DtmfSettingsData, D890UvMemoryMap.DtmfSettingsRecordLength);
            var dtmfSettings = DtmfSettingsCodec.DecodeSingleton(dtmfSettingsData);
            var dtmfBotData = connection.ReadMemory(D890UvMemoryMap.DtmfBotData, D890UvMemoryMap.DtmfSettingsRecordLength);
            var dtmfBot = DtmfSettingsCodec.DecodeCode(dtmfBotData);
            var dtmfEotData = connection.ReadMemory(D890UvMemoryMap.DtmfEotData, D890UvMemoryMap.DtmfSettingsRecordLength);
            var dtmfEot = DtmfSettingsCodec.DecodeCode(dtmfEotData);
            var dtmfRemotelyKillData = connection.ReadMemory(D890UvMemoryMap.DtmfRemotelyKillData, D890UvMemoryMap.DtmfSettingsRecordLength);
            var dtmfRemotelyKill = DtmfSettingsCodec.DecodeCode(dtmfRemotelyKillData);
            var dtmfRemotelyStunData = connection.ReadMemory(D890UvMemoryMap.DtmfRemotelyStunData, D890UvMemoryMap.DtmfSettingsRecordLength);
            var dtmfRemotelyStun = DtmfSettingsCodec.DecodeCode(dtmfRemotelyStunData);
            var dtmfTransmittingTimeIndexData = connection.ReadMemory(D890UvMemoryMap.DtmfTransmittingTimeIndexData, 1);
            var dtmfTransmittingTimeIndex = DtmfSettingsCodec.DecodeTransmittingTimeIndex(dtmfTransmittingTimeIndexData[0]);
            var dtmfTransmittingTimeMs = dtmfTransmittingTimeIndex >= 0 && dtmfTransmittingTimeIndex < DtmfSettingsCodec.TransmittingTimeMsValues.Length
                ? DtmfSettingsCodec.TransmittingTimeMsValues[dtmfTransmittingTimeIndex]
                : 50;
            var dtmfEncodeEntries = ReadDtmfEncodeEntries(connection, progress);

            // This tail stretch (small one-shot reads, no per-item loop of
            // its own) used to report every step as a flat (0, 1) - since
            // RadioReadProgressPercentText computes Current*100/Total, that
            // math always came out to 0%, so the percentage visibly dropped
            // to 0% right after the last big list read (which DOES climb via
            // its own i/count) and then sat there for the rest of the read,
            // including "Reading optional settings..." - the very last step
            // before completion in the common case (encryption keys off).
            // Found 2026-08-04 from a direct user report ("no progress when
            // reading the last optional settings"). Fixed by giving this
            // whole tail its own incrementing step counter instead, so the
            // percentage climbs through it the same way it does everywhere
            // else, rather than freezing.
            var tailStepTotal = includeEncryptionKeys ? 7 : 6;
            var tailStep = 0;

            progress?.Report(new RadioReadProgress("Reading local information...", ++tailStep, tailStepTotal));
            var localInfoBytes = connection.ReadMemory(D890UvMemoryMap.LocalInfo, D890UvMemoryMap.LocalInfoLength);
            var localInfo = LocalInfoCodec.Decode(localInfoBytes);

            progress?.Report(new RadioReadProgress("Reading master ID...", ++tailStep, tailStepTotal));
            var masterIdBytes = connection.ReadMemory(D890UvMemoryMap.MasterIdData, MasterIdCodec.RecordLength);
            var masterId = MasterIdCodec.Decode(masterIdBytes);

            progress?.Report(new RadioReadProgress("Reading talk alias settings...", ++tailStep, tailStepTotal));
            var talkAliasBytes = connection.ReadMemory(D890UvMemoryMap.TalkAliasSettingsBase, D890UvMemoryMap.TalkAliasSettingsReadLength);
            var talkAliasSettings = TalkAliasSettingsCodec.Decode(
                talkAliasBytes[TalkAliasSettingsCodec.DisplayPriorityAddress - D890UvMemoryMap.TalkAliasSettingsBase],
                talkAliasBytes[TalkAliasSettingsCodec.DataFormatAddress - D890UvMemoryMap.TalkAliasSettingsBase]);

            progress?.Report(new RadioReadProgress("Reading alarm settings...", ++tailStep, tailStepTotal));
            var alarmData3483000 = connection.ReadMemory(D890UvMemoryMap.AlarmSettingsData3483000, AlarmSettingsCodec.Data3483000Length);
            var alarmData3482e00 = connection.ReadMemory(D890UvMemoryMap.AlarmSettingsData3482e00, AlarmSettingsCodec.Data3482e00Length);
            var alarmData3500000 = connection.ReadMemory(D890UvMemoryMap.AlarmSettingsData3500000, AlarmSettingsCodec.Data3500000Length);
            var alarmSettings = AlarmSettingsCodec.Decode(alarmData3483000, alarmData3482e00, alarmData3500000);

            progress?.Report(new RadioReadProgress("Reading APRS settings...", ++tailStep, tailStepTotal));
            var aprsMainData = connection.ReadMemory(D890UvMemoryMap.AprsSettingsMainData, AprsSettingsCodec.MainDataLength);

            var aprsReceiveFilterData = connection.ReadMemory(D890UvMemoryMap.AprsReceiveFilterData, D890UvMemoryMap.AprsReceiveFilterDataLength);
            var aprsReceiveFilters = new List<AprsReceiveFilterCodec.DecodedAprsReceiveFilter>(AprsReceiveFilterCodec.EntryCount);
            for (var i = 0; i < AprsReceiveFilterCodec.EntryCount; i++)
            {
                var offset = i * AprsReceiveFilterCodec.RecordLength;
                aprsReceiveFilters.Add(AprsReceiveFilterCodec.Decode(aprsReceiveFilterData.AsSpan(offset, AprsReceiveFilterCodec.RecordLength), i));
            }

            progress?.Report(new RadioReadProgress("Reading optional settings...", ++tailStep, tailStepTotal));
            var optionalData3500000 = connection.ReadMemory(D890UvMemoryMap.OptionalSettingsData3500000, OptionalSettingsCodec.MainDataLength);
            var optionalData3500900 = connection.ReadMemory(D890UvMemoryMap.OptionalSettingsData3500900, OptionalSettingsCodec.SecondaryDataLength);
            var optionalData3501280 = connection.ReadMemory(D890UvMemoryMap.OptionalSettingsData3501280, OptionalSettingsCodec.TertiaryDataLength);
            var optionalSettings = OptionalSettingsCodec.Decode(optionalData3500000, optionalData3500900, optionalData3501280);

            // FixedLocationBeacon lives outside AprsSettingsMainData, inside
            // this same already-read 0x3500000 buffer - see
            // D890UvMemoryMap.AprsFixedLocationBeaconAddress's doc comment.
            var fixedLocationBeaconOffset = D890UvMemoryMap.AprsFixedLocationBeaconAddress - D890UvMemoryMap.OptionalSettingsData3500000;
            var aprsSettings = AprsSettingsCodec.Decode(aprsMainData, optionalData3500000[fixedLocationBeaconOffset]);

            List<DigitalContactCodec.DecodedDigitalContact> digitalContacts = includeDigitalContacts
                ? ReadDigitalContacts(connection, progress)
                : [];

            IReadOnlyList<EncryptionKeyCodec.DecodedEncryptionKey> aesEncryptionKeys = [];
            IReadOnlyList<EncryptionKeyCodec.DecodedEncryptionKey> arc4EncryptionKeys = [];
            IReadOnlyList<EncryptionKeyCodec.DecodedEncryptionCode> basicEncryptionCodes = [];
            if (includeEncryptionKeys)
            {
                // Off by default - a real USB capture confirmed the vendor
                // CPS never reads this back at all (write-only there), and
                // our own read is genuinely slow (one 16-byte block per
                // serial round-trip, ~1024 of them for the AES table alone).
                // See IncludeEncryptionKeysList's doc comment.
                progress?.Report(new RadioReadProgress("Reading encryption keys...", ++tailStep, tailStepTotal));
                var aesKeyData = connection.ReadMemory(D890UvMemoryMap.AesEncryptionKeyData, D890UvMemoryMap.AesEncryptionKeyStride * D890UvMemoryMap.AesEncryptionKeyMaxSlots);
                aesEncryptionKeys = EncryptionKeyCodec.DecodeAesKeys(aesKeyData);
                var arc4KeyData = connection.ReadMemory(D890UvMemoryMap.Arc4EncryptionKeyData, D890UvMemoryMap.Arc4EncryptionKeyStride * D890UvMemoryMap.Arc4EncryptionKeyMaxSlots);
                arc4EncryptionKeys = EncryptionKeyCodec.DecodeArc4Keys(arc4KeyData);
                var basicCodeData = connection.ReadMemory(D890UvMemoryMap.BasicEncryptionCodeData, D890UvMemoryMap.BasicEncryptionCodeStride * D890UvMemoryMap.BasicEncryptionCodeMaxSlots);
                basicEncryptionCodes = EncryptionKeyCodec.DecodeBasicEncryptionCodes(basicCodeData);
            }

            return new RadioCodeplugReadResult
            {
                Success = true,
                Identity = identity,
                Channels = channels,
                Zones = zones,
                RadioIds = radioIds,
                Talkgroups = talkgroups,
                ScanLists = scanLists,
                RoamingChannels = roamingChannels,
                RoamingZones = roamingZones,
                ReceiveGroupLists = receiveGroupLists,
                AutoRepeaterOffsets = autoRepeaterOffsets,
                AnalogAddresses = analogAddresses,
                GpsRoamingEntries = gpsRoamingEntries,
                TalkgroupWhitelist = talkgroupWhitelist,
                DigitalContactWhitelist = digitalContactWhitelist,
                PrefabricatedSms = prefabricatedSms,
                AmAirChannels = amAirChannels,
                AmZones = amZones,
                FmChannels = fmChannels,
                AnalogQuickCalls = analogQuickCalls,
                StateInformation = stateInformation,
                HotKeys = hotKeys,
                Qdc1200Ids = qdc1200Ids,
                Qdc1200Settings = qdc1200Settings,
                QdcAddresses = qdcAddresses,
                FiveToneIds = fiveToneIds,
                FiveToneInfoIdSlots = fiveToneInfoIdSlots,
                FiveToneSettings = fiveToneSettings,
                FiveToneBot = fiveToneBot,
                FiveToneEot = fiveToneEot,
                TwoToneEncodeEntries = twoToneEncodeEntries,
                TwoToneDecodeEntries = twoToneDecodeEntries,
                TwoToneEncodeSettings = twoToneEncodeSettings,
                DtmfSettings = dtmfSettings,
                DtmfBot = dtmfBot,
                DtmfEot = dtmfEot,
                DtmfRemotelyKill = dtmfRemotelyKill,
                DtmfRemotelyStun = dtmfRemotelyStun,
                DtmfTransmittingTimeMs = dtmfTransmittingTimeMs,
                DtmfEncodeEntries = dtmfEncodeEntries,
                LocalInfo = localInfo,
                MasterId = masterId,
                TalkAliasSettings = talkAliasSettings,
                AlarmSettings = alarmSettings,
                AprsSettings = aprsSettings,
                AprsReceiveFilters = aprsReceiveFilters,
                OptionalSettings = optionalSettings,
                DigitalContacts = digitalContacts,
                AesEncryptionKeys = aesEncryptionKeys,
                Arc4EncryptionKeys = arc4EncryptionKeys,
                BasicEncryptionCodes = basicEncryptionCodes,
                Warnings = warnings
            };
        }
    }

    private static List<ChannelCodec.DecodedChannel> ReadChannels(
        IRadioConnection connection,
        IProgress<RadioReadProgress>? progress)
    {
        progress?.Report(new RadioReadProgress("Reading channel index...", 0, 1));
        var bitmap = connection.ReadMemory(D890UvMemoryMap.ChannelSet, ChannelBitmapBytes);
        // VFO A/B (indices 4000/4001, right after the 4000 regular channel
        // slots) have their presence bits set just like any real channel,
        // but they are NOT regular user-managed channels - see
        // D890UvMemoryMap.MaxRegularChannelCount's doc comment for the
        // real incident this caused when they leaked into this list.
        var indices = EnumerateSetBits(bitmap).Where(idx => idx < D890UvMemoryMap.MaxRegularChannelCount).ToList();

        var channels = new List<ChannelCodec.DecodedChannel>(indices.Count);
        for (var i = 0; i < indices.Count; i++)
        {
            var idx = indices[i];
            progress?.Report(new RadioReadProgress("Reading channels...", i + 1, indices.Count));

            var blockIndex = idx / D890UvMemoryMap.ChannelDataBlockSize;
            var indexInBlock = idx % D890UvMemoryMap.ChannelDataBlockSize;
            var address = D890UvMemoryMap.ChannelData
                + blockIndex * D890UvMemoryMap.ChannelDataBlockOffset
                + indexInBlock * D890UvMemoryMap.ChannelDataOffset;

            var record = connection.ReadMemory(address, ChannelCodec.RecordLength);
            channels.Add(ChannelCodec.Decode(record, idx));
        }

        return channels;
    }

    // Per-zone channel-membership record size (128 x uint16 channel indices).
    private const int ZoneChannelsRecordBytes = 0x200;

    private static List<ZoneCodec.DecodedZone> ReadZones(
        IRadioConnection connection,
        IProgress<RadioReadProgress>? progress)
    {
        progress?.Report(new RadioReadProgress("Reading zone index...", 0, 1));
        var bitmap = connection.ReadMemory(D890UvMemoryMap.ZoneSet, ZoneBitmapBytes);
        var indices = EnumerateSetBits(bitmap);

        // Only the small, fixed-size flat arrays (A-channel/B-channel index,
        // hide bitmap) are cheap enough to batch-read for all 256 possible
        // zone slots up front - this matches the reference project exactly.
        // The NAME and (much larger, 0x200-byte) CHANNEL-MEMBERSHIP regions
        // are read per POPULATED zone only, inside the loop below - reading
        // those for all 256 slots regardless of population would mean ~8200
        // extra 16-byte block reads for slots that don't even exist, which is
        // what made this step appear to hang before this fix.
        progress?.Report(new RadioReadProgress("Reading zone tables...", 0, 1));
        var aChannelRegion = connection.ReadMemory(D890UvMemoryMap.ZoneAChannel, ZoneSlotCount * 2);
        var bChannelRegion = connection.ReadMemory(D890UvMemoryMap.ZoneBChannel, ZoneSlotCount * 2);
        var hideRegion = connection.ReadMemory(D890UvMemoryMap.ZoneHide, ZoneBitmapBytes);

        var zones = new List<ZoneCodec.DecodedZone>(indices.Count);
        for (var i = 0; i < indices.Count; i++)
        {
            var idx = indices[i];
            progress?.Report(new RadioReadProgress("Reading zones...", i + 1, indices.Count));

            var nameBytes = connection.ReadMemory(
                D890UvMemoryMap.ZonesName + idx * D890UvMemoryMap.ZoneDataOffset,
                D890UvMemoryMap.ZoneDataLength);
            var channelsBytes = connection.ReadMemory(
                D890UvMemoryMap.ZoneChannels + idx * ZoneChannelsRecordBytes,
                ZoneChannelsRecordBytes);

            // idx=0 here because nameBytes/channelsBytes are already sliced to
            // just this one zone - unlike aChannelRegion/bChannelRegion/hideRegion,
            // which still span all zones and need the real idx.
            zones.Add(ZoneCodec.Decode(idx, nameBytes, channelsBytes, aChannelRegion, bChannelRegion, hideRegion, nameAndChannelsAlreadySliced: true));
        }

        return zones;
    }

    /// <summary>Enumerates indices of set bits in a bitmap (bit set = populated),
    /// which is the standard convention used by every entity except the
    /// talkgroup set (see <see cref="D890UvMemoryMap.TalkgroupSet"/>).</summary>
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

    /// <summary>
    /// Generic bitmap-driven "flat stride" reader shared by RadioId,
    /// Talkgroup, ScanList, RoamingChannel, RoamingZone, and ReceiveGroupList
    /// - each is: read a presence bitmap, enumerate populated indices, then
    /// read one fixed-length record per index at <c>dataBase + idx*stride</c>.
    /// Talkgroup uniquely uses an INVERTED bitmap (bit set = NOT populated) -
    /// pass <paramref name="invertedBitmap"/> = true for that one.
    /// </summary>
    /// <summary>Custom delegate because <see cref="Func{T1,T2,TResult}"/> cannot
    /// take a <see cref="ReadOnlySpan{T}"/> parameter (ref struct types aren't
    /// valid generic type arguments) even though a plain delegate can.</summary>
    private delegate TDecoded SpanDecoder<out TDecoded>(ReadOnlySpan<byte> data, int index);

    private static List<TDecoded> ReadSimpleEntities<TDecoded>(
        IRadioConnection connection,
        IProgress<RadioReadProgress>? progress,
        string label,
        int bitmapAddress,
        int dataBase,
        int stride,
        int recordLength,
        SpanDecoder<TDecoded> decode,
        bool invertedBitmap,
        int bitmapBytes = 0x20)
    {
        progress?.Report(new RadioReadProgress($"Reading {label} index...", 0, 1));
        var bitmap = connection.ReadMemory(bitmapAddress, bitmapBytes);
        var indices = invertedBitmap ? EnumerateUnsetBits(bitmap) : EnumerateSetBits(bitmap);

        var results = new List<TDecoded>(indices.Count);
        for (var i = 0; i < indices.Count; i++)
        {
            var idx = indices[i];
            progress?.Report(new RadioReadProgress($"Reading {label}...", i + 1, indices.Count));

            var address = dataBase + idx * stride;
            var record = connection.ReadMemory(address, recordLength);
            results.Add(decode(record, idx));
        }

        return results;
    }

    /// <summary>Inverted-bitmap counterpart of <see cref="EnumerateSetBits"/>:
    /// bit UNSET (0) means the index is populated. Used only for
    /// <see cref="D890UvMemoryMap.TalkgroupSet"/>.</summary>
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

    /// <summary>Auto Repeater Offset Frequencies are a flat contiguous array -
    /// no bitmap, no per-entry name, just 250 x 4-byte entries.</summary>
    private static List<AutoRepeaterOffsetCodec.DecodedAutoRepeaterOffset> ReadAutoRepeaterOffsets(
        IRadioConnection connection,
        IProgress<RadioReadProgress>? progress)
    {
        const int readBytes = 0x3F0; // reference reads slightly more than 250*4=0x3E8

        progress?.Report(new RadioReadProgress("Reading auto repeater offsets...", 0, 1));
        var data = connection.ReadMemory(D890UvMemoryMap.AutoRepeaterData, readBytes);

        var count = Math.Min(AutoRepeaterOffsetCodec.EntryCount, data.Length / AutoRepeaterOffsetCodec.RecordLength);
        var results = new List<AutoRepeaterOffsetCodec.DecodedAutoRepeaterOffset>(count);
        for (var i = 0; i < count; i++)
        {
            var offset = i * AutoRepeaterOffsetCodec.RecordLength;
            var record = data.AsSpan(offset, AutoRepeaterOffsetCodec.RecordLength).ToArray();
            var decoded = AutoRepeaterOffsetCodec.Decode(record, i);
            if (decoded.RawOffset != 0)
            {
                results.Add(decoded);
            }
        }

        return results;
    }

    /// <summary>Analog Quick Call: a flat 4-slot array, no bitmap - same
    /// shape as Auto Repeater Offset above. Unconfigured slots (Operation
    /// Type=Off) are skipped, matching the Add/Remove-with-cap UI
    /// convention (see AnalogQuickCallCodec's doc comment).</summary>
    private static List<AnalogQuickCallCodec.DecodedAnalogQuickCall> ReadAnalogQuickCalls(
        IRadioConnection connection,
        IProgress<RadioReadProgress>? progress)
    {
        progress?.Report(new RadioReadProgress("Reading analog quick call...", 0, 1));
        var data = connection.ReadMemory(D890UvMemoryMap.AnalogQuickCallData, AnalogQuickCallCodec.SlotCount * AnalogQuickCallCodec.RecordLength);

        var results = new List<AnalogQuickCallCodec.DecodedAnalogQuickCall>();
        for (var i = 0; i < AnalogQuickCallCodec.SlotCount; i++)
        {
            var offset = i * AnalogQuickCallCodec.RecordLength;
            var decoded = AnalogQuickCallCodec.Decode(data.AsSpan(offset, AnalogQuickCallCodec.RecordLength), i);
            if (decoded.OperationType != 0)
            {
                results.Add(decoded);
            }
        }

        return results;
    }

    /// <summary>State Information: a flat 32-slot array, no bitmap - each
    /// slot decoded independently via TextFieldCodec, blank slots return
    /// an empty string.</summary>
    private static List<string> ReadStateInformation(
        IRadioConnection connection,
        IProgress<RadioReadProgress>? progress)
    {
        progress?.Report(new RadioReadProgress("Reading state information...", 0, 1));
        var data = connection.ReadMemory(D890UvMemoryMap.StateInformationData, D890UvMemoryMap.StateInformationSlotCount * D890UvMemoryMap.StateInformationStride);

        var results = new List<string>(D890UvMemoryMap.StateInformationSlotCount);
        for (var i = 0; i < D890UvMemoryMap.StateInformationSlotCount; i++)
        {
            var offset = i * D890UvMemoryMap.StateInformationStride;
            results.Add(StateInformationCodec.Decode(data.AsSpan(offset, D890UvMemoryMap.StateInformationStride)));
        }

        return results;
    }

    /// <summary>Hot Key: a flat 18-record array, no bitmap - unlike Analog
    /// Quick Call/State Information above, every record is always kept
    /// (not filtered), since the Hot Key list is a fixed named 18-row list
    /// with no Add/Remove (see HotKeyEntry's class doc comment).</summary>
    private static List<HotKeyCodec.DecodedHotKey> ReadHotKeys(
        IRadioConnection connection,
        IProgress<RadioReadProgress>? progress)
    {
        progress?.Report(new RadioReadProgress("Reading hot keys...", 0, 1));
        var data = connection.ReadMemory(D890UvMemoryMap.HotKeyData, HotKeyCodec.KeyCount * HotKeyCodec.RecordLength);

        var results = new List<HotKeyCodec.DecodedHotKey>(HotKeyCodec.KeyCount);
        for (var i = 0; i < HotKeyCodec.KeyCount; i++)
        {
            var offset = i * HotKeyCodec.RecordLength;
            results.Add(HotKeyCodec.Decode(data.AsSpan(offset, HotKeyCodec.RecordLength), i));
        }

        return results;
    }

    /// <summary>QDC 1200 Setting's Encode tab ID table: a flat 100-slot
    /// array, no bitmap or presence list found anywhere nearby in either
    /// capture confirming this codec (see Qdc1200IdCodec's own doc
    /// comment) - a blank Name is treated as "unconfigured" and skipped,
    /// same convention as every other flat-array entity here (Auto
    /// Repeater Offset/Analog Quick Call).</summary>
    private static List<Qdc1200IdCodec.DecodedQdc1200Id> ReadQdc1200Ids(
        IRadioConnection connection,
        IProgress<RadioReadProgress>? progress)
    {
        progress?.Report(new RadioReadProgress("Reading QDC 1200 IDs...", 0, 1));
        var data = connection.ReadMemory(D890UvMemoryMap.Qdc1200IdData, Qdc1200IdCodec.SlotCount * Qdc1200IdCodec.RecordLength);

        var results = new List<Qdc1200IdCodec.DecodedQdc1200Id>();
        for (var i = 0; i < Qdc1200IdCodec.SlotCount; i++)
        {
            var offset = i * Qdc1200IdCodec.RecordLength;
            var decoded = Qdc1200IdCodec.Decode(data.AsSpan(offset, Qdc1200IdCodec.RecordLength), i);
            if (!string.IsNullOrEmpty(decoded.Name))
            {
                results.Add(decoded);
            }
        }

        return results;
    }

    /// <summary>QDC Address Book: a flat 128-slot array, no bitmap or
    /// presence list found anywhere nearby (see QdcAddressCodec's own doc
    /// comment) - a blank Name is treated as "unconfigured" and skipped,
    /// same convention as QDC 1200 Setting's own ID table above.</summary>
    private static List<QdcAddressCodec.DecodedQdcAddress> ReadQdcAddresses(
        IRadioConnection connection,
        IProgress<RadioReadProgress>? progress)
    {
        progress?.Report(new RadioReadProgress("Reading QDC addresses...", 0, 1));
        var data = connection.ReadMemory(D890UvMemoryMap.QdcAddressData, QdcAddressCodec.SlotCount * QdcAddressCodec.RecordLength);

        var results = new List<QdcAddressCodec.DecodedQdcAddress>();
        for (var i = 0; i < QdcAddressCodec.SlotCount; i++)
        {
            var offset = i * QdcAddressCodec.RecordLength;
            var decoded = QdcAddressCodec.Decode(data.AsSpan(offset, QdcAddressCodec.RecordLength), i);
            if (!string.IsNullOrEmpty(decoded.Name))
            {
                results.Add(decoded);
            }
        }

        return results;
    }

    /// <summary>5Tone Settings' ID table: standard bitmap-driven pattern
    /// (100 slots), confirmed 2026-08-06 as the singleton block's own byte
    /// 0 (see D890UvMemoryMap's own doc comment - a real presence bitmap,
    /// not a row count). Needs the caller's already-decoded Self ID length
    /// (Other Side ID is confirmed to always match it exactly, and the
    /// packed Encode ID region has no self-describing length of its own -
    /// see FiveToneIdCodec.Decode's own doc comment) and the singleton's
    /// own raw bytes (already read by the caller, reused here rather than
    /// re-reading the same region a second time just for its bitmap
    /// byte).</summary>
    private static List<FiveToneIdCodec.DecodedFiveToneId> ReadFiveToneIds(
        IRadioConnection connection,
        IProgress<RadioReadProgress>? progress,
        byte[] singletonData,
        int selfIdLength)
    {
        progress?.Report(new RadioReadProgress("Reading 5Tone IDs...", 0, 1));
        var indices = EnumerateSetBits([singletonData[0]]);

        var results = new List<FiveToneIdCodec.DecodedFiveToneId>(indices.Count);
        for (var i = 0; i < indices.Count; i++)
        {
            var idx = indices[i];
            progress?.Report(new RadioReadProgress("Reading 5Tone IDs...", i + 1, indices.Count));

            var address = D890UvMemoryMap.FiveToneIdData + idx * FiveToneIdCodec.RecordLength;
            var record = connection.ReadMemory(address, FiveToneIdCodec.RecordLength);
            results.Add(FiveToneIdCodec.Decode(record, idx, selfIdLength));
        }

        return results;
    }

    /// <summary>The "Information ID / Information Code Function1" slot
    /// array - always read in full (all 16 slots), same reasoning as Auto
    /// Repeater Offset/QDC 1200 ID (small, flat, no presence bitmap of its
    /// own found anywhere nearby - presence is really "does a row with
    /// this Number exist", which RadioReadMapper.MapFiveToneIds checks
    /// against the already-read FiveToneIds list, not against anything in
    /// this region itself).</summary>
    private static List<FiveToneInfoIdSlotCodec.DecodedFiveToneInfoIdSlot> ReadFiveToneInfoIdSlots(
        IRadioConnection connection,
        IProgress<RadioReadProgress>? progress)
    {
        progress?.Report(new RadioReadProgress("Reading 5Tone Information ID slots...", 0, 1));
        var results = new List<FiveToneInfoIdSlotCodec.DecodedFiveToneInfoIdSlot>(D890UvMemoryMap.FiveToneInfoIdSlotCount);
        for (var i = 0; i < D890UvMemoryMap.FiveToneInfoIdSlotCount; i++)
        {
            var address = D890UvMemoryMap.FiveToneInfoIdData + i * D890UvMemoryMap.FiveToneInfoIdSlotStride;
            var record = connection.ReadMemory(address, FiveToneInfoIdSlotCodec.RecordLength);
            results.Add(FiveToneInfoIdSlotCodec.Decode(record));
        }

        return results;
    }

    /// <summary>DTMF's M1-M16 list - no presence bitmap (fixed set, blank
    /// slot = all-0xFF, same convention as every other "fixed, not
    /// addable/removable" list in this app), so always read in full.</summary>
    private static List<DtmfEncodeCodec.DecodedDtmfEncode> ReadDtmfEncodeEntries(
        IRadioConnection connection,
        IProgress<RadioReadProgress>? progress)
    {
        progress?.Report(new RadioReadProgress("Reading DTMF Encode entries...", 0, 1));
        var results = new List<DtmfEncodeCodec.DecodedDtmfEncode>(DtmfEncodeCodec.SlotCount);
        for (var i = 0; i < DtmfEncodeCodec.SlotCount; i++)
        {
            var address = D890UvMemoryMap.DtmfEncodeData + i * D890UvMemoryMap.DtmfEncodeRecordLength;
            var record = connection.ReadMemory(address, DtmfEncodeCodec.RecordLength);
            results.Add(DtmfEncodeCodec.Decode(record, i));
        }

        return results;
    }

    /// <summary>Analog Address Book: not bitmap-driven like every other
    /// entity here - the "id list" at <see cref="D890UvMemoryMap.AnalogBookId"/>
    /// is a flat 256-byte array whose non-0xFF byte VALUES (not positions)
    /// are themselves the populated record indices. Matches the reference
    /// project's own iteration exactly (see the memory map doc comment).</summary>
    private static List<AnalogAddressCodec.DecodedAnalogAddress> ReadAnalogAddresses(
        IRadioConnection connection,
        IProgress<RadioReadProgress>? progress)
    {
        progress?.Report(new RadioReadProgress("Reading analog address book index...", 0, 1));
        var idList = connection.ReadMemory(D890UvMemoryMap.AnalogBookId, D890UvMemoryMap.AnalogBookIdLength);

        var indices = new List<int>();
        foreach (var b in idList)
        {
            if (b != 0xff)
            {
                indices.Add(b);
            }
        }

        var results = new List<AnalogAddressCodec.DecodedAnalogAddress>(indices.Count);
        for (var i = 0; i < indices.Count; i++)
        {
            var idx = indices[i];
            progress?.Report(new RadioReadProgress("Reading analog address book...", i + 1, indices.Count));

            var address = D890UvMemoryMap.AnalogBookData + idx * D890UvMemoryMap.AnalogBookDataStride;
            var record = connection.ReadMemory(address, D890UvMemoryMap.AnalogBookDataLength);
            results.Add(AnalogAddressCodec.Decode(record, idx));
        }

        return results;
    }

    /// <summary>GPS Roaming: fixed 32-slot array, no bitmap - read the whole
    /// block once and slice per-entry, matching the reference project's
    /// packed 2-entries-per-0x20-byte-stride addressing exactly.</summary>
    private static List<GpsRoamingCodec.DecodedGpsRoaming> ReadGpsRoaming(
        IRadioConnection connection,
        IProgress<RadioReadProgress>? progress)
    {
        progress?.Report(new RadioReadProgress("Reading GPS roaming...", 0, 1));
        var data = connection.ReadMemory(D890UvMemoryMap.GpsRoamingData, D890UvMemoryMap.GpsRoamingDataLength);

        var results = new List<GpsRoamingCodec.DecodedGpsRoaming>(GpsRoamingCodec.EntryCount);
        for (var idx = 0; idx < GpsRoamingCodec.EntryCount; idx++)
        {
            var offset = GpsRoamingCodec.OffsetForIndex(idx);
            results.Add(GpsRoamingCodec.Decode(data.AsSpan(offset, GpsRoamingCodec.RecordLength), idx));
        }

        return results;
    }

    /// <summary>Packed stream read, not stride-driven - see
    /// TalkgroupWhitelistCodec's doc comment for the block layout and the
    /// stop condition (blank second half of a block). Shared by both
    /// Talkgroup Whitelist and Digital-Contact Whitelist, which are
    /// byte-for-byte identical in wire format - just different base
    /// addresses and distinct lists in the vendor CPS.</summary>
    private static List<TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist> ReadWhitelist(
        IRadioConnection connection,
        IProgress<RadioReadProgress>? progress,
        int baseAddress,
        string label)
    {
        progress?.Report(new RadioReadProgress($"Reading {label}...", 0, 1));

        var results = new List<TalkgroupWhitelistCodec.DecodedTalkgroupWhitelist>();
        for (var i = 0; i < TalkgroupWhitelistCodec.MaxBlocks; i++)
        {
            var address = baseAddress + i * TalkgroupWhitelistCodec.BlockLength;
            var block = connection.ReadMemory(address, TalkgroupWhitelistCodec.BlockLength);
            var decoded = TalkgroupWhitelistCodec.DecodeBlock(block);

            if (decoded.First is { } first)
            {
                results.Add(first);
            }

            if (decoded.Second is { } second)
            {
                results.Add(second);
            }

            if (decoded.StopReading)
            {
                break;
            }
        }

        return results;
    }

    /// <summary>Digital Contact database - the ONE opt-in read in this app
    /// (see <see cref="DigitalContactCodec"/>'s doc comment for why). Only
    /// called when the caller passes <c>includeDigitalContacts: true</c>.
    /// Reports fine-grained (i/count) progress, matching the reference
    /// project's own dedicated progress UI for this specific read.</summary>
    private static List<DigitalContactCodec.DecodedDigitalContact> ReadDigitalContacts(
        IRadioConnection connection,
        IProgress<RadioReadProgress>? progress)
    {
        progress?.Report(new RadioReadProgress("Reading digital contact count...", 0, 1));
        var count = DigitalContactCodec.ReadCount(connection);

        return DigitalContactCodec.DecodeAll(connection, count, (current, total) =>
        {
            progress?.Report(new RadioReadProgress($"Reading digital contacts ({current}/{total})...", current, total));
        });
    }

    /// <summary>Prefabricated SMS: a linked-list "used slot" index, not a
    /// bitmap - walk it starting at slot 0, following the `next` pointer
    /// embedded in each visited node, until <see cref="PrefabricatedSmsCodec.EndMarker"/>,
    /// a repeated (cyclic) id, or <see cref="PrefabricatedSmsCodec.MaxHops"/>
    /// is reached - matches the reference project's own cycle-safety cap
    /// exactly.</summary>
    private static List<PrefabricatedSmsCodec.DecodedPrefabricatedSms> ReadPrefabricatedSms(
        IRadioConnection connection,
        IProgress<RadioReadProgress>? progress)
    {
        progress?.Report(new RadioReadProgress("Reading prefabricated SMS index...", 0, 1));

        var seen = new bool[PrefabricatedSmsCodec.SlotCount];
        var ids = new List<int>();
        byte current = 0;

        for (var hop = 0; hop <= PrefabricatedSmsCodec.MaxHops; hop++)
        {
            var address = D890UvMemoryMap.PrefabSmsSet + current * PrefabricatedSmsCodec.SetEntryLength;
            var entry = connection.ReadMemory(address, PrefabricatedSmsCodec.SetEntryLength);

            if (!PrefabricatedSmsCodec.TryDecodeSetEntry(entry, out var next, out var id))
            {
                break;
            }

            if (id == PrefabricatedSmsCodec.EndMarker)
            {
                break;
            }

            if (id >= PrefabricatedSmsCodec.SlotCount || seen[id])
            {
                break;
            }

            seen[id] = true;
            ids.Add(id);

            if (next == PrefabricatedSmsCodec.EndMarker)
            {
                break;
            }

            current = next;
        }

        var results = new List<PrefabricatedSmsCodec.DecodedPrefabricatedSms>(ids.Count);
        for (var i = 0; i < ids.Count; i++)
        {
            var idx = ids[i];
            progress?.Report(new RadioReadProgress("Reading prefabricated SMS...", i + 1, ids.Count));

            var address = PrefabricatedSmsCodec.ComputeAddress(idx);
            var record = connection.ReadMemory(address, D890UvMemoryMap.PrefabSmsDataLength);
            results.Add(PrefabricatedSmsCodec.Decode(record, idx));
        }

        return results;
    }

    /// <summary>AM Air: standard bitmap-driven pattern (256 slots) plus one
    /// extra always-present "VFO" record read unconditionally at a separate
    /// fixed address, appended with index 256 (see
    /// <see cref="AmAirCodec.VfoIndex"/>).</summary>
    private static List<AmAirCodec.DecodedAmAir> ReadAmAir(
        IRadioConnection connection,
        IProgress<RadioReadProgress>? progress)
    {
        progress?.Report(new RadioReadProgress("Reading AM air index...", 0, 1));
        var bitmap = connection.ReadMemory(D890UvMemoryMap.AmAirSet, 0x20);
        var indices = EnumerateSetBits(bitmap);

        var results = new List<AmAirCodec.DecodedAmAir>(indices.Count + 1);
        for (var i = 0; i < indices.Count; i++)
        {
            var idx = indices[i];
            progress?.Report(new RadioReadProgress("Reading AM air channels...", i + 1, indices.Count));

            var address = D890UvMemoryMap.AmAirData + idx * D890UvMemoryMap.AmAirDataStride;
            var record = connection.ReadMemory(address, D890UvMemoryMap.AmAirDataLength);
            results.Add(AmAirCodec.Decode(record, idx));
        }

        progress?.Report(new RadioReadProgress("Reading AM air VFO...", 0, 1));
        var vfoRecord = connection.ReadMemory(D890UvMemoryMap.AmAirVfo, D890UvMemoryMap.AmAirDataLength);
        results.Add(AmAirCodec.Decode(vfoRecord, AmAirCodec.VfoIndex));

        return results;
    }

    /// <summary>AM Zone: bitmap of 16 fixed zone slots, per-zone record, plus
    /// a flat parallel uint16 array (AChannel) and a separate per-zone scan-
    /// channel bitmask (see AmZoneCodec's doc comment), both read once per
    /// zone.</summary>
    private static List<AmZoneCodec.DecodedAmZone> ReadAmZones(
        IRadioConnection connection,
        IProgress<RadioReadProgress>? progress)
    {
        progress?.Report(new RadioReadProgress("Reading AM zone index...", 0, 1));
        var bitmap = connection.ReadMemory(D890UvMemoryMap.AmZoneSet, 0x10);
        var indices = EnumerateSetBits(bitmap);

        var aChannelData = connection.ReadMemory(D890UvMemoryMap.AmZoneAChannel, D890UvMemoryMap.AmZoneCount * 2);

        var results = new List<AmZoneCodec.DecodedAmZone>(indices.Count);
        for (var i = 0; i < indices.Count; i++)
        {
            var idx = indices[i];
            if (idx >= D890UvMemoryMap.AmZoneCount)
            {
                continue;
            }

            progress?.Report(new RadioReadProgress("Reading AM zones...", i + 1, indices.Count));

            var address = D890UvMemoryMap.AmZoneData + idx * D890UvMemoryMap.AmZoneDataStride;
            var record = connection.ReadMemory(address, D890UvMemoryMap.AmZoneDataLength);
            var aChannelIndex = BinaryPrimitives.ReadUInt16LittleEndian(aChannelData.AsSpan(idx * 2, 2));
            var scanChannelAddress = D890UvMemoryMap.AmZoneScan + idx * D890UvMemoryMap.AmZoneScanStride;
            var scanChannelBitmask = connection.ReadMemory(scanChannelAddress, D890UvMemoryMap.AmZoneScanLength);
            results.Add(AmZoneCodec.Decode(record, aChannelIndex, scanChannelBitmask, idx));
        }

        return results;
    }

    /// <summary>FM broadcast channels: active/scan bitmasks live inside the
    /// same shared metadata block as the "home"/VFO channel's own record
    /// (see <see cref="D890UvMemoryMap.FmMeta"/>'s doc comment) - not a
    /// separate bitmap region like every other bitmap-driven entity here.</summary>
    private static List<FmChannelCodec.DecodedFmChannel> ReadFmChannels(
        IRadioConnection connection,
        IProgress<RadioReadProgress>? progress)
    {
        progress?.Report(new RadioReadProgress("Reading FM channel index...", 0, 1));
        var meta = connection.ReadMemory(D890UvMemoryMap.FmMeta, D890UvMemoryMap.FmMetaLength);

        var results = new List<FmChannelCodec.DecodedFmChannel>();

        for (var idx = 0; idx < D890UvMemoryMap.FmChannelCount; idx++)
        {
            var byteIndex = idx / 8;
            var bit = idx % 8;
            var active = (meta[D890UvMemoryMap.FmActiveMaskOffset + byteIndex] & (1 << bit)) != 0;
            if (!active)
            {
                continue;
            }

            progress?.Report(new RadioReadProgress("Reading FM channels...", idx + 1, D890UvMemoryMap.FmChannelCount));

            var scanAdd = (meta[D890UvMemoryMap.FmScanMaskOffset + byteIndex] & (1 << bit)) != 0;
            var address = D890UvMemoryMap.FmChannelData + idx * D890UvMemoryMap.FmChannelDataStride;
            var record = connection.ReadMemory(address, FmChannelCodec.RecordLength);
            results.Add(FmChannelCodec.Decode(record, scanAdd, idx));
        }

        progress?.Report(new RadioReadProgress("Reading FM home channel...", 0, 1));
        results.Add(FmChannelCodec.Decode(meta[..FmChannelCodec.RecordLength], scanAdd: true, FmChannelCodec.HomeIndex));

        return results;
    }

    private static RadioCodeplugReadResult Failure(string error, List<string> warnings, RadioIdentity? identity = null)
    {
        return new RadioCodeplugReadResult
        {
            Success = false,
            Error = error,
            Identity = identity,
            Warnings = warnings
        };
    }
}

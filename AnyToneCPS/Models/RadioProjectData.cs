using System.Collections.Generic;

namespace AnyToneCPS.Models;

public sealed class RadioProjectData
{
    public List<ChannelData> Channels { get; set; } = [];
    public List<ZoneData> Zones { get; set; } = [];
    public List<EncryptionKeyData> EncryptionKeys { get; set; } = [];
    public List<EncryptionKeyData> Arc4EncryptionKeys { get; set; } = [];
    public List<EncryptionKeyData> AesEncryptionKeys { get; set; } = [];
    public List<RadioIdData> RadioIds { get; set; } = [];
    public List<TalkgroupData> Talkgroups { get; set; } = [];
    public List<ScanListData> ScanLists { get; set; } = [];
    public List<RoamingChannelData> RoamingChannels { get; set; } = [];
    public List<RoamingZoneData> RoamingZones { get; set; } = [];
    public List<ReceiveGroupListData> ReceiveGroupLists { get; set; } = [];
    public List<AutoRepeaterOffsetData> AutoRepeaterOffsets { get; set; } = [];
    public List<AnalogAddressData> AnalogAddresses { get; set; } = [];
    public List<GpsRoamingData> GpsRoamingEntries { get; set; } = [];
    public List<TalkgroupWhitelistData> TalkgroupWhitelist { get; set; } = [];
    public List<DigitalContactWhitelistData> DigitalContactWhitelist { get; set; } = [];
    public List<PrefabricatedSmsData> PrefabricatedSms { get; set; } = [];
    public List<AmAirData> AmAirChannels { get; set; } = [];
    public List<AmZoneData> AmZones { get; set; } = [];
    public List<FmChannelData> FmChannels { get; set; } = [];
    public MasterIdData? MasterId { get; set; }
    public TalkAliasSettingsData? TalkAliasSettings { get; set; }
    public AlarmSettingsData? AlarmSettings { get; set; }
    public AprsSettingsData? AprsSettings { get; set; }
    public List<AprsReceiveFilterData> AprsReceiveFilters { get; set; } = [];
    public OptionalSettingsData? OptionalSettings { get; set; }

    /// <summary>Only populated if the user opted into reading the Digital
    /// Contact database (off by default - see MainViewModel.Radio.cs'
    /// IncludeDigitalContactList). Can be large; persisted anyway for
    /// consistency with every other entity's save/load round-trip.</summary>
    public List<DigitalContactData> DigitalContacts { get; set; } = [];

    /// <summary>Mirrors MainViewModel's own runtime-only
    /// _digitalContactsGenuinelyPopulatedFromRadio flag into the saved file,
    /// so a Digital Contact List that really did come from a genuine Read
    /// From Radio stays write-eligible after a save/load round trip -
    /// including on a different device (real bug found 2026-08-16: loading
    /// a project on Android always disabled the write-side checkbox, even
    /// for a list that traced back to a genuine Desktop read, because the
    /// flag was never saved anywhere). Still false for any file saved
    /// before this field existed, or for a list that was never genuinely
    /// read - same "must read before this specific write is trusted" gate
    /// as before, just no longer reset by save/load alone.</summary>
    public bool DigitalContactsGenuinelyPopulatedFromRadio { get; set; }
}

public sealed class EncryptionKeyData
{
    public int Number { get; set; }
    public string EncryptionKey { get; set; } = "";
    public string EncryptionId { get; set; } = "";
}

/// <summary>Typed to match the new canonical <see cref="ChannelEntry"/> (see
/// that class's doc comment) - byte/bool/double/ushort fields matching the
/// radio's own wire encoding, not display strings. Breaks old saved project
/// files' Channel data - an accepted, deliberate migration cost.</summary>
public sealed class ChannelData
{
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public double RxFrequencyMHz { get; set; }
    public double OffsetMHz { get; set; }
    public byte OffsetDirection { get; set; }
    public byte ChannelType { get; set; }
    public byte TransmitPower { get; set; }
    public byte Bandwidth { get; set; }
    public byte CtcssDcsDecode { get; set; }
    public byte CtcssDcsEncode { get; set; }
    public byte ColorCode { get; set; }
    public byte TxColorCode { get; set; }
    public bool RepeaterSlot2 { get; set; }
    public ushort ContactIndex { get; set; }
    public ushort RadioIdIndex { get; set; }
    public byte BusyLock { get; set; }
    public byte SquelchMode { get; set; }
    public byte OptionalSignal { get; set; }
    public byte PttId { get; set; }
    public ushort ScanListIndex { get; set; }
    public ushort ReceiveGroupListIndex { get; set; }
    public bool PttProhibit { get; set; }
    public bool Reverse { get; set; }
    public bool SlotSuit { get; set; }
    public byte AesEncryptionIndex { get; set; }
    public bool CallConfirmation { get; set; }
    public bool TalkAround { get; set; }
    public bool WorkAlone { get; set; }
    public ushort CustomCtcss { get; set; }
    public byte CtcssEncodeTone { get; set; }
    public byte CtcssDecodeTone { get; set; }
    public ushort DcsEncodeTone { get; set; }
    public ushort DcsDecodeTone { get; set; }
    public bool AutoScan { get; set; }
    public bool SmsConfirmation { get; set; }
    public byte CorrectFrequencyHz { get; set; }
    public byte DmrModeDcdm { get; set; }
    public bool DmrMode { get; set; }
    public int ScrambleMode { get; set; }
    public int CustomScrambleFrequencyIndex { get; set; }
    public byte Arc4EncryptionKeyIndex { get; set; }
    public byte DigitalEncryptionIndex { get; set; }
    public bool DmrCrcIgnore { get; set; }
    public bool SendTalkerAlias { get; set; }
    public bool SmsForbid { get; set; }
    public bool DataAckDisable { get; set; }
    public bool ExcludeChannelRoaming { get; set; }
    public bool AesRandomKey { get; set; }
    public bool AesMultipleKey { get; set; }
    public bool AprsRx { get; set; }
    public byte DtmfIdIndex { get; set; }
    public byte Tone2IdIndex { get; set; }
    public byte Tone5IdIndex { get; set; }
    public byte Tone2Decode { get; set; }
    public byte R5ToneBot { get; set; }
    public byte R5ToneEot { get; set; }
    public byte QdcIdIndex { get; set; }
    public bool ExtendEncryption { get; set; }
    public bool TxInterrupt { get; set; }
    public bool IdleTx { get; set; }
    public bool Ranging { get; set; }

    // Cached display names for the reference index fields above - resolved
    // once at read time (see ChannelEntry's doc comment). Found missing
    // here 2026-07-19 by the verify-roundtrip diagnostic tool: without
    // these, every digital channel's Contact/Radio ID name silently reset
    // to blank on every save/load round trip.
    public string ContactDisplayName { get; set; } = "";
    public string RadioIdDisplayName { get; set; } = "";
    public string ReceiveGroupListDisplayName { get; set; } = "None";
}

public sealed class ZoneData
{
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public List<int> MemberChannelNumbers { get; set; } = [];
    public int? AChannelNumber { get; set; }
    public int? BChannelNumber { get; set; }
    public bool IsHidden { get; set; }
}

using System.Collections.Generic;

namespace AnyToneCPS.Models;

/// <summary>
/// Serialization-friendly flat DTOs for the new codeplug entity types
/// (RadioId, Talkgroup, ScanList, RoamingChannel, RoamingZone,
/// ReceiveGroupList, AutoRepeaterOffsetFrequency). Split out from
/// RadioProjectData.cs to keep that file from growing unwieldy; the
/// List&lt;FooData&gt; collection properties still live on the
/// <see cref="RadioProjectData"/> class itself in RadioProjectData.cs.
/// All cross-entity references are plain int/long indexes rather than
/// object references, matching the on-radio wire format.
/// </summary>
public sealed class RadioIdData
{
    public int Number { get; set; }
    public long DmrId { get; set; }
    public string Name { get; set; } = "";
}

public sealed class TalkgroupData
{
    public int Number { get; set; }
    public long DmrId { get; set; }
    public string Name { get; set; } = "";
    public string CallType { get; set; } = "Group Call";

    /// <summary>Changed from bool to string 2026-08-07 once the live
    /// capture showed Call Alert is really a 3-state field ("None"/"Ring"/
    /// "Online Alert") - see TalkgroupCodec's doc comment. Project files
    /// saved before this change had CallAlert as a JSON bool - a real user
    /// hit this the same day (JsonException on Open, permanently locked
    /// out of their own saved project) - so
    /// <see cref="Services.BoolTolerantCallAlertJsonConverter"/> tolerates
    /// the old bool shape on read (mapped to Online Alert/None, the
    /// closest reasonable guess) and always writes the new plain string
    /// shape going forward.</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(Services.BoolTolerantCallAlertJsonConverter))]
    public string CallAlert { get; set; } = "None";
}

public sealed class ScanListData
{
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public int PriorityChannelSelect { get; set; }

    /// <summary>Channel Number (not 0-based radio index), matching
    /// ZoneData's AChannelNumber/BChannelNumber convention - resolved back
    /// to a ChannelEntry object via a channel-number lookup on load.</summary>
    public int? PriorityChannel1Number { get; set; }

    public int? PriorityChannel2Number { get; set; }
    public int LookbackTimeA { get; set; }
    public int LookbackTimeB { get; set; }
    public int DropoutDelayTime { get; set; }
    public int DwellTime { get; set; }
    public int RevertChannel { get; set; }

    /// <summary>Channel Numbers (not 0-based radio indexes), matching
    /// ZoneData.MemberChannelNumbers's convention.</summary>
    public List<int> MemberChannelNumbers { get; set; } = [];
}

public sealed class RoamingChannelData
{
    public int Number { get; set; }
    public double RxFrequencyMhz { get; set; }
    public double TxFrequencyMhz { get; set; }
    public int ColorCode { get; set; }
    public int Slot { get; set; }
    public string Name { get; set; } = "";
}

public sealed class RoamingZoneData
{
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public List<int> RoamingChannelIndexes { get; set; } = [];
}

public sealed class ReceiveGroupListData
{
    public int Number { get; set; }
    public string Name { get; set; } = "";
    public List<long> TalkgroupIndexes { get; set; } = [];
}

public sealed class AutoRepeaterOffsetData
{
    public int Number { get; set; }
    public double OffsetFrequencyMhz { get; set; }
    public int RawOffset { get; set; }
}

public sealed class AnalogAddressData
{
    public int Number { get; set; }
    public long AddressNumber { get; set; }
    public string Name { get; set; } = "";
}

public sealed class TalkgroupWhitelistData
{
    public int Number { get; set; }
    public long DmrId { get; set; }
    public int CallType { get; set; }
}

public sealed class DigitalContactWhitelistData
{
    public int Number { get; set; }
    public long DmrId { get; set; }
    public int CallType { get; set; }
}

public sealed class DigitalContactData
{
    public int Index { get; set; }
    public int CallType { get; set; }

    /// <summary>Was a plain bool (IsCallAlert) before 2026-08-09 - same
    /// migration as TalkgroupData.CallAlert, reusing the same tolerant
    /// converter (old true/false projects still load, mapped to Online
    /// Alert/None).</summary>
    [System.Text.Json.Serialization.JsonConverter(typeof(Services.BoolTolerantCallAlertJsonConverter))]
    public string CallAlert { get; set; } = "None";

    /// <summary>Added 2026-08-09 - defaults false, so older project files
    /// (saved before this field existed) load with every contact
    /// unfriended rather than throwing.</summary>
    public bool IsFriend { get; set; }

    public long RadioId { get; set; }
    public string Name { get; set; } = "";
    public string City { get; set; } = "";
    public string Callsign { get; set; } = "";
    public string State { get; set; } = "";
    public string Country { get; set; } = "";
    public string Remarks { get; set; } = "";
}

public sealed class PrefabricatedSmsData
{
    public int Number { get; set; }
    public string Text { get; set; } = "";
}

public sealed class AmAirData
{
    public int Number { get; set; }
    public double FrequencyMhz { get; set; }
    public string Name { get; set; } = "";
}

public sealed class FmChannelData
{
    public int Number { get; set; }
    public double FrequencyMhz { get; set; }
    public string Name { get; set; } = "";
    public bool ScanAdd { get; set; }
}

public sealed class AmZoneData
{
    public int Number { get; set; }
    public string Name { get; set; } = "";

    /// <summary>AM Air channel Number (not 0-based radio index), matching
    /// ZoneData's AChannelNumber/BChannelNumber convention - resolved back
    /// to an AmAirEntry object via a channel-number lookup on load.</summary>
    public int? AChannelNumber { get; set; }

    /// <summary>AM Air channel Numbers (not 0-based radio indexes), matching
    /// ZoneData.MemberChannelNumbers's convention.</summary>
    public List<int> MemberChannelNumbers { get; set; } = [];

    /// <summary>AM Air channel Numbers for the separate "Zone Scan Channel
    /// Member" field - see AmZoneCodec's doc comment. Same Number
    /// convention as MemberChannelNumbers; only ever contains Numbers
    /// 1-128 (AmZoneCodec.ScanChannelBitCount's real hardware limit).</summary>
    public List<int> ScanChannelMemberNumbers { get; set; } = [];
}

public sealed class GpsRoamingData
{
    public int Number { get; set; }
    public bool Enabled { get; set; }
    public int ZoneIndex { get; set; }
    public int LatDegree { get; set; }
    public int LatMinute { get; set; }
    public int LatMinuteDecimal { get; set; }
    public int NorthSouth { get; set; }
    public int LongDegree { get; set; }
    public int LongMinute { get; set; }
    public int LongMinuteDecimal { get; set; }
    public int EastWest { get; set; }
    public int Radius { get; set; }
}

/// <summary>Single instance, not a list - there's only ever one Master ID.</summary>
public sealed class MasterIdData
{
    public long DmrId { get; set; }
    public bool Used { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>Single instance, not a list - there's only ever one Talk Alias
/// Settings record.</summary>
public sealed class AprsFixLocationData
{
    public int Number { get; set; }
    public double Lat { get; set; }
    public byte Ns { get; set; }
    public double Lng { get; set; }
    public byte Ew { get; set; }
}

public sealed class AprsDigitalReportData
{
    public int Number { get; set; }
    public int Channel { get; set; }
    public long TalkgroupId { get; set; }
    public byte CallType { get; set; }
    public byte Slot { get; set; }
}

public sealed class AprsReceiveFilterData
{
    public int Number { get; set; }
    public bool Enabled { get; set; }
    public string Callsign { get; set; } = "";
    public byte Ssid { get; set; }
}

/// <summary>Single instance, not a list - there's only ever one APRS
/// Settings record.</summary>
public sealed class AprsSettingsData
{
    public double TxFreq1Mhz { get; set; }
    public byte TxDelay { get; set; }
    public byte SendSubtone { get; set; }
    public byte Ctcss { get; set; }
    public int Dcs { get; set; }
    public byte ManualTxInterval { get; set; }
    public byte AutoTxInterval { get; set; }
    public byte TxTone { get; set; }
    public byte FixedLocationBeacon { get; set; }

    public double Fix1Lat { get; set; }
    public byte Fix1Ns { get; set; }
    public double Fix1Lng { get; set; }
    public byte Fix1Ew { get; set; }

    public string ToCall { get; set; } = "";
    public byte ToCallSsid { get; set; }
    public string YourCall { get; set; } = "";
    public byte YourCallSsid { get; set; }
    public string DigipeaterPath { get; set; } = "";

    public string AprsSymbol { get; set; } = "";
    public string MapIcon { get; set; } = "";
    public byte TxPower { get; set; }
    public byte PrewaveTime { get; set; }

    public byte RoamingSupport { get; set; }
    public byte RepeaterActivationDelay { get; set; }
    public byte DisTime { get; set; }
    public int Altitude { get; set; }
    public byte AnalogTxMode { get; set; }
    public byte PassAll { get; set; }

    public double TxFreq2Mhz { get; set; }
    public double TxFreq3Mhz { get; set; }
    public double TxFreq4Mhz { get; set; }
    public double TxFreq5Mhz { get; set; }
    public double TxFreq6Mhz { get; set; }
    public double TxFreq7Mhz { get; set; }
    public double TxFreq8Mhz { get; set; }

    public string SendingText { get; set; } = "";

    public bool FilterPosition { get; set; }
    public bool FilterMicE { get; set; }
    public bool FilterObject { get; set; }
    public bool FilterItem { get; set; }
    public bool FilterMessage { get; set; }
    public bool FilterWxReport { get; set; }
    public bool FilterNmeaReport { get; set; }
    public bool FilterStatusReport { get; set; }
    public bool FilterOther { get; set; }

    public List<AprsFixLocationData> AdditionalFixLocations { get; set; } = [];
    public List<AprsDigitalReportData> DigitalReports { get; set; } = [];
}

/// <summary>Single instance, not a list - there's only ever one Optional
/// Settings record. Deliberately a partial port - see
/// OptionalSettingsCodec's doc comment for what's not included yet.</summary>
public sealed class OptionalSettingsData
{
    public byte PowerOnInterface { get; set; }
    public string PowerOnDisplayLine1 { get; set; } = "";
    public string PowerOnDisplayLine2 { get; set; } = "";
    public byte PowerOnPassword { get; set; }
    public string PowerOnPasswordChar { get; set; } = "";
    public byte DefaultStartupChannel { get; set; }
    public byte StartupZoneA { get; set; }
    public byte StartupChannelA { get; set; }
    public byte StartupZoneB { get; set; }
    public byte StartupChannelB { get; set; }
    public byte StartupGpsTest { get; set; }
    public byte StartupReset { get; set; }

    public byte Brightness { get; set; }
    public byte AutoBacklightDuration { get; set; }
    public byte BacklightTxDelay { get; set; }
    public byte MenuExitTime { get; set; }
    public byte TimeDisplay { get; set; }
    public byte LastCaller { get; set; }
    public byte CallDisplayMode { get; set; }
    public byte CallsignDisplayColor { get; set; }
    public byte CallEndPromptBox { get; set; }
    public byte DisplayChannelNumber { get; set; }
    public byte DisplayCurrentContact { get; set; }
    public byte StandbyCharColor { get; set; }
    public byte StandbyBkPicture { get; set; }
    public byte ShowLastCallOnLaunch { get; set; }
    public byte SeparateDisplay { get; set; }
    public byte ChSwitchingKeepsCaller { get; set; }
    public byte BacklightRxDelay { get; set; }
    public byte ChannelNameColorA { get; set; }
    public byte ChannelNameColorB { get; set; }
    public byte ZoneNameColorA { get; set; }
    public byte ZoneNameColorB { get; set; }
    public bool DisplayChannelType { get; set; }
    public bool DisplayTimeSlot { get; set; }
    public bool DisplayColorCode { get; set; }
    public byte DateDisplayFormat { get; set; }
    public byte VolumeBar { get; set; }

    public byte KeyLock { get; set; }
    public byte Pf1ShortKey { get; set; }
    public byte Pf2ShortKey { get; set; }
    public byte Pf3ShortKey { get; set; }
    public byte P1ShortKey { get; set; }
    public byte P2ShortKey { get; set; }
    public byte Pf1LongKey { get; set; }
    public byte Pf2LongKey { get; set; }
    public byte Pf3LongKey { get; set; }
    public byte P1LongKey { get; set; }
    public byte P2LongKey { get; set; }
    public byte LongKeyTime { get; set; }
    public bool KnobLock { get; set; }
    public bool KeyboardLock { get; set; }
    public bool SideKeyLock { get; set; }
    public bool ForcedKeyLock { get; set; }

    public byte SmsAlert { get; set; }
    public byte CallAlert { get; set; }
    public byte DigiCallResetTone { get; set; }
    public byte TalkPermit { get; set; }
    public byte KeyTone { get; set; }
    public byte DigiIdleChannelTone { get; set; }
    public byte StartupSound { get; set; }
    public byte ToneKeySoundAdjustable { get; set; }
    public byte AnalogIdleChannelTone { get; set; }
    public byte PluginRecordingTone { get; set; }

    public byte GpsPower { get; set; }
    public byte GpsPositioning { get; set; }
    public byte TimeZone { get; set; }
    public byte RangingInterval { get; set; }
    public byte DistanceUnit { get; set; }
    public byte GpsTemplateInformation { get; set; }
    public string GpsInformationChar { get; set; } = "";
    public byte GpsMode { get; set; }
    public byte GpsRoaming { get; set; }

    public byte VfoScanType { get; set; }
    public int VfoScanStartFreqUhf { get; set; }
    public int VfoScanEndFreqUhf { get; set; }
    public int VfoScanStartFreqVhf { get; set; }
    public int VfoScanEndFreqVhf { get; set; }

    public byte AutoRepeaterA { get; set; }
    public byte AutoRepeaterB { get; set; }
    public byte AutoRepeater1Uhf { get; set; }
    public byte AutoRepeater1Vhf { get; set; }
    public byte AutoRepeater2Uhf { get; set; }
    public byte AutoRepeater2Vhf { get; set; }
    public byte RepeaterCheck { get; set; }
    public byte RepeaterCheckInterval { get; set; }
    public byte RepeaterCheckReconnections { get; set; }
    public byte RepeaterOutOfRangeNotify { get; set; }
    public byte OutOfRangeNotify { get; set; }
    public byte AutoRoaming { get; set; }
    public byte AutoRoamingStartCondition { get; set; }
    public byte AutoRoamingFixedTime { get; set; }
    public byte RoamingEffectWaitTime { get; set; }
    public byte RoamingZone { get; set; }
    public int AutoRepeater1MinFreqVhf { get; set; }
    public int AutoRepeater1MaxFreqVhf { get; set; }
    public int AutoRepeater1MinFreqUhf { get; set; }
    public int AutoRepeater1MaxFreqUhf { get; set; }
    public int AutoRepeater2MinFreqVhf { get; set; }
    public int AutoRepeater2MaxFreqVhf { get; set; }
    public int AutoRepeater2MinFreqUhf { get; set; }
    public int AutoRepeater2MaxFreqUhf { get; set; }
    public byte RepeaterMode { get; set; }
    public byte RepCcLimit { get; set; }
    public byte RepSlotA { get; set; }
    public byte RepSlotB { get; set; }

    public byte RecordFunction { get; set; }
    public byte RecordDelay { get; set; }

    public byte MaxVolume { get; set; }
    public byte PowerOnVolumeType { get; set; }
    public byte PowerOnVolume { get; set; }
    public byte MaxHeadphoneVolume { get; set; }
    public byte DigiMicGain { get; set; }
    public byte EnhancedSoundQuality { get; set; }
    public byte AnalogMicGain { get; set; }
    public byte RxAgc { get; set; }
    public byte NxMicGain { get; set; }

    public byte DisplayMode { get; set; }
    public byte VfMrA { get; set; }
    public byte VfMrB { get; set; }
    public byte MemZoneA { get; set; }
    public byte MemZoneB { get; set; }
    public byte MainChannelSet { get; set; }
    public byte SubChannelMode { get; set; }
    public byte WorkingMode { get; set; }

    public byte VoxLevel { get; set; }
    public byte VoxDelay { get; set; }
    public byte VoxDetection { get; set; }
    public byte BtOnOff { get; set; }
    public byte BtIntMic { get; set; }
    public byte BtIntSpk { get; set; }
    public byte BtMicGain { get; set; }
    public byte BtSpkGain { get; set; }
    public byte BtHoldTime { get; set; }
    public byte BtRxDelay { get; set; }
    public byte BtPttHold { get; set; }
    public byte BtPttSleepTime { get; set; }
    public byte BtNrBefore { get; set; }
    public byte BtNrAfter { get; set; }

    public byte SteTypeOfCtcss { get; set; }
    public byte SteWhenNoSignal { get; set; }
    public byte SteTime { get; set; }

    public byte AmFmFunction { get; set; }
    public byte FmVfoMem { get; set; }
    public byte FmWorkChannel { get; set; }
    public byte FmMonitor { get; set; }
    public byte AmVfoMem { get; set; }
    public byte AmWorkZone { get; set; }
    public byte AmOffset { get; set; }
    public byte AmSqlLevel { get; set; }

    public byte AutoShutdown { get; set; }
    public byte PowerSave { get; set; }
    public byte AutoShutdownType { get; set; }

    public byte AddressBookSentWithCode { get; set; }
    public byte Tot { get; set; }
    public byte Language { get; set; }
    public byte FrequencyStep { get; set; }
    public byte SqlLevelA { get; set; }
    public byte SqlLevelB { get; set; }
    public byte Tbst { get; set; }
    public byte AnalogCallHoldTime { get; set; }
    public byte CallChannelMaintained { get; set; }
    public byte PriorityZoneA { get; set; }
    public byte PriorityZoneB { get; set; }
    public byte MuteTiming { get; set; }
    public byte EncryptionType { get; set; }
    public byte TotPredict { get; set; }
    public byte TxPowerAgc { get; set; }
    public byte NoaaMoni { get; set; }
    public byte NoaaScan { get; set; }
    public byte Noaa { get; set; }
    public byte NoaaChannel { get; set; }

    public byte GroupCallHoldTime { get; set; }
    public byte PrivateCallHoldTime { get; set; }
    public byte ManualDialGroupCallHoldTime { get; set; }
    public byte ManualDialPrivateCallHoldTime { get; set; }
    public byte VoiceHeaderRepetitions { get; set; }
    public byte TxPreambleDuration { get; set; }
    public byte FilterOwnId { get; set; }
    public byte DigitalRemoteKill { get; set; }
    public byte DigitalMonitor { get; set; }
    public byte DigitalMonitorCc { get; set; }
    public byte DigitalMonitorId { get; set; }
    public byte MonitorSlotHold { get; set; }
    public byte RemoteMonitor { get; set; }
    public byte SmsFormat { get; set; }
    public byte ResetDigitalProtocol { get; set; }

    public byte SatLocation { get; set; }
    public byte SatTxPower { get; set; }
    public byte SatAnaSql { get; set; }
    public byte SatAosLimit { get; set; }

    public List<AlertToneData> AlertTones { get; set; } = [];
}

public sealed class AlertToneData
{
    public string Category { get; set; } = "";
    public int ToneNumber { get; set; }
    public int Frequency { get; set; }
    public int Period { get; set; }
}

public sealed class TalkAliasSettingsData
{
    public byte DisplayPriority { get; set; }
    public byte DataFormat { get; set; }
}

/// <summary>Single instance, not a list - there's only ever one Alarm/
/// Emergency settings record.</summary>
public sealed class AlarmSettingsData
{
    public byte AnalogEmergencyAlarm { get; set; }
    public byte AnalogEniType { get; set; }
    public byte AnalogEmergencyId { get; set; }
    public byte AnalogAlarmTime { get; set; }
    public byte AnalogTxDuration { get; set; }
    public byte AnalogRxDuration { get; set; }
    public int AnalogEmergencyChannel { get; set; }
    public byte AnalogEniSend { get; set; }
    public byte AnalogEmergencyCycle { get; set; }

    public byte DigitalEmergencyAlarm { get; set; }
    public byte DigitalAlarmTime { get; set; }
    public byte DigitalTxDuration { get; set; }
    public byte DigitalRxDuration { get; set; }
    public ushort DigitalEmergencyChannel { get; set; }
    public byte DigitalEmergencyCycle { get; set; }
    public byte DigitalEniSend { get; set; }
    public byte DigitalCallType { get; set; }
    public long DigitalTgDmrId { get; set; }

    public bool ReceiveAlarm { get; set; }
    public bool ManDown { get; set; }
    public byte ManDownDelay { get; set; }

    public byte WorkAloneResponseTime { get; set; }
    public byte WorkAloneWarningTime { get; set; }
    public byte WorkAloneResponse { get; set; }

    public string QdcGroupId { get; set; } = "";
    public string QdcPrivateId { get; set; } = "";
}

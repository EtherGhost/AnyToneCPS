using System;
using System.Collections.Generic;
using System.Linq;
using AnyToneCPS.Models;

namespace AnyToneCPS.Services;

public static class RadioProjectMapper
{
    public static RadioProjectData ToData(
        IEnumerable<ChannelEntry> channels,
        IEnumerable<ZoneEntry> zones,
        IEnumerable<EncryptionKeyEntry>? encryptionKeys = null,
        IEnumerable<EncryptionKeyEntry>? arc4EncryptionKeys = null,
        IEnumerable<EncryptionKeyEntry>? aesEncryptionKeys = null,
        IEnumerable<RadioIdEntry>? radioIds = null,
        IEnumerable<TalkgroupEntry>? talkgroups = null,
        IEnumerable<ScanListEntry>? scanLists = null,
        IEnumerable<RoamingChannelEntry>? roamingChannels = null,
        IEnumerable<RoamingZoneEntry>? roamingZones = null,
        IEnumerable<ReceiveGroupListEntry>? receiveGroupLists = null,
        IEnumerable<AutoRepeaterOffsetEntry>? autoRepeaterOffsets = null,
        MasterIdEntry? masterId = null,
        TalkAliasSettingsEntry? talkAliasSettings = null,
        IEnumerable<AnalogAddressEntry>? analogAddresses = null,
        IEnumerable<GpsRoamingEntry>? gpsRoamingEntries = null,
        IEnumerable<TalkgroupWhitelistEntry>? talkgroupWhitelist = null,
        IEnumerable<PrefabricatedSmsEntry>? prefabricatedSms = null,
        IEnumerable<AmAirEntry>? amAirChannels = null,
        IEnumerable<AmZoneEntry>? amZones = null,
        IEnumerable<FmChannelEntry>? fmChannels = null,
        AlarmSettingsEntry? alarmSettings = null,
        IEnumerable<DigitalContactWhitelistEntry>? digitalContactWhitelist = null,
        AprsSettingsEntry? aprsSettings = null,
        IEnumerable<AprsReceiveFilterEntry>? aprsReceiveFilters = null,
        OptionalSettingsEntry? optionalSettings = null,
        IEnumerable<DigitalContactEntry>? digitalContacts = null,
        bool digitalContactsGenuinelyPopulatedFromRadio = false)
    {
        return new RadioProjectData
        {
            DigitalContactsGenuinelyPopulatedFromRadio = digitalContactsGenuinelyPopulatedFromRadio,
            MasterId = masterId is null ? null : ToData(masterId),
            TalkAliasSettings = talkAliasSettings is null ? null : ToData(talkAliasSettings),
            AlarmSettings = alarmSettings is null ? null : ToData(alarmSettings),
            AprsSettings = aprsSettings is null ? null : ToData(aprsSettings),
            OptionalSettings = optionalSettings is null ? null : ToData(optionalSettings),
            AprsReceiveFilters = (aprsReceiveFilters ?? [])
                .OrderBy(entry => entry.Number)
                .Select(ToData)
                .ToList(),
            Channels = channels
                .OrderBy(channel => channel.Number)
                .Select(ToData)
                .ToList(),
            Zones = zones
                .OrderBy(zone => zone.Number)
                .Select(ToData)
                .ToList(),
            EncryptionKeys = (encryptionKeys ?? [])
                .OrderBy(key => key.Number)
                .Select(ToData)
                .ToList(),
            Arc4EncryptionKeys = (arc4EncryptionKeys ?? [])
                .OrderBy(key => key.Number)
                .Select(ToData)
                .ToList(),
            AesEncryptionKeys = (aesEncryptionKeys ?? [])
                .OrderBy(key => key.Number)
                .Select(ToData)
                .ToList(),
            RadioIds = (radioIds ?? [])
                .OrderBy(entry => entry.Number)
                .Select(ToData)
                .ToList(),
            Talkgroups = (talkgroups ?? [])
                .OrderBy(entry => entry.Number)
                .Select(ToData)
                .ToList(),
            ScanLists = (scanLists ?? [])
                .OrderBy(entry => entry.Number)
                .Select(ToData)
                .ToList(),
            RoamingChannels = (roamingChannels ?? [])
                .OrderBy(entry => entry.Number)
                .Select(ToData)
                .ToList(),
            RoamingZones = (roamingZones ?? [])
                .OrderBy(entry => entry.Number)
                .Select(ToData)
                .ToList(),
            ReceiveGroupLists = (receiveGroupLists ?? [])
                .OrderBy(entry => entry.Number)
                .Select(ToData)
                .ToList(),
            AutoRepeaterOffsets = (autoRepeaterOffsets ?? [])
                .OrderBy(entry => entry.Number)
                .Select(ToData)
                .ToList(),
            AnalogAddresses = (analogAddresses ?? [])
                .OrderBy(entry => entry.Number)
                .Select(ToData)
                .ToList(),
            GpsRoamingEntries = (gpsRoamingEntries ?? [])
                .OrderBy(entry => entry.Number)
                .Select(ToData)
                .ToList(),
            TalkgroupWhitelist = (talkgroupWhitelist ?? [])
                .OrderBy(entry => entry.Number)
                .Select(ToData)
                .ToList(),
            PrefabricatedSms = (prefabricatedSms ?? [])
                .OrderBy(entry => entry.Number)
                .Select(ToData)
                .ToList(),
            AmAirChannels = (amAirChannels ?? [])
                .OrderBy(entry => entry.Number)
                .Select(ToData)
                .ToList(),
            AmZones = (amZones ?? [])
                .OrderBy(entry => entry.Number)
                .Select(ToData)
                .ToList(),
            FmChannels = (fmChannels ?? [])
                .OrderBy(entry => entry.Number)
                .Select(ToData)
                .ToList(),
            DigitalContactWhitelist = (digitalContactWhitelist ?? [])
                .OrderBy(entry => entry.Number)
                .Select(ToData)
                .ToList(),
            DigitalContacts = (digitalContacts ?? [])
                .OrderBy(entry => entry.Index)
                .Select(ToData)
                .ToList()
        };
    }

    public static void LoadInto(
        RadioProjectData data,
        ICollection<ChannelEntry> channels,
        ICollection<ZoneEntry> zones,
        ICollection<EncryptionKeyEntry>? encryptionKeys = null,
        ICollection<EncryptionKeyEntry>? arc4EncryptionKeys = null,
        ICollection<EncryptionKeyEntry>? aesEncryptionKeys = null,
        ICollection<RadioIdEntry>? radioIds = null,
        ICollection<TalkgroupEntry>? talkgroups = null,
        ICollection<ScanListEntry>? scanLists = null,
        ICollection<RoamingChannelEntry>? roamingChannels = null,
        ICollection<RoamingZoneEntry>? roamingZones = null,
        ICollection<ReceiveGroupListEntry>? receiveGroupLists = null,
        ICollection<AutoRepeaterOffsetEntry>? autoRepeaterOffsets = null,
        MasterIdEntry? masterId = null,
        TalkAliasSettingsEntry? talkAliasSettings = null,
        ICollection<AnalogAddressEntry>? analogAddresses = null,
        ICollection<GpsRoamingEntry>? gpsRoamingEntries = null,
        ICollection<TalkgroupWhitelistEntry>? talkgroupWhitelist = null,
        ICollection<PrefabricatedSmsEntry>? prefabricatedSms = null,
        ICollection<AmAirEntry>? amAirChannels = null,
        ICollection<AmZoneEntry>? amZones = null,
        ICollection<FmChannelEntry>? fmChannels = null,
        AlarmSettingsEntry? alarmSettings = null,
        ICollection<DigitalContactWhitelistEntry>? digitalContactWhitelist = null,
        AprsSettingsEntry? aprsSettings = null,
        ICollection<AprsReceiveFilterEntry>? aprsReceiveFilters = null,
        OptionalSettingsEntry? optionalSettings = null,
        ICollection<DigitalContactEntry>? digitalContacts = null)
    {
        if (masterId is not null && data.MasterId is { } masterIdData)
        {
            masterId.DmrId = masterIdData.DmrId;
            masterId.Used = masterIdData.Used;
            masterId.Name = masterIdData.Name;
        }

        if (talkAliasSettings is not null && data.TalkAliasSettings is { } talkAliasData)
        {
            talkAliasSettings.DisplayPriority = talkAliasData.DisplayPriority;
            talkAliasSettings.DataFormat = talkAliasData.DataFormat;
        }

        if (alarmSettings is not null && data.AlarmSettings is { } alarmData)
        {
            alarmSettings.AnalogEmergencyAlarm = alarmData.AnalogEmergencyAlarm;
            alarmSettings.AnalogEniType = alarmData.AnalogEniType;
            alarmSettings.AnalogEmergencyId = alarmData.AnalogEmergencyId;
            alarmSettings.AnalogAlarmTime = alarmData.AnalogAlarmTime;
            alarmSettings.AnalogTxDuration = alarmData.AnalogTxDuration;
            alarmSettings.AnalogRxDuration = alarmData.AnalogRxDuration;
            alarmSettings.AnalogEmergencyChannel = alarmData.AnalogEmergencyChannel;
            alarmSettings.AnalogEniSend = alarmData.AnalogEniSend;
            alarmSettings.AnalogEmergencyCycle = alarmData.AnalogEmergencyCycle;

            alarmSettings.DigitalEmergencyAlarm = alarmData.DigitalEmergencyAlarm;
            alarmSettings.DigitalAlarmTime = alarmData.DigitalAlarmTime;
            alarmSettings.DigitalTxDuration = alarmData.DigitalTxDuration;
            alarmSettings.DigitalRxDuration = alarmData.DigitalRxDuration;
            alarmSettings.DigitalEmergencyChannel = alarmData.DigitalEmergencyChannel;
            alarmSettings.DigitalEmergencyCycle = alarmData.DigitalEmergencyCycle;
            alarmSettings.DigitalEniSend = alarmData.DigitalEniSend;
            alarmSettings.DigitalCallType = alarmData.DigitalCallType;
            alarmSettings.DigitalTgDmrId = alarmData.DigitalTgDmrId;

            alarmSettings.ReceiveAlarm = alarmData.ReceiveAlarm;
            alarmSettings.ManDown = alarmData.ManDown;
            alarmSettings.ManDownDelay = alarmData.ManDownDelay;

            alarmSettings.WorkAloneResponseTime = alarmData.WorkAloneResponseTime;
            alarmSettings.WorkAloneWarningTime = alarmData.WorkAloneWarningTime;
            alarmSettings.WorkAloneResponse = alarmData.WorkAloneResponse;

            alarmSettings.QdcGroupId = alarmData.QdcGroupId;
            alarmSettings.QdcPrivateId = alarmData.QdcPrivateId;
        }

        if (aprsSettings is not null && data.AprsSettings is { } aprsData)
        {
            aprsSettings.TxFreq1Mhz = aprsData.TxFreq1Mhz;
            aprsSettings.TxDelay = aprsData.TxDelay;
            aprsSettings.SendSubtone = aprsData.SendSubtone;
            aprsSettings.Ctcss = aprsData.Ctcss;
            aprsSettings.Dcs = aprsData.Dcs;
            aprsSettings.ManualTxInterval = aprsData.ManualTxInterval;
            aprsSettings.AutoTxInterval = aprsData.AutoTxInterval;
            aprsSettings.TxTone = aprsData.TxTone;
            aprsSettings.FixedLocationBeacon = aprsData.FixedLocationBeacon;

            aprsSettings.Fix1Lat = aprsData.Fix1Lat;
            aprsSettings.Fix1Ns = aprsData.Fix1Ns;
            aprsSettings.Fix1Lng = aprsData.Fix1Lng;
            aprsSettings.Fix1Ew = aprsData.Fix1Ew;

            aprsSettings.ToCall = aprsData.ToCall;
            aprsSettings.ToCallSsid = aprsData.ToCallSsid;
            aprsSettings.YourCall = aprsData.YourCall;
            aprsSettings.YourCallSsid = aprsData.YourCallSsid;
            aprsSettings.DigipeaterPath = aprsData.DigipeaterPath;

            aprsSettings.AprsSymbol = aprsData.AprsSymbol;
            aprsSettings.MapIcon = aprsData.MapIcon;
            aprsSettings.TxPower = aprsData.TxPower;
            aprsSettings.PrewaveTime = aprsData.PrewaveTime;

            aprsSettings.RoamingSupport = aprsData.RoamingSupport;
            aprsSettings.RepeaterActivationDelay = aprsData.RepeaterActivationDelay;
            aprsSettings.DisTime = aprsData.DisTime;
            aprsSettings.Altitude = aprsData.Altitude;
            aprsSettings.AnalogTxMode = aprsData.AnalogTxMode;
            aprsSettings.PassAll = aprsData.PassAll;

            aprsSettings.TxFreq2Mhz = aprsData.TxFreq2Mhz;
            aprsSettings.TxFreq3Mhz = aprsData.TxFreq3Mhz;
            aprsSettings.TxFreq4Mhz = aprsData.TxFreq4Mhz;
            aprsSettings.TxFreq5Mhz = aprsData.TxFreq5Mhz;
            aprsSettings.TxFreq6Mhz = aprsData.TxFreq6Mhz;
            aprsSettings.TxFreq7Mhz = aprsData.TxFreq7Mhz;
            aprsSettings.TxFreq8Mhz = aprsData.TxFreq8Mhz;

            aprsSettings.SendingText = aprsData.SendingText;

            aprsSettings.FilterPosition = aprsData.FilterPosition;
            aprsSettings.FilterMicE = aprsData.FilterMicE;
            aprsSettings.FilterObject = aprsData.FilterObject;
            aprsSettings.FilterItem = aprsData.FilterItem;
            aprsSettings.FilterMessage = aprsData.FilterMessage;
            aprsSettings.FilterWxReport = aprsData.FilterWxReport;
            aprsSettings.FilterNmeaReport = aprsData.FilterNmeaReport;
            aprsSettings.FilterStatusReport = aprsData.FilterStatusReport;
            aprsSettings.FilterOther = aprsData.FilterOther;

            foreach (var fixData in aprsData.AdditionalFixLocations)
            {
                var existing = aprsSettings.AdditionalFixLocations.FirstOrDefault(f => f.Number == fixData.Number);
                if (existing is null)
                {
                    continue;
                }

                existing.Lat = fixData.Lat;
                existing.Ns = fixData.Ns;
                existing.Lng = fixData.Lng;
                existing.Ew = fixData.Ew;
            }

            foreach (var reportData in aprsData.DigitalReports)
            {
                var existing = aprsSettings.DigitalReports.FirstOrDefault(r => r.Number == reportData.Number);
                if (existing is null)
                {
                    continue;
                }

                existing.Channel = reportData.Channel;
                existing.TalkgroupId = reportData.TalkgroupId;
                existing.CallType = reportData.CallType;
                existing.Slot = reportData.Slot;
            }
        }

        if (optionalSettings is not null && data.OptionalSettings is { } optionalData)
        {
            optionalSettings.PowerOnInterface = optionalData.PowerOnInterface;
            optionalSettings.PowerOnDisplayLine1 = optionalData.PowerOnDisplayLine1;
            optionalSettings.PowerOnDisplayLine2 = optionalData.PowerOnDisplayLine2;
            optionalSettings.PowerOnPassword = optionalData.PowerOnPassword;
            optionalSettings.PowerOnPasswordChar = optionalData.PowerOnPasswordChar;
            optionalSettings.DefaultStartupChannel = optionalData.DefaultStartupChannel;
            optionalSettings.StartupZoneA = optionalData.StartupZoneA;
            optionalSettings.StartupChannelA = optionalData.StartupChannelA;
            optionalSettings.StartupZoneB = optionalData.StartupZoneB;
            optionalSettings.StartupChannelB = optionalData.StartupChannelB;
            optionalSettings.StartupGpsTest = optionalData.StartupGpsTest;
            optionalSettings.StartupReset = optionalData.StartupReset;

            optionalSettings.Brightness = optionalData.Brightness;
            optionalSettings.AutoBacklightDuration = optionalData.AutoBacklightDuration;
            optionalSettings.BacklightTxDelay = optionalData.BacklightTxDelay;
            optionalSettings.MenuExitTime = optionalData.MenuExitTime;
            optionalSettings.TimeDisplay = optionalData.TimeDisplay;
            optionalSettings.LastCaller = optionalData.LastCaller;
            optionalSettings.CallDisplayMode = optionalData.CallDisplayMode;
            optionalSettings.CallsignDisplayColor = optionalData.CallsignDisplayColor;
            optionalSettings.CallEndPromptBox = optionalData.CallEndPromptBox;
            optionalSettings.DisplayChannelNumber = optionalData.DisplayChannelNumber;
            optionalSettings.DisplayCurrentContact = optionalData.DisplayCurrentContact;
            optionalSettings.StandbyCharColor = optionalData.StandbyCharColor;
            optionalSettings.StandbyBkPicture = optionalData.StandbyBkPicture;
            optionalSettings.ShowLastCallOnLaunch = optionalData.ShowLastCallOnLaunch;
            optionalSettings.SeparateDisplay = optionalData.SeparateDisplay;
            optionalSettings.ChSwitchingKeepsCaller = optionalData.ChSwitchingKeepsCaller;
            optionalSettings.BacklightRxDelay = optionalData.BacklightRxDelay;
            optionalSettings.ChannelNameColorA = optionalData.ChannelNameColorA;
            optionalSettings.ChannelNameColorB = optionalData.ChannelNameColorB;
            optionalSettings.ZoneNameColorA = optionalData.ZoneNameColorA;
            optionalSettings.ZoneNameColorB = optionalData.ZoneNameColorB;
            optionalSettings.DisplayChannelType = optionalData.DisplayChannelType;
            optionalSettings.DisplayTimeSlot = optionalData.DisplayTimeSlot;
            optionalSettings.DisplayColorCode = optionalData.DisplayColorCode;
            optionalSettings.DateDisplayFormat = optionalData.DateDisplayFormat;
            optionalSettings.VolumeBar = optionalData.VolumeBar;

            optionalSettings.KeyLock = optionalData.KeyLock;
            optionalSettings.Pf1ShortKey = optionalData.Pf1ShortKey;
            optionalSettings.Pf2ShortKey = optionalData.Pf2ShortKey;
            optionalSettings.Pf3ShortKey = optionalData.Pf3ShortKey;
            optionalSettings.P1ShortKey = optionalData.P1ShortKey;
            optionalSettings.P2ShortKey = optionalData.P2ShortKey;
            optionalSettings.Pf1LongKey = optionalData.Pf1LongKey;
            optionalSettings.Pf2LongKey = optionalData.Pf2LongKey;
            optionalSettings.Pf3LongKey = optionalData.Pf3LongKey;
            optionalSettings.P1LongKey = optionalData.P1LongKey;
            optionalSettings.P2LongKey = optionalData.P2LongKey;
            optionalSettings.LongKeyTime = optionalData.LongKeyTime;
            optionalSettings.KnobLock = optionalData.KnobLock;
            optionalSettings.KeyboardLock = optionalData.KeyboardLock;
            optionalSettings.SideKeyLock = optionalData.SideKeyLock;
            optionalSettings.ForcedKeyLock = optionalData.ForcedKeyLock;

            optionalSettings.SmsAlert = optionalData.SmsAlert;
            optionalSettings.CallAlert = optionalData.CallAlert;
            optionalSettings.DigiCallResetTone = optionalData.DigiCallResetTone;
            optionalSettings.TalkPermit = optionalData.TalkPermit;
            optionalSettings.KeyTone = optionalData.KeyTone;
            optionalSettings.DigiIdleChannelTone = optionalData.DigiIdleChannelTone;
            optionalSettings.StartupSound = optionalData.StartupSound;
            optionalSettings.ToneKeySoundAdjustable = optionalData.ToneKeySoundAdjustable;
            optionalSettings.AnalogIdleChannelTone = optionalData.AnalogIdleChannelTone;
            optionalSettings.PluginRecordingTone = optionalData.PluginRecordingTone;

            optionalSettings.GpsPower = optionalData.GpsPower;
            optionalSettings.GpsPositioning = optionalData.GpsPositioning;
            optionalSettings.TimeZone = optionalData.TimeZone;
            optionalSettings.RangingInterval = optionalData.RangingInterval;
            optionalSettings.DistanceUnit = optionalData.DistanceUnit;
            optionalSettings.GpsTemplateInformation = optionalData.GpsTemplateInformation;
            optionalSettings.GpsInformationChar = optionalData.GpsInformationChar;
            optionalSettings.GpsMode = optionalData.GpsMode;
            optionalSettings.GpsRoaming = optionalData.GpsRoaming;

            optionalSettings.VfoScanType = optionalData.VfoScanType;
            optionalSettings.VfoScanStartFreqUhf = optionalData.VfoScanStartFreqUhf;
            optionalSettings.VfoScanEndFreqUhf = optionalData.VfoScanEndFreqUhf;
            optionalSettings.VfoScanStartFreqVhf = optionalData.VfoScanStartFreqVhf;
            optionalSettings.VfoScanEndFreqVhf = optionalData.VfoScanEndFreqVhf;

            optionalSettings.AutoRepeaterA = optionalData.AutoRepeaterA;
            optionalSettings.AutoRepeaterB = optionalData.AutoRepeaterB;
            optionalSettings.AutoRepeater1Uhf = optionalData.AutoRepeater1Uhf;
            optionalSettings.AutoRepeater1Vhf = optionalData.AutoRepeater1Vhf;
            optionalSettings.AutoRepeater2Uhf = optionalData.AutoRepeater2Uhf;
            optionalSettings.AutoRepeater2Vhf = optionalData.AutoRepeater2Vhf;
            optionalSettings.RepeaterCheck = optionalData.RepeaterCheck;
            optionalSettings.RepeaterCheckInterval = optionalData.RepeaterCheckInterval;
            optionalSettings.RepeaterCheckReconnections = optionalData.RepeaterCheckReconnections;
            optionalSettings.RepeaterOutOfRangeNotify = optionalData.RepeaterOutOfRangeNotify;
            optionalSettings.OutOfRangeNotify = optionalData.OutOfRangeNotify;
            optionalSettings.AutoRoaming = optionalData.AutoRoaming;
            optionalSettings.AutoRoamingStartCondition = optionalData.AutoRoamingStartCondition;
            optionalSettings.AutoRoamingFixedTime = optionalData.AutoRoamingFixedTime;
            optionalSettings.RoamingEffectWaitTime = optionalData.RoamingEffectWaitTime;
            optionalSettings.RoamingZone = optionalData.RoamingZone;
            optionalSettings.AutoRepeater1MinFreqVhf = optionalData.AutoRepeater1MinFreqVhf;
            optionalSettings.AutoRepeater1MaxFreqVhf = optionalData.AutoRepeater1MaxFreqVhf;
            optionalSettings.AutoRepeater1MinFreqUhf = optionalData.AutoRepeater1MinFreqUhf;
            optionalSettings.AutoRepeater1MaxFreqUhf = optionalData.AutoRepeater1MaxFreqUhf;
            optionalSettings.AutoRepeater2MinFreqVhf = optionalData.AutoRepeater2MinFreqVhf;
            optionalSettings.AutoRepeater2MaxFreqVhf = optionalData.AutoRepeater2MaxFreqVhf;
            optionalSettings.AutoRepeater2MinFreqUhf = optionalData.AutoRepeater2MinFreqUhf;
            optionalSettings.AutoRepeater2MaxFreqUhf = optionalData.AutoRepeater2MaxFreqUhf;
            optionalSettings.RepeaterMode = optionalData.RepeaterMode;
            optionalSettings.RepCcLimit = optionalData.RepCcLimit;
            optionalSettings.RepSlotA = optionalData.RepSlotA;
            optionalSettings.RepSlotB = optionalData.RepSlotB;

            optionalSettings.RecordFunction = optionalData.RecordFunction;
            optionalSettings.RecordDelay = optionalData.RecordDelay;

            optionalSettings.MaxVolume = optionalData.MaxVolume;
            optionalSettings.PowerOnVolumeType = optionalData.PowerOnVolumeType;
            optionalSettings.PowerOnVolume = optionalData.PowerOnVolume;
            optionalSettings.MaxHeadphoneVolume = optionalData.MaxHeadphoneVolume;
            optionalSettings.DigiMicGain = optionalData.DigiMicGain;
            optionalSettings.EnhancedSoundQuality = optionalData.EnhancedSoundQuality;
            optionalSettings.AnalogMicGain = optionalData.AnalogMicGain;
            optionalSettings.RxAgc = optionalData.RxAgc;
            optionalSettings.NxMicGain = optionalData.NxMicGain;

            optionalSettings.DisplayMode = optionalData.DisplayMode;
            optionalSettings.VfMrA = optionalData.VfMrA;
            optionalSettings.VfMrB = optionalData.VfMrB;
            optionalSettings.MemZoneA = optionalData.MemZoneA;
            optionalSettings.MemZoneB = optionalData.MemZoneB;
            optionalSettings.MainChannelSet = optionalData.MainChannelSet;
            optionalSettings.SubChannelMode = optionalData.SubChannelMode;
            optionalSettings.WorkingMode = optionalData.WorkingMode;

            optionalSettings.VoxLevel = optionalData.VoxLevel;
            optionalSettings.VoxDelay = optionalData.VoxDelay;
            optionalSettings.VoxDetection = optionalData.VoxDetection;
            optionalSettings.BtOnOff = optionalData.BtOnOff;
            optionalSettings.BtIntMic = optionalData.BtIntMic;
            optionalSettings.BtIntSpk = optionalData.BtIntSpk;
            optionalSettings.BtMicGain = optionalData.BtMicGain;
            optionalSettings.BtSpkGain = optionalData.BtSpkGain;
            optionalSettings.BtHoldTime = optionalData.BtHoldTime;
            optionalSettings.BtRxDelay = optionalData.BtRxDelay;
            optionalSettings.BtPttHold = optionalData.BtPttHold;
            optionalSettings.BtPttSleepTime = optionalData.BtPttSleepTime;
            optionalSettings.BtNrBefore = optionalData.BtNrBefore;
            optionalSettings.BtNrAfter = optionalData.BtNrAfter;

            optionalSettings.SteTypeOfCtcss = optionalData.SteTypeOfCtcss;
            optionalSettings.SteWhenNoSignal = optionalData.SteWhenNoSignal;
            optionalSettings.SteTime = optionalData.SteTime;

            optionalSettings.AmFmFunction = optionalData.AmFmFunction;
            optionalSettings.FmVfoMem = optionalData.FmVfoMem;
            optionalSettings.FmWorkChannel = optionalData.FmWorkChannel;
            optionalSettings.FmMonitor = optionalData.FmMonitor;
            optionalSettings.AmVfoMem = optionalData.AmVfoMem;
            optionalSettings.AmWorkZone = optionalData.AmWorkZone;
            optionalSettings.AmOffset = optionalData.AmOffset;
            optionalSettings.AmSqlLevel = optionalData.AmSqlLevel;

            optionalSettings.AutoShutdown = optionalData.AutoShutdown;
            optionalSettings.PowerSave = optionalData.PowerSave;
            optionalSettings.AutoShutdownType = optionalData.AutoShutdownType;

            optionalSettings.AddressBookSentWithCode = optionalData.AddressBookSentWithCode;
            optionalSettings.Tot = optionalData.Tot;
            optionalSettings.Language = optionalData.Language;
            optionalSettings.FrequencyStep = optionalData.FrequencyStep;
            optionalSettings.SqlLevelA = optionalData.SqlLevelA;
            optionalSettings.SqlLevelB = optionalData.SqlLevelB;
            optionalSettings.Tbst = optionalData.Tbst;
            optionalSettings.AnalogCallHoldTime = optionalData.AnalogCallHoldTime;
            optionalSettings.CallChannelMaintained = optionalData.CallChannelMaintained;
            optionalSettings.PriorityZoneA = optionalData.PriorityZoneA;
            optionalSettings.PriorityZoneB = optionalData.PriorityZoneB;
            optionalSettings.MuteTiming = optionalData.MuteTiming;
            optionalSettings.EncryptionType = optionalData.EncryptionType;
            optionalSettings.TotPredict = optionalData.TotPredict;
            optionalSettings.TxPowerAgc = optionalData.TxPowerAgc;
            optionalSettings.NoaaMoni = optionalData.NoaaMoni;
            optionalSettings.NoaaScan = optionalData.NoaaScan;
            optionalSettings.Noaa = optionalData.Noaa;
            optionalSettings.NoaaChannel = optionalData.NoaaChannel;

            optionalSettings.GroupCallHoldTime = optionalData.GroupCallHoldTime;
            optionalSettings.PrivateCallHoldTime = optionalData.PrivateCallHoldTime;
            optionalSettings.ManualDialGroupCallHoldTime = optionalData.ManualDialGroupCallHoldTime;
            optionalSettings.ManualDialPrivateCallHoldTime = optionalData.ManualDialPrivateCallHoldTime;
            optionalSettings.VoiceHeaderRepetitions = optionalData.VoiceHeaderRepetitions;
            optionalSettings.TxPreambleDuration = optionalData.TxPreambleDuration;
            optionalSettings.FilterOwnId = optionalData.FilterOwnId;
            optionalSettings.DigitalRemoteKill = optionalData.DigitalRemoteKill;
            optionalSettings.DigitalMonitor = optionalData.DigitalMonitor;
            optionalSettings.DigitalMonitorCc = optionalData.DigitalMonitorCc;
            optionalSettings.DigitalMonitorId = optionalData.DigitalMonitorId;
            optionalSettings.MonitorSlotHold = optionalData.MonitorSlotHold;
            optionalSettings.RemoteMonitor = optionalData.RemoteMonitor;
            optionalSettings.SmsFormat = optionalData.SmsFormat;
            optionalSettings.ResetDigitalProtocol = optionalData.ResetDigitalProtocol;

            optionalSettings.SatLocation = optionalData.SatLocation;
            optionalSettings.SatTxPower = optionalData.SatTxPower;
            optionalSettings.SatAnaSql = optionalData.SatAnaSql;
            optionalSettings.SatAosLimit = optionalData.SatAosLimit;

            foreach (var toneData in optionalData.AlertTones)
            {
                var existing = optionalSettings.AlertTones.FirstOrDefault(t => t.Category == toneData.Category && t.ToneNumber == toneData.ToneNumber);
                if (existing is null)
                {
                    continue;
                }

                existing.Frequency = toneData.Frequency;
                existing.Period = toneData.Period;
            }
        }

        channels.Clear();
        zones.Clear();
        encryptionKeys?.Clear();
        arc4EncryptionKeys?.Clear();
        aesEncryptionKeys?.Clear();

        var channelMap = new Dictionary<int, ChannelEntry>();
        foreach (var channelData in data.Channels.OrderBy(channel => channel.Number))
        {
            var channel = ToEntry(channelData);
            channels.Add(channel);
            channelMap[channel.Number] = channel;
        }

        foreach (var zoneData in data.Zones.OrderBy(zone => zone.Number))
        {
            var zone = new ZoneEntry
            {
                Number = zoneData.Number,
                Name = zoneData.Name,
                IsHidden = zoneData.IsHidden
            };

            foreach (var channelNumber in zoneData.MemberChannelNumbers)
            {
                if (channelMap.TryGetValue(channelNumber, out var channel))
                {
                    zone.Members.Add(channel);
                }
            }

            zone.AChannel = zoneData.AChannelNumber is { } aNumber && channelMap.TryGetValue(aNumber, out var aChannel)
                ? aChannel
                : zone.Members.FirstOrDefault();

            zone.BChannel = zoneData.BChannelNumber is { } bNumber && channelMap.TryGetValue(bNumber, out var bChannel)
                ? bChannel
                : zone.Members.Skip(1).FirstOrDefault() ?? zone.Members.FirstOrDefault();

            zones.Add(zone);
        }

        LoadKeys(data.EncryptionKeys, encryptionKeys, EncryptionKeyKind.Basic);
        LoadKeys(data.Arc4EncryptionKeys, arc4EncryptionKeys, EncryptionKeyKind.Arc4);
        LoadKeys(data.AesEncryptionKeys, aesEncryptionKeys, EncryptionKeyKind.Aes);

        LoadSimple(data.RadioIds, radioIds, entry => entry.Number, ToEntry);
        LoadSimple(data.Talkgroups, talkgroups, entry => entry.Number, ToEntry);

        if (scanLists is not null)
        {
            scanLists.Clear();
            foreach (var scanListData in data.ScanLists.OrderBy(scanList => scanList.Number))
            {
                scanLists.Add(ToEntry(scanListData, channelMap));
            }
        }

        LoadSimple(data.RoamingChannels, roamingChannels, entry => entry.Number, ToEntry);

        if (roamingZones is not null)
        {
            // Indexer assignment (not ToDictionary), same tolerance as
            // channelMap above - a malformed project file with duplicate
            // Roaming Channel numbers must not crash loading, just resolve
            // to whichever one wins.
            var roamingChannelMap = new Dictionary<int, RoamingChannelEntry>();
            foreach (var roamingChannel in roamingChannels ?? [])
            {
                roamingChannelMap[roamingChannel.Number - 1] = roamingChannel;
            }

            roamingZones.Clear();
            foreach (var roamingZoneData in data.RoamingZones.OrderBy(z => z.Number))
            {
                roamingZones.Add(ToEntry(roamingZoneData, roamingChannelMap));
            }
        }

        LoadSimple(data.ReceiveGroupLists, receiveGroupLists, entry => entry.Number, ToEntry);
        LoadSimple(data.AutoRepeaterOffsets, autoRepeaterOffsets, entry => entry.Number, ToEntry);
        LoadSimple(data.AnalogAddresses, analogAddresses, entry => entry.Number, ToEntry);
        LoadSimple(data.GpsRoamingEntries, gpsRoamingEntries, entry => entry.Number, ToEntry);
        // ZoneDisplayName isn't itself persisted - resolved fresh against
        // the just-loaded zones so a project saved before a zone was
        // renamed doesn't show a stale name (same reasoning as the live
        // Read path's own RadioReadMapper.ResolveZoneName).
        if (gpsRoamingEntries is not null)
        {
            foreach (var gpsRoaming in gpsRoamingEntries)
            {
                var match = zones.FirstOrDefault(z => z.Number - 1 == gpsRoaming.ZoneIndex);
                gpsRoaming.ZoneDisplayName = gpsRoaming.ZoneIndex == 255 ? "Off" : match?.Name ?? $"Zone idx {gpsRoaming.ZoneIndex}";
            }
        }
        LoadSimple(data.TalkgroupWhitelist, talkgroupWhitelist, entry => entry.Number, ToEntry);
        LoadSimple(data.PrefabricatedSms, prefabricatedSms, entry => entry.Number, ToEntry);
        // amAirMap is always built (regardless of whether the caller wants
        // AmAirChannels populated) since AM Zone needs it to resolve
        // Members/AChannel back to real AmAirEntry objects - same pattern
        // as channelMap above for Zone/ScanList.
        amAirChannels?.Clear();
        var amAirMap = new Dictionary<int, AmAirEntry>();
        foreach (var amAirData in data.AmAirChannels.OrderBy(a => a.Number))
        {
            var amAir = ToEntry(amAirData);
            amAirMap[amAir.Number] = amAir;
            amAirChannels?.Add(amAir);
        }

        if (amZones is not null)
        {
            amZones.Clear();
            foreach (var amZoneData in data.AmZones.OrderBy(z => z.Number))
            {
                var amZone = new AmZoneEntry { Number = amZoneData.Number, Name = amZoneData.Name };

                foreach (var channelNumber in amZoneData.MemberChannelNumbers)
                {
                    if (amAirMap.TryGetValue(channelNumber, out var channel))
                    {
                        amZone.Members.Add(channel);
                    }
                }

                amZone.AChannel = amZoneData.AChannelNumber is { } aNumber && amAirMap.TryGetValue(aNumber, out var aChannel)
                    ? aChannel
                    : amZone.Members.FirstOrDefault();

                foreach (var channelNumber in amZoneData.ScanChannelMemberNumbers)
                {
                    if (amAirMap.TryGetValue(channelNumber, out var channel))
                    {
                        amZone.ScanChannelMembers.Add(channel);
                    }
                }

                amZones.Add(amZone);
            }
        }
        LoadSimple(data.FmChannels, fmChannels, entry => entry.Number, ToEntry);
        LoadSimple(data.DigitalContactWhitelist, digitalContactWhitelist, entry => entry.Number, ToEntry);
        LoadSimple(data.AprsReceiveFilters, aprsReceiveFilters, entry => entry.Number, ToEntry);
        LoadSimple(data.DigitalContacts, digitalContacts, entry => entry.Index, ToEntry);
    }

    private static void LoadSimple<TData, TEntry>(
        IEnumerable<TData> source,
        ICollection<TEntry>? target,
        Func<TData, int> numberSelector,
        Func<TData, TEntry> toEntry)
        where TData : class
    {
        if (target is null)
        {
            return;
        }

        target.Clear();
        foreach (var entryData in source.OrderBy(numberSelector))
        {
            target.Add(toEntry(entryData));
        }
    }

    private static void LoadKeys(IEnumerable<EncryptionKeyData> source, ICollection<EncryptionKeyEntry>? target, EncryptionKeyKind kind)
    {
        if (target is null)
        {
            return;
        }

        foreach (var keyData in source.OrderBy(key => key.Number))
        {
            target.Add(ToEntry(keyData, kind));
        }
    }

    private static ChannelData ToData(ChannelEntry channel)
    {
        return new ChannelData
        {
            Number = channel.Number,
            Name = channel.Name,
            RxFrequencyMHz = channel.RxFrequencyMHz,
            OffsetMHz = channel.OffsetMHz,
            OffsetDirection = channel.OffsetDirection,
            ChannelType = channel.ChannelType,
            TransmitPower = channel.TransmitPower,
            Bandwidth = channel.Bandwidth,
            CtcssDcsDecode = channel.CtcssDcsDecode,
            CtcssDcsEncode = channel.CtcssDcsEncode,
            ColorCode = channel.ColorCode,
            TxColorCode = channel.TxColorCode,
            RepeaterSlot2 = channel.RepeaterSlot2,
            ContactIndex = channel.ContactIndex,
            RadioIdIndex = channel.RadioIdIndex,
            BusyLock = channel.BusyLock,
            SquelchMode = channel.SquelchMode,
            OptionalSignal = channel.OptionalSignal,
            PttId = channel.PttId,
            ScanListIndex = channel.ScanListIndex,
            ReceiveGroupListIndex = channel.ReceiveGroupListIndex,
            PttProhibit = channel.PttProhibit,
            Reverse = channel.Reverse,
            SlotSuit = channel.SlotSuit,
            AesEncryptionIndex = channel.AesEncryptionIndex,
            CallConfirmation = channel.CallConfirmation,
            TalkAround = channel.TalkAround,
            WorkAlone = channel.WorkAlone,
            CustomCtcss = channel.CustomCtcss,
            CtcssEncodeTone = channel.CtcssEncodeTone,
            CtcssDecodeTone = channel.CtcssDecodeTone,
            DcsEncodeTone = channel.DcsEncodeTone,
            DcsDecodeTone = channel.DcsDecodeTone,
            AutoScan = channel.AutoScan,
            SmsConfirmation = channel.SmsConfirmation,
            CorrectFrequencyHz = channel.CorrectFrequencyHz,
            DmrModeDcdm = channel.DmrModeDcdm,
            DmrMode = channel.DmrMode,
            ScrambleMode = channel.ScrambleMode,
            CustomScrambleFrequencyIndex = channel.CustomScrambleFrequencyIndex,
            Arc4EncryptionKeyIndex = channel.Arc4EncryptionKeyIndex,
            DigitalEncryptionIndex = channel.DigitalEncryptionIndex,
            DmrCrcIgnore = channel.DmrCrcIgnore,
            SendTalkerAlias = channel.SendTalkerAlias,
            SmsForbid = channel.SmsForbid,
            DataAckDisable = channel.DataAckDisable,
            ExcludeChannelRoaming = channel.ExcludeChannelRoaming,
            AesRandomKey = channel.AesRandomKey,
            AesMultipleKey = channel.AesMultipleKey,
            AprsRx = channel.AprsRx,
            DtmfIdIndex = channel.DtmfIdIndex,
            Tone2IdIndex = channel.Tone2IdIndex,
            Tone5IdIndex = channel.Tone5IdIndex,
            Tone2Decode = channel.Tone2Decode,
            R5ToneBot = channel.R5ToneBot,
            R5ToneEot = channel.R5ToneEot,
            QdcIdIndex = channel.QdcIdIndex,
            ExtendEncryption = channel.ExtendEncryption,
            TxInterrupt = channel.TxInterrupt,
            IdleTx = channel.IdleTx,
            Ranging = channel.Ranging,
            ContactDisplayName = channel.ContactDisplayName,
            RadioIdDisplayName = channel.RadioIdDisplayName,
            ReceiveGroupListDisplayName = channel.ReceiveGroupListDisplayName
        };
    }

    private static ZoneData ToData(ZoneEntry zone)
    {
        return new ZoneData
        {
            Number = zone.Number,
            Name = zone.Name,
            MemberChannelNumbers = zone.Members.Select(channel => channel.Number).ToList(),
            AChannelNumber = zone.AChannel?.Number,
            BChannelNumber = zone.BChannel?.Number,
            IsHidden = zone.IsHidden
        };
    }

    private static EncryptionKeyData ToData(EncryptionKeyEntry key)
    {
        return new EncryptionKeyData
        {
            Number = key.Number,
            EncryptionKey = key.EncryptionKey,
            EncryptionId = key.EncryptionId
        };
    }

    private static ChannelEntry ToEntry(ChannelData data)
    {
        return new ChannelEntry
        {
            Number = data.Number,
            Name = data.Name,
            RxFrequencyMHz = data.RxFrequencyMHz,
            OffsetMHz = data.OffsetMHz,
            OffsetDirection = data.OffsetDirection,
            ChannelType = data.ChannelType,
            TransmitPower = data.TransmitPower,
            Bandwidth = data.Bandwidth,
            CtcssDcsDecode = data.CtcssDcsDecode,
            CtcssDcsEncode = data.CtcssDcsEncode,
            ColorCode = data.ColorCode,
            TxColorCode = data.TxColorCode,
            RepeaterSlot2 = data.RepeaterSlot2,
            ContactIndex = data.ContactIndex,
            RadioIdIndex = data.RadioIdIndex,
            BusyLock = data.BusyLock,
            SquelchMode = data.SquelchMode,
            OptionalSignal = data.OptionalSignal,
            PttId = data.PttId,
            ScanListIndex = data.ScanListIndex,
            ReceiveGroupListIndex = data.ReceiveGroupListIndex,
            PttProhibit = data.PttProhibit,
            Reverse = data.Reverse,
            SlotSuit = data.SlotSuit,
            AesEncryptionIndex = data.AesEncryptionIndex,
            CallConfirmation = data.CallConfirmation,
            TalkAround = data.TalkAround,
            WorkAlone = data.WorkAlone,
            CustomCtcss = data.CustomCtcss,
            CtcssEncodeTone = data.CtcssEncodeTone,
            CtcssDecodeTone = data.CtcssDecodeTone,
            DcsEncodeTone = data.DcsEncodeTone,
            DcsDecodeTone = data.DcsDecodeTone,
            AutoScan = data.AutoScan,
            SmsConfirmation = data.SmsConfirmation,
            CorrectFrequencyHz = data.CorrectFrequencyHz,
            DmrModeDcdm = data.DmrModeDcdm,
            DmrMode = data.DmrMode,
            ScrambleMode = data.ScrambleMode,
            CustomScrambleFrequencyIndex = data.CustomScrambleFrequencyIndex,
            Arc4EncryptionKeyIndex = data.Arc4EncryptionKeyIndex,
            DigitalEncryptionIndex = data.DigitalEncryptionIndex,
            DmrCrcIgnore = data.DmrCrcIgnore,
            SendTalkerAlias = data.SendTalkerAlias,
            SmsForbid = data.SmsForbid,
            DataAckDisable = data.DataAckDisable,
            ExcludeChannelRoaming = data.ExcludeChannelRoaming,
            AesRandomKey = data.AesRandomKey,
            AesMultipleKey = data.AesMultipleKey,
            AprsRx = data.AprsRx,
            DtmfIdIndex = data.DtmfIdIndex,
            Tone2IdIndex = data.Tone2IdIndex,
            Tone5IdIndex = data.Tone5IdIndex,
            Tone2Decode = data.Tone2Decode,
            R5ToneBot = data.R5ToneBot,
            R5ToneEot = data.R5ToneEot,
            QdcIdIndex = data.QdcIdIndex,
            ExtendEncryption = data.ExtendEncryption,
            TxInterrupt = data.TxInterrupt,
            IdleTx = data.IdleTx,
            Ranging = data.Ranging,
            ContactDisplayName = data.ContactDisplayName,
            RadioIdDisplayName = data.RadioIdDisplayName,
            ReceiveGroupListDisplayName = data.ReceiveGroupListDisplayName
        };
    }

    private static EncryptionKeyEntry ToEntry(EncryptionKeyData data, EncryptionKeyKind kind)
    {
        return new EncryptionKeyEntry
        {
            Kind = kind,
            Number = data.Number,
            EncryptionKeyText = data.EncryptionKey,
            EncryptionIdText = data.EncryptionId
        };
    }

    private static RadioIdData ToData(RadioIdEntry entry)
    {
        return new RadioIdData
        {
            Number = entry.Number,
            DmrId = entry.DmrId,
            Name = entry.Name
        };
    }

    private static RadioIdEntry ToEntry(RadioIdData data)
    {
        return new RadioIdEntry
        {
            Number = data.Number,
            DmrId = data.DmrId,
            Name = data.Name
        };
    }

    private static TalkgroupData ToData(TalkgroupEntry entry)
    {
        return new TalkgroupData
        {
            Number = entry.Number,
            DmrId = entry.DmrId,
            Name = entry.Name,
            CallType = entry.CallType,
            CallAlert = entry.CallAlert
        };
    }

    private static TalkgroupEntry ToEntry(TalkgroupData data)
    {
        return new TalkgroupEntry
        {
            Number = data.Number,
            DmrId = data.DmrId,
            Name = data.Name,
            CallType = data.CallType,
            CallAlert = data.CallAlert
        };
    }

    private static ScanListData ToData(ScanListEntry entry)
    {
        return new ScanListData
        {
            Number = entry.Number,
            Name = entry.Name,
            PriorityChannelSelect = entry.PriorityChannelSelect,
            PriorityChannel1Number = entry.PriorityChannel1?.Number,
            PriorityChannel2Number = entry.PriorityChannel2?.Number,
            LookbackTimeA = entry.LookbackTimeA,
            LookbackTimeB = entry.LookbackTimeB,
            DropoutDelayTime = entry.DropoutDelayTime,
            DwellTime = entry.DwellTime,
            RevertChannel = entry.RevertChannel,
            MemberChannelNumbers = entry.Members.Select(channel => channel.Number).ToList()
        };
    }

    /// <summary>Unlike the other simple entity ToEntry methods, this needs
    /// <paramref name="channelMap"/> to resolve Members/PriorityChannel1/2
    /// back to real ChannelEntry objects - see ZoneData's equivalent
    /// resolution in <see cref="LoadInto"/> for the same pattern.</summary>
    private static ScanListEntry ToEntry(ScanListData data, IReadOnlyDictionary<int, ChannelEntry> channelMap)
    {
        var entry = new ScanListEntry
        {
            Number = data.Number,
            Name = data.Name,
            PriorityChannelSelect = data.PriorityChannelSelect,
            LookbackTimeA = data.LookbackTimeA,
            LookbackTimeB = data.LookbackTimeB,
            DropoutDelayTime = data.DropoutDelayTime,
            DwellTime = data.DwellTime,
            RevertChannel = data.RevertChannel
        };

        foreach (var channelNumber in data.MemberChannelNumbers)
        {
            if (channelMap.TryGetValue(channelNumber, out var channel))
            {
                entry.Members.Add(channel);
            }
        }

        entry.PriorityChannel1 = data.PriorityChannel1Number is { } p1Number ? channelMap.GetValueOrDefault(p1Number) : null;
        entry.PriorityChannel2 = data.PriorityChannel2Number is { } p2Number ? channelMap.GetValueOrDefault(p2Number) : null;

        return entry;
    }

    private static RoamingChannelData ToData(RoamingChannelEntry entry)
    {
        return new RoamingChannelData
        {
            Number = entry.Number,
            RxFrequencyMhz = entry.RxFrequencyMhz,
            TxFrequencyMhz = entry.TxFrequencyMhz,
            ColorCode = entry.ColorCode,
            Slot = entry.Slot,
            Name = entry.Name
        };
    }

    private static RoamingChannelEntry ToEntry(RoamingChannelData data)
    {
        return new RoamingChannelEntry
        {
            Number = data.Number,
            RxFrequencyMhz = data.RxFrequencyMhz,
            TxFrequencyMhz = data.TxFrequencyMhz,
            ColorCode = data.ColorCode,
            Slot = data.Slot,
            Name = data.Name
        };
    }

    /// <summary>RoamingChannelIndexes stays 0-based radio indices on disk
    /// (unlike ZoneData's Number-based MemberChannelNumbers) - deliberately
    /// unchanged from before RoamingZoneEntry.Members existed, so old saved
    /// project files keep loading correctly.</summary>
    private static RoamingZoneData ToData(RoamingZoneEntry entry)
    {
        return new RoamingZoneData
        {
            Number = entry.Number,
            Name = entry.Name,
            RoamingChannelIndexes = entry.Members.Select(m => m.Number - 1).ToList()
        };
    }

    /// <summary>Members are resolved via <paramref name="roamingChannelsByRadioIndex"/>
    /// (0-based) - see this class's own ToData(RoamingZoneEntry) doc comment.</summary>
    private static RoamingZoneEntry ToEntry(RoamingZoneData data, IReadOnlyDictionary<int, RoamingChannelEntry> roamingChannelsByRadioIndex)
    {
        var entry = new RoamingZoneEntry
        {
            Number = data.Number,
            Name = data.Name
        };

        foreach (var channelIndex in data.RoamingChannelIndexes)
        {
            if (roamingChannelsByRadioIndex.TryGetValue(channelIndex, out var roamingChannel))
            {
                entry.Members.Add(roamingChannel);
            }
        }

        return entry;
    }

    private static ReceiveGroupListData ToData(ReceiveGroupListEntry entry)
    {
        return new ReceiveGroupListData
        {
            Number = entry.Number,
            Name = entry.Name,
            TalkgroupIndexes = entry.TalkgroupIndexes.ToList()
        };
    }

    private static ReceiveGroupListEntry ToEntry(ReceiveGroupListData data)
    {
        var entry = new ReceiveGroupListEntry
        {
            Number = data.Number,
            Name = data.Name
        };

        foreach (var talkgroupIndex in data.TalkgroupIndexes)
        {
            entry.TalkgroupIndexes.Add(talkgroupIndex);
        }

        return entry;
    }

    private static AutoRepeaterOffsetData ToData(AutoRepeaterOffsetEntry entry)
    {
        return new AutoRepeaterOffsetData
        {
            Number = entry.Number,
            OffsetFrequencyMhz = entry.OffsetFrequencyMhz,
            RawOffset = entry.RawOffset
        };
    }

    private static AutoRepeaterOffsetEntry ToEntry(AutoRepeaterOffsetData data)
    {
        return new AutoRepeaterOffsetEntry
        {
            Number = data.Number,
            OffsetFrequencyMhz = data.OffsetFrequencyMhz,
            RawOffset = data.RawOffset
        };
    }

    private static MasterIdData ToData(MasterIdEntry entry)
    {
        return new MasterIdData
        {
            DmrId = entry.DmrId,
            Used = entry.Used,
            Name = entry.Name
        };
    }

    private static TalkAliasSettingsData ToData(TalkAliasSettingsEntry entry)
    {
        return new TalkAliasSettingsData
        {
            DisplayPriority = entry.DisplayPriority,
            DataFormat = entry.DataFormat
        };
    }

    private static OptionalSettingsData ToData(OptionalSettingsEntry entry)
    {
        return new OptionalSettingsData
        {
            PowerOnInterface = entry.PowerOnInterface,
            PowerOnDisplayLine1 = entry.PowerOnDisplayLine1,
            PowerOnDisplayLine2 = entry.PowerOnDisplayLine2,
            PowerOnPassword = entry.PowerOnPassword,
            PowerOnPasswordChar = entry.PowerOnPasswordChar,
            DefaultStartupChannel = entry.DefaultStartupChannel,
            StartupZoneA = entry.StartupZoneA,
            StartupChannelA = entry.StartupChannelA,
            StartupZoneB = entry.StartupZoneB,
            StartupChannelB = entry.StartupChannelB,
            StartupGpsTest = entry.StartupGpsTest,
            StartupReset = entry.StartupReset,

            Brightness = entry.Brightness,
            AutoBacklightDuration = entry.AutoBacklightDuration,
            BacklightTxDelay = entry.BacklightTxDelay,
            MenuExitTime = entry.MenuExitTime,
            TimeDisplay = entry.TimeDisplay,
            LastCaller = entry.LastCaller,
            CallDisplayMode = entry.CallDisplayMode,
            CallsignDisplayColor = entry.CallsignDisplayColor,
            CallEndPromptBox = entry.CallEndPromptBox,
            DisplayChannelNumber = entry.DisplayChannelNumber,
            DisplayCurrentContact = entry.DisplayCurrentContact,
            StandbyCharColor = entry.StandbyCharColor,
            StandbyBkPicture = entry.StandbyBkPicture,
            ShowLastCallOnLaunch = entry.ShowLastCallOnLaunch,
            SeparateDisplay = entry.SeparateDisplay,
            ChSwitchingKeepsCaller = entry.ChSwitchingKeepsCaller,
            BacklightRxDelay = entry.BacklightRxDelay,
            ChannelNameColorA = entry.ChannelNameColorA,
            ChannelNameColorB = entry.ChannelNameColorB,
            ZoneNameColorA = entry.ZoneNameColorA,
            ZoneNameColorB = entry.ZoneNameColorB,
            DisplayChannelType = entry.DisplayChannelType,
            DisplayTimeSlot = entry.DisplayTimeSlot,
            DisplayColorCode = entry.DisplayColorCode,
            DateDisplayFormat = entry.DateDisplayFormat,
            VolumeBar = entry.VolumeBar,

            KeyLock = entry.KeyLock,
            Pf1ShortKey = entry.Pf1ShortKey,
            Pf2ShortKey = entry.Pf2ShortKey,
            Pf3ShortKey = entry.Pf3ShortKey,
            P1ShortKey = entry.P1ShortKey,
            P2ShortKey = entry.P2ShortKey,
            Pf1LongKey = entry.Pf1LongKey,
            Pf2LongKey = entry.Pf2LongKey,
            Pf3LongKey = entry.Pf3LongKey,
            P1LongKey = entry.P1LongKey,
            P2LongKey = entry.P2LongKey,
            LongKeyTime = entry.LongKeyTime,
            KnobLock = entry.KnobLock,
            KeyboardLock = entry.KeyboardLock,
            SideKeyLock = entry.SideKeyLock,
            ForcedKeyLock = entry.ForcedKeyLock,

            SmsAlert = entry.SmsAlert,
            CallAlert = entry.CallAlert,
            DigiCallResetTone = entry.DigiCallResetTone,
            TalkPermit = entry.TalkPermit,
            KeyTone = entry.KeyTone,
            DigiIdleChannelTone = entry.DigiIdleChannelTone,
            StartupSound = entry.StartupSound,
            ToneKeySoundAdjustable = entry.ToneKeySoundAdjustable,
            AnalogIdleChannelTone = entry.AnalogIdleChannelTone,
            PluginRecordingTone = entry.PluginRecordingTone,

            GpsPower = entry.GpsPower,
            GpsPositioning = entry.GpsPositioning,
            TimeZone = entry.TimeZone,
            RangingInterval = entry.RangingInterval,
            DistanceUnit = entry.DistanceUnit,
            GpsTemplateInformation = entry.GpsTemplateInformation,
            GpsInformationChar = entry.GpsInformationChar,
            GpsMode = entry.GpsMode,
            GpsRoaming = entry.GpsRoaming,

            VfoScanType = entry.VfoScanType,
            VfoScanStartFreqUhf = entry.VfoScanStartFreqUhf,
            VfoScanEndFreqUhf = entry.VfoScanEndFreqUhf,
            VfoScanStartFreqVhf = entry.VfoScanStartFreqVhf,
            VfoScanEndFreqVhf = entry.VfoScanEndFreqVhf,

            AutoRepeaterA = entry.AutoRepeaterA,
            AutoRepeaterB = entry.AutoRepeaterB,
            AutoRepeater1Uhf = entry.AutoRepeater1Uhf,
            AutoRepeater1Vhf = entry.AutoRepeater1Vhf,
            AutoRepeater2Uhf = entry.AutoRepeater2Uhf,
            AutoRepeater2Vhf = entry.AutoRepeater2Vhf,
            RepeaterCheck = entry.RepeaterCheck,
            RepeaterCheckInterval = entry.RepeaterCheckInterval,
            RepeaterCheckReconnections = entry.RepeaterCheckReconnections,
            RepeaterOutOfRangeNotify = entry.RepeaterOutOfRangeNotify,
            OutOfRangeNotify = entry.OutOfRangeNotify,
            AutoRoaming = entry.AutoRoaming,
            AutoRoamingStartCondition = entry.AutoRoamingStartCondition,
            AutoRoamingFixedTime = entry.AutoRoamingFixedTime,
            RoamingEffectWaitTime = entry.RoamingEffectWaitTime,
            RoamingZone = entry.RoamingZone,
            AutoRepeater1MinFreqVhf = entry.AutoRepeater1MinFreqVhf,
            AutoRepeater1MaxFreqVhf = entry.AutoRepeater1MaxFreqVhf,
            AutoRepeater1MinFreqUhf = entry.AutoRepeater1MinFreqUhf,
            AutoRepeater1MaxFreqUhf = entry.AutoRepeater1MaxFreqUhf,
            AutoRepeater2MinFreqVhf = entry.AutoRepeater2MinFreqVhf,
            AutoRepeater2MaxFreqVhf = entry.AutoRepeater2MaxFreqVhf,
            AutoRepeater2MinFreqUhf = entry.AutoRepeater2MinFreqUhf,
            AutoRepeater2MaxFreqUhf = entry.AutoRepeater2MaxFreqUhf,
            RepeaterMode = entry.RepeaterMode,
            RepCcLimit = entry.RepCcLimit,
            RepSlotA = entry.RepSlotA,
            RepSlotB = entry.RepSlotB,

            RecordFunction = entry.RecordFunction,
            RecordDelay = entry.RecordDelay,

            MaxVolume = entry.MaxVolume,
            PowerOnVolumeType = entry.PowerOnVolumeType,
            PowerOnVolume = entry.PowerOnVolume,
            MaxHeadphoneVolume = entry.MaxHeadphoneVolume,
            DigiMicGain = entry.DigiMicGain,
            EnhancedSoundQuality = entry.EnhancedSoundQuality,
            AnalogMicGain = entry.AnalogMicGain,
            RxAgc = entry.RxAgc,
            NxMicGain = entry.NxMicGain,

            DisplayMode = entry.DisplayMode,
            VfMrA = entry.VfMrA,
            VfMrB = entry.VfMrB,
            MemZoneA = entry.MemZoneA,
            MemZoneB = entry.MemZoneB,
            MainChannelSet = entry.MainChannelSet,
            SubChannelMode = entry.SubChannelMode,
            WorkingMode = entry.WorkingMode,

            VoxLevel = entry.VoxLevel,
            VoxDelay = entry.VoxDelay,
            VoxDetection = entry.VoxDetection,
            BtOnOff = entry.BtOnOff,
            BtIntMic = entry.BtIntMic,
            BtIntSpk = entry.BtIntSpk,
            BtMicGain = entry.BtMicGain,
            BtSpkGain = entry.BtSpkGain,
            BtHoldTime = entry.BtHoldTime,
            BtRxDelay = entry.BtRxDelay,
            BtPttHold = entry.BtPttHold,
            BtPttSleepTime = entry.BtPttSleepTime,
            BtNrBefore = entry.BtNrBefore,
            BtNrAfter = entry.BtNrAfter,

            SteTypeOfCtcss = entry.SteTypeOfCtcss,
            SteWhenNoSignal = entry.SteWhenNoSignal,
            SteTime = entry.SteTime,

            AmFmFunction = entry.AmFmFunction,
            FmVfoMem = entry.FmVfoMem,
            FmWorkChannel = entry.FmWorkChannel,
            FmMonitor = entry.FmMonitor,
            AmVfoMem = entry.AmVfoMem,
            AmWorkZone = entry.AmWorkZone,
            AmOffset = entry.AmOffset,
            AmSqlLevel = entry.AmSqlLevel,

            AutoShutdown = entry.AutoShutdown,
            PowerSave = entry.PowerSave,
            AutoShutdownType = entry.AutoShutdownType,

            AddressBookSentWithCode = entry.AddressBookSentWithCode,
            Tot = entry.Tot,
            Language = entry.Language,
            FrequencyStep = entry.FrequencyStep,
            SqlLevelA = entry.SqlLevelA,
            SqlLevelB = entry.SqlLevelB,
            Tbst = entry.Tbst,
            AnalogCallHoldTime = entry.AnalogCallHoldTime,
            CallChannelMaintained = entry.CallChannelMaintained,
            PriorityZoneA = entry.PriorityZoneA,
            PriorityZoneB = entry.PriorityZoneB,
            MuteTiming = entry.MuteTiming,
            EncryptionType = entry.EncryptionType,
            TotPredict = entry.TotPredict,
            TxPowerAgc = entry.TxPowerAgc,
            NoaaMoni = entry.NoaaMoni,
            NoaaScan = entry.NoaaScan,
            Noaa = entry.Noaa,
            NoaaChannel = entry.NoaaChannel,

            GroupCallHoldTime = entry.GroupCallHoldTime,
            PrivateCallHoldTime = entry.PrivateCallHoldTime,
            ManualDialGroupCallHoldTime = entry.ManualDialGroupCallHoldTime,
            ManualDialPrivateCallHoldTime = entry.ManualDialPrivateCallHoldTime,
            VoiceHeaderRepetitions = entry.VoiceHeaderRepetitions,
            TxPreambleDuration = entry.TxPreambleDuration,
            FilterOwnId = entry.FilterOwnId,
            DigitalRemoteKill = entry.DigitalRemoteKill,
            DigitalMonitor = entry.DigitalMonitor,
            DigitalMonitorCc = entry.DigitalMonitorCc,
            DigitalMonitorId = entry.DigitalMonitorId,
            MonitorSlotHold = entry.MonitorSlotHold,
            RemoteMonitor = entry.RemoteMonitor,
            SmsFormat = entry.SmsFormat,
            ResetDigitalProtocol = entry.ResetDigitalProtocol,

            SatLocation = entry.SatLocation,
            SatTxPower = entry.SatTxPower,
            SatAnaSql = entry.SatAnaSql,
            SatAosLimit = entry.SatAosLimit,

            AlertTones = entry.AlertTones.Select(t => new AlertToneData
            {
                Category = t.Category,
                ToneNumber = t.ToneNumber,
                Frequency = t.Frequency,
                Period = t.Period
            }).ToList()
        };
    }

    private static AprsSettingsData ToData(AprsSettingsEntry entry)
    {
        return new AprsSettingsData
        {
            TxFreq1Mhz = entry.TxFreq1Mhz,
            TxDelay = entry.TxDelay,
            SendSubtone = entry.SendSubtone,
            Ctcss = entry.Ctcss,
            Dcs = entry.Dcs,
            ManualTxInterval = entry.ManualTxInterval,
            AutoTxInterval = entry.AutoTxInterval,
            TxTone = entry.TxTone,
            FixedLocationBeacon = entry.FixedLocationBeacon,

            Fix1Lat = entry.Fix1Lat,
            Fix1Ns = entry.Fix1Ns,
            Fix1Lng = entry.Fix1Lng,
            Fix1Ew = entry.Fix1Ew,

            ToCall = entry.ToCall,
            ToCallSsid = entry.ToCallSsid,
            YourCall = entry.YourCall,
            YourCallSsid = entry.YourCallSsid,
            DigipeaterPath = entry.DigipeaterPath,

            AprsSymbol = entry.AprsSymbol,
            MapIcon = entry.MapIcon,
            TxPower = entry.TxPower,
            PrewaveTime = entry.PrewaveTime,

            RoamingSupport = entry.RoamingSupport,
            RepeaterActivationDelay = entry.RepeaterActivationDelay,
            DisTime = entry.DisTime,
            Altitude = entry.Altitude,
            AnalogTxMode = entry.AnalogTxMode,
            PassAll = entry.PassAll,

            TxFreq2Mhz = entry.TxFreq2Mhz,
            TxFreq3Mhz = entry.TxFreq3Mhz,
            TxFreq4Mhz = entry.TxFreq4Mhz,
            TxFreq5Mhz = entry.TxFreq5Mhz,
            TxFreq6Mhz = entry.TxFreq6Mhz,
            TxFreq7Mhz = entry.TxFreq7Mhz,
            TxFreq8Mhz = entry.TxFreq8Mhz,

            SendingText = entry.SendingText,

            FilterPosition = entry.FilterPosition,
            FilterMicE = entry.FilterMicE,
            FilterObject = entry.FilterObject,
            FilterItem = entry.FilterItem,
            FilterMessage = entry.FilterMessage,
            FilterWxReport = entry.FilterWxReport,
            FilterNmeaReport = entry.FilterNmeaReport,
            FilterStatusReport = entry.FilterStatusReport,
            FilterOther = entry.FilterOther,

            AdditionalFixLocations = entry.AdditionalFixLocations.Select(f => new AprsFixLocationData
            {
                Number = f.Number,
                Lat = f.Lat,
                Ns = f.Ns,
                Lng = f.Lng,
                Ew = f.Ew
            }).ToList(),

            DigitalReports = entry.DigitalReports.Select(r => new AprsDigitalReportData
            {
                Number = r.Number,
                Channel = r.Channel,
                TalkgroupId = r.TalkgroupId,
                CallType = r.CallType,
                Slot = r.Slot
            }).ToList()
        };
    }

    private static AprsReceiveFilterData ToData(AprsReceiveFilterEntry entry)
    {
        return new AprsReceiveFilterData
        {
            Number = entry.Number,
            Enabled = entry.Enabled,
            Callsign = entry.Callsign,
            Ssid = entry.Ssid
        };
    }

    private static AprsReceiveFilterEntry ToEntry(AprsReceiveFilterData data)
    {
        return new AprsReceiveFilterEntry
        {
            Number = data.Number,
            Enabled = data.Enabled,
            Callsign = data.Callsign,
            Ssid = data.Ssid
        };
    }

    private static AlarmSettingsData ToData(AlarmSettingsEntry entry)
    {
        return new AlarmSettingsData
        {
            AnalogEmergencyAlarm = entry.AnalogEmergencyAlarm,
            AnalogEniType = entry.AnalogEniType,
            AnalogEmergencyId = entry.AnalogEmergencyId,
            AnalogAlarmTime = entry.AnalogAlarmTime,
            AnalogTxDuration = entry.AnalogTxDuration,
            AnalogRxDuration = entry.AnalogRxDuration,
            AnalogEmergencyChannel = entry.AnalogEmergencyChannel,
            AnalogEniSend = entry.AnalogEniSend,
            AnalogEmergencyCycle = entry.AnalogEmergencyCycle,

            DigitalEmergencyAlarm = entry.DigitalEmergencyAlarm,
            DigitalAlarmTime = entry.DigitalAlarmTime,
            DigitalTxDuration = entry.DigitalTxDuration,
            DigitalRxDuration = entry.DigitalRxDuration,
            DigitalEmergencyChannel = entry.DigitalEmergencyChannel,
            DigitalEmergencyCycle = entry.DigitalEmergencyCycle,
            DigitalEniSend = entry.DigitalEniSend,
            DigitalCallType = entry.DigitalCallType,
            DigitalTgDmrId = entry.DigitalTgDmrId,

            ReceiveAlarm = entry.ReceiveAlarm,
            ManDown = entry.ManDown,
            ManDownDelay = entry.ManDownDelay,

            WorkAloneResponseTime = entry.WorkAloneResponseTime,
            WorkAloneWarningTime = entry.WorkAloneWarningTime,
            WorkAloneResponse = entry.WorkAloneResponse,

            QdcGroupId = entry.QdcGroupId,
            QdcPrivateId = entry.QdcPrivateId
        };
    }

    private static AnalogAddressData ToData(AnalogAddressEntry entry)
    {
        return new AnalogAddressData
        {
            Number = entry.Number,
            AddressNumber = entry.AddressNumber,
            Name = entry.Name
        };
    }

    private static AnalogAddressEntry ToEntry(AnalogAddressData data)
    {
        return new AnalogAddressEntry
        {
            Number = data.Number,
            AddressNumber = data.AddressNumber,
            Name = data.Name
        };
    }

    private static GpsRoamingData ToData(GpsRoamingEntry entry)
    {
        return new GpsRoamingData
        {
            Number = entry.Number,
            Enabled = entry.Enabled,
            ZoneIndex = entry.ZoneIndex,
            LatDegree = entry.LatDegree,
            LatMinute = entry.LatMinute,
            LatMinuteDecimal = entry.LatMinuteDecimal,
            NorthSouth = entry.NorthSouth,
            LongDegree = entry.LongDegree,
            LongMinute = entry.LongMinute,
            LongMinuteDecimal = entry.LongMinuteDecimal,
            EastWest = entry.EastWest,
            Radius = entry.Radius
        };
    }

    private static GpsRoamingEntry ToEntry(GpsRoamingData data)
    {
        return new GpsRoamingEntry
        {
            Number = data.Number,
            Enabled = data.Enabled,
            ZoneIndex = data.ZoneIndex,
            LatDegree = data.LatDegree,
            LatMinute = data.LatMinute,
            LatMinuteDecimal = data.LatMinuteDecimal,
            NorthSouth = data.NorthSouth,
            LongDegree = data.LongDegree,
            LongMinute = data.LongMinute,
            LongMinuteDecimal = data.LongMinuteDecimal,
            EastWest = data.EastWest,
            Radius = data.Radius
        };
    }

    private static TalkgroupWhitelistData ToData(TalkgroupWhitelistEntry entry)
    {
        return new TalkgroupWhitelistData
        {
            Number = entry.Number,
            DmrId = entry.DmrId,
            CallType = entry.CallType
        };
    }

    private static TalkgroupWhitelistEntry ToEntry(TalkgroupWhitelistData data)
    {
        return new TalkgroupWhitelistEntry
        {
            Number = data.Number,
            DmrId = data.DmrId,
            CallType = data.CallType
        };
    }

    private static DigitalContactData ToData(DigitalContactEntry entry)
    {
        return new DigitalContactData
        {
            Index = entry.Index,
            CallType = entry.CallType,
            CallAlert = entry.CallAlert,
            IsFriend = entry.IsFriend,
            RadioId = entry.RadioId,
            Name = entry.Name,
            City = entry.City,
            Callsign = entry.Callsign,
            State = entry.State,
            Country = entry.Country,
            Remarks = entry.Remarks
        };
    }

    private static DigitalContactEntry ToEntry(DigitalContactData data)
    {
        return new DigitalContactEntry
        {
            Index = data.Index,
            CallType = data.CallType,
            CallAlert = data.CallAlert,
            IsFriend = data.IsFriend,
            RadioId = data.RadioId,
            Name = data.Name,
            City = data.City,
            Callsign = data.Callsign,
            State = data.State,
            Country = data.Country,
            Remarks = data.Remarks
        };
    }

    private static DigitalContactWhitelistData ToData(DigitalContactWhitelistEntry entry)
    {
        return new DigitalContactWhitelistData
        {
            Number = entry.Number,
            DmrId = entry.DmrId,
            CallType = entry.CallType
        };
    }

    private static DigitalContactWhitelistEntry ToEntry(DigitalContactWhitelistData data)
    {
        return new DigitalContactWhitelistEntry
        {
            Number = data.Number,
            DmrId = data.DmrId,
            CallType = data.CallType
        };
    }

    private static PrefabricatedSmsData ToData(PrefabricatedSmsEntry entry)
    {
        return new PrefabricatedSmsData
        {
            Number = entry.Number,
            Text = entry.Text
        };
    }

    private static PrefabricatedSmsEntry ToEntry(PrefabricatedSmsData data)
    {
        return new PrefabricatedSmsEntry
        {
            Number = data.Number,
            Text = data.Text
        };
    }

    private static AmAirData ToData(AmAirEntry entry)
    {
        return new AmAirData
        {
            Number = entry.Number,
            FrequencyMhz = entry.FrequencyMhz,
            Name = entry.Name
        };
    }

    private static AmAirEntry ToEntry(AmAirData data)
    {
        return new AmAirEntry
        {
            Number = data.Number,
            FrequencyMhz = data.FrequencyMhz,
            Name = data.Name
        };
    }

    private static AmZoneData ToData(AmZoneEntry entry)
    {
        return new AmZoneData
        {
            Number = entry.Number,
            Name = entry.Name,
            AChannelNumber = entry.AChannel?.Number,
            MemberChannelNumbers = entry.Members.Select(channel => channel.Number).ToList(),
            ScanChannelMemberNumbers = entry.ScanChannelMembers.Select(channel => channel.Number).ToList()
        };
    }

    private static FmChannelData ToData(FmChannelEntry entry)
    {
        return new FmChannelData
        {
            Number = entry.Number,
            FrequencyMhz = entry.FrequencyMhz,
            Name = entry.Name,
            ScanAdd = entry.ScanAdd
        };
    }

    private static FmChannelEntry ToEntry(FmChannelData data)
    {
        return new FmChannelEntry
        {
            Number = data.Number,
            FrequencyMhz = data.FrequencyMhz,
            Name = data.Name,
            ScanAdd = data.ScanAdd
        };
    }
}

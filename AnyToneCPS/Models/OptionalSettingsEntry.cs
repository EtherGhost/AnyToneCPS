using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AnyToneCPS.Models;

/// <summary>
/// There's only ever one Optional Settings record on the radio - single
/// instance, not a collection, like Master ID/Talk Alias/Alarm/APRS
/// Settings. Started as a deliberate partial port (Power-on/Display/Key
/// Function only); now covers every category in the reference's
/// <c>decode_D890UV</c> - see OptionalSettingsCodec's doc comment for known
/// quirks (repeated byte offsets, text-encoding lessons).
/// <see cref="AlertTones"/> is a fixed 25-entry sub-list (5 categories x 5
/// tones), always pre-populated to full size - not user add/removable,
/// same pattern as AprsSettingsEntry's fix-location/digital-report lists.
/// </summary>
public partial class OptionalSettingsEntry : ObservableValidator
{
    /// <summary>Re-runs every property-level validation attribute against
    /// its current (getter-derived) value - needed because <see
    /// cref="ValidateProperty"/> only fires from within a live setter call.
    /// A project Load or a Read From Radio sets the underlying wire ints
    /// directly (object-initializer syntax), which does go through the
    /// generated property setter and its OnXxxChanged hook (see e.g.
    /// OnVfoScanStartFreqUhfChanged), so per-field validation already
    /// happens there - this is a belt-and-braces full sweep, called once
    /// after each Load/Read, cheap for the handful of fields that carry
    /// validation attributes.</summary>
    public void RevalidateAll() => ValidateAllProperties();


    /// <summary>Radio-write baseline for the Power-on tab's 11 writable
    /// fields - the only ones with write support so far (see
    /// OptionalSettingsCodec.PowerOnFieldPatch's doc comment). Same
    /// separate-from-file-save-dirty pattern as ChannelEntry's own
    /// _radioSyncSnapshot: only set by <see cref="MarkRadioSynced"/>, after
    /// a successful Read From Radio or Write, never by a project Save.</summary>
    private PowerOnSnapshot? _radioSyncSnapshot;

    public OptionalSettingsEntry()
    {
        // Category names match the vendor CPS's own UI (english.ini ids
        // 39300-39304): "Call Permit Tone", "Match End Tone" (=CallEnd),
        // "Call Reset Tone", "UnMatch End Tone" (=UnMatchEnd), "All Call
        // End Tone" (=CallAll) - see AlertToneCodec.Categories' doc comment
        // for the 2026-07-20 rename ("IdleChannel" didn't match any real
        // name) and offset-swap fix (CallEnd/UnMatchEnd's byte ranges were
        // themselves swapped) this required.
        foreach (var category in new[] { "CallPermit", "UnMatchEnd", "CallReset", "CallEnd", "CallAll" })
        {
            for (var tone = 1; tone <= 5; tone++)
            {
                AlertTones.Add(new AlertToneEntry { Category = category, ToneNumber = tone });
            }
        }
    }

    [ObservableProperty] private byte _powerOnInterface;
    [ObservableProperty] private string _powerOnDisplayLine1 = "";
    [ObservableProperty] private string _powerOnDisplayLine2 = "";
    [ObservableProperty] private byte _powerOnPassword;
    private string _powerOnPasswordChar = "";
    [ObservableProperty] private byte _defaultStartupChannel;
    [ObservableProperty] private byte _startupZoneA;
    [ObservableProperty] private byte _startupChannelA;
    [ObservableProperty] private byte _startupZoneB;
    [ObservableProperty] private byte _startupChannelB;
    [ObservableProperty] private byte _startupGpsTest;
    [ObservableProperty] private byte _startupReset;

    [ObservableProperty] private byte _brightness;
    [ObservableProperty] private byte _autoBacklightDuration;
    [ObservableProperty] private byte _backlightTxDelay;
    [ObservableProperty] private byte _menuExitTime;
    [ObservableProperty] private byte _timeDisplay;
    [ObservableProperty] private byte _lastCaller;
    [ObservableProperty] private byte _callDisplayMode;
    [ObservableProperty] private byte _callsignDisplayColor;
    [ObservableProperty] private byte _callEndPromptBox;
    [ObservableProperty] private byte _displayChannelNumber;
    [ObservableProperty] private byte _displayCurrentContact;
    [ObservableProperty] private byte _standbyCharColor;
    [ObservableProperty] private byte _standbyBkPicture;
    [ObservableProperty] private byte _showLastCallOnLaunch;
    [ObservableProperty] private byte _separateDisplay;
    [ObservableProperty] private byte _chSwitchingKeepsCaller;
    [ObservableProperty] private byte _backlightRxDelay;
    [ObservableProperty] private byte _channelNameColorA;
    [ObservableProperty] private byte _channelNameColorB;
    [ObservableProperty] private byte _zoneNameColorA;
    [ObservableProperty] private byte _zoneNameColorB;
    [ObservableProperty] private bool _displayChannelType;
    [ObservableProperty] private bool _displayTimeSlot;
    [ObservableProperty] private bool _displayColorCode;
    [ObservableProperty] private byte _dateDisplayFormat;
    [ObservableProperty] private byte _volumeBar;
    // Not in the reference project at all - see OptionalSettingsCodec.
    // Decode's NightMode assignment doc comment.
    [ObservableProperty] private byte _nightMode;

    [ObservableProperty] private byte _keyLock;
    [ObservableProperty] private byte _pf1ShortKey;
    [ObservableProperty] private byte _pf2ShortKey;
    [ObservableProperty] private byte _pf3ShortKey;
    [ObservableProperty] private byte _p1ShortKey;
    [ObservableProperty] private byte _p2ShortKey;
    [ObservableProperty] private byte _pf1LongKey;
    [ObservableProperty] private byte _pf2LongKey;
    [ObservableProperty] private byte _pf3LongKey;
    [ObservableProperty] private byte _p1LongKey;
    [ObservableProperty] private byte _p2LongKey;
    [ObservableProperty] private byte _longKeyTime;
    [ObservableProperty] private bool _knobLock;
    [ObservableProperty] private bool _keyboardLock;
    [ObservableProperty] private bool _sideKeyLock;
    [ObservableProperty] private bool _forcedKeyLock;

    [ObservableProperty] private byte _smsAlert;
    [ObservableProperty] private byte _callAlert;
    [ObservableProperty] private byte _digiCallResetTone;
    [ObservableProperty] private byte _talkPermit;
    [ObservableProperty] private byte _keyTone;
    [ObservableProperty] private byte _digiIdleChannelTone;
    [ObservableProperty] private byte _startupSound;
    [ObservableProperty] private byte _toneKeySoundAdjustable;
    [ObservableProperty] private byte _analogIdleChannelTone;
    [ObservableProperty] private byte _pluginRecordingTone;

    [ObservableProperty] private byte _gpsPower;
    [ObservableProperty] private byte _gpsPositioning;
    [ObservableProperty] private byte _timeZone;
    [ObservableProperty] private byte _rangingInterval;
    [ObservableProperty] private byte _distanceUnit;
    [ObservableProperty] private byte _gpsTemplateInformation;
    [ObservableProperty] private string _gpsInformationChar = "";
    [ObservableProperty] private byte _gpsMode;
    [ObservableProperty] private byte _gpsRoaming;

    [ObservableProperty] private byte _vfoScanType;
    [ObservableProperty] private int _vfoScanStartFreqUhf;
    [ObservableProperty] private int _vfoScanEndFreqUhf;
    [ObservableProperty] private int _vfoScanStartFreqVhf;
    [ObservableProperty] private int _vfoScanEndFreqVhf;

    [ObservableProperty] private byte _autoRepeaterA;
    [ObservableProperty] private byte _autoRepeaterB;
    [ObservableProperty] private byte _autoRepeater1Uhf;
    [ObservableProperty] private byte _autoRepeater1Vhf;
    [ObservableProperty] private byte _autoRepeater2Uhf;
    [ObservableProperty] private byte _autoRepeater2Vhf;
    [ObservableProperty] private byte _repeaterCheck;
    [ObservableProperty] private byte _repeaterCheckInterval;
    [ObservableProperty] private byte _repeaterCheckReconnections;
    [ObservableProperty] private byte _repeaterOutOfRangeNotify;
    [ObservableProperty] private byte _outOfRangeNotify;
    [ObservableProperty] private byte _autoRoaming;
    [ObservableProperty] private byte _autoRoamingStartCondition;
    [ObservableProperty] private byte _autoRoamingFixedTime;
    [ObservableProperty] private byte _roamingEffectWaitTime;
    [ObservableProperty] private byte _roamingZone;
    [ObservableProperty] private int _autoRepeater1MinFreqVhf;
    [ObservableProperty] private int _autoRepeater1MaxFreqVhf;
    [ObservableProperty] private int _autoRepeater1MinFreqUhf;
    [ObservableProperty] private int _autoRepeater1MaxFreqUhf;
    [ObservableProperty] private int _autoRepeater2MinFreqVhf;
    [ObservableProperty] private int _autoRepeater2MaxFreqVhf;
    [ObservableProperty] private int _autoRepeater2MinFreqUhf;
    [ObservableProperty] private int _autoRepeater2MaxFreqUhf;
    [ObservableProperty] private byte _repeaterMode;
    [ObservableProperty] private byte _repCcLimit;
    [ObservableProperty] private byte _repSlotA;
    [ObservableProperty] private byte _repSlotB;

    // Found from scratch 2026-07-28 (no reference-project offset existed at
    // all, same class of gap as Display's "Night Mode") via a dedicated
    // toggle-only differential write - offset 0x15a, plain 0/1 boolean.
    [ObservableProperty] private byte _repeaterWhitelist;

    [ObservableProperty] private byte _recordFunction;
    [ObservableProperty] private byte _recordDelay;

    [ObservableProperty] private byte _maxVolume;
    [ObservableProperty] private byte _powerOnVolumeType;
    [ObservableProperty] private byte _powerOnVolume;
    [ObservableProperty] private byte _maxHeadphoneVolume;
    [ObservableProperty] private byte _digiMicGain;
    [ObservableProperty] private byte _enhancedSoundQuality;
    [ObservableProperty] private byte _analogMicGain;
    [ObservableProperty] private byte _rxAgc;
    [ObservableProperty] private byte _nxMicGain;

    // Found from scratch 2026-07-28 (no reference-project offset existed for
    // any of these) via a live differential write - offsets 0x142, 0x148,
    // 0x149, all right next to already-known Volume/Audio-area fields
    // (RxAgc at 0x147, the WorkMode/RepeaterMode cluster near 0x140-0x146).
    [ObservableProperty] private byte _subSpkInTx;
    [ObservableProperty] private byte _rxNoiseReduction;
    [ObservableProperty] private byte _txNoiseReduction;

    [ObservableProperty] private byte _displayMode;
    [ObservableProperty] private byte _vfMrA;
    [ObservableProperty] private byte _vfMrB;
    [ObservableProperty] private byte _memZoneA;
    [ObservableProperty] private byte _memZoneB;
    [ObservableProperty] private byte _mainChannelSet;
    [ObservableProperty] private byte _subChannelMode;
    [ObservableProperty] private byte _workingMode;

    [ObservableProperty] private byte _voxLevel;
    [ObservableProperty] private byte _voxDelay;
    [ObservableProperty] private byte _voxDetection;
    [ObservableProperty] private byte _btOnOff;
    [ObservableProperty] private byte _btIntMic;
    [ObservableProperty] private byte _btIntSpk;
    [ObservableProperty] private byte _btMicGain;
    [ObservableProperty] private byte _btSpkGain;
    [ObservableProperty] private byte _btHoldTime;
    [ObservableProperty] private byte _btRxDelay;
    [ObservableProperty] private byte _btPttHold;
    [ObservableProperty] private byte _btPttSleepTime;
    [ObservableProperty] private byte _btNrBefore;
    [ObservableProperty] private byte _btNrAfter;

    [ObservableProperty] private byte _steTypeOfCtcss;
    [ObservableProperty] private byte _steWhenNoSignal;
    [ObservableProperty] private byte _steTime;

    [ObservableProperty] private byte _amFmFunction;
    [ObservableProperty] private byte _fmVfoMem;
    [ObservableProperty] private byte _fmWorkChannel;
    [ObservableProperty] private byte _fmMonitor;
    [ObservableProperty] private byte _amVfoMem;
    [ObservableProperty] private byte _amWorkZone;
    [ObservableProperty] private byte _amOffset;
    [ObservableProperty] private byte _amSqlLevel;

    [ObservableProperty] private byte _autoShutdown;
    [ObservableProperty] private byte _powerSave;
    [ObservableProperty] private byte _autoShutdownType;

    [ObservableProperty] private byte _addressBookSentWithCode;
    [ObservableProperty] private byte _tot;
    [ObservableProperty] private byte _language;
    [ObservableProperty] private byte _frequencyStep;
    [ObservableProperty] private byte _generalFrequencyStep;
    [ObservableProperty] private byte _sqlLevelA;
    [ObservableProperty] private byte _sqlLevelB;
    [ObservableProperty] private byte _tbst;
    [ObservableProperty] private byte _analogCallHoldTime;
    [ObservableProperty] private byte _callChannelMaintained;
    [ObservableProperty] private byte _priorityZoneA;
    [ObservableProperty] private byte _priorityZoneB;
    [ObservableProperty] private byte _muteTiming;
    [ObservableProperty] private byte _encryptionType;
    [ObservableProperty] private byte _totPredict;
    [ObservableProperty] private byte _txPowerAgc;
    [ObservableProperty] private byte _noaaMoni;
    [ObservableProperty] private byte _noaaScan;
    [ObservableProperty] private byte _noaa;
    [ObservableProperty] private byte _noaaChannel;

    [ObservableProperty] private byte _groupCallHoldTime;
    [ObservableProperty] private byte _privateCallHoldTime;
    [ObservableProperty] private byte _manualDialGroupCallHoldTime;
    [ObservableProperty] private byte _manualDialPrivateCallHoldTime;
    [ObservableProperty] private byte _voiceHeaderRepetitions;
    [ObservableProperty] private byte _txPreambleDuration;
    [ObservableProperty] private byte _filterOwnId;
    [ObservableProperty] private byte _digitalRemoteKill;
    [ObservableProperty] private byte _digitalMonitor;
    [ObservableProperty] private byte _digitalMonitorCc;
    [ObservableProperty] private byte _digitalMonitorId;
    [ObservableProperty] private byte _monitorSlotHold;
    [ObservableProperty] private byte _remoteMonitor;
    [ObservableProperty] private byte _smsFormat;
    [ObservableProperty] private byte _resetDigitalProtocol;

    [ObservableProperty] private byte _satLocation;
    [ObservableProperty] private byte _satTxPower;
    [ObservableProperty] private byte _satAnaSql;
    [ObservableProperty] private byte _satAosLimit;

    public ObservableCollection<AlertToneEntry> AlertTones { get; } = [];

    // The 7 fields below are the only ones in this ~230-field entity with a
    // confirmed enum in Docs/AnyTone_D890UV/field_options.json - everything
    // else stays a raw byte (see this class's own doc comment). Each pairs
    // a raw byte with a label list in the vendor ini's own id order (which,
    // for every one of these, is a plain ascending sequence - 0, 1, 2... -
    // matching the byte value directly, the same convention ChannelEntry's
    // enum-backed fields already use).
    // Confirmed 2026-07-20 directly against the vendor CPS's own UI
    // (english.ini ids 30164/39027/39028/39049 for the interface options -
    // NOT a contiguous id range, another instance of the shared enum-string
    // pool trap seen elsewhere in this codebase; the On/Off pairs below don't
    // have their own dedicated ids at all - they reuse a generic shared
    // pair). Order confirmed Off=0/On=1 the same day via a live radio read
    // (PowerOnPassword/DefaultStartupChannel both showed the wrong label
    // with "On" first) - matches the Off-first convention every other
    // enum in this app uses.
    public static IReadOnlyList<string> PowerOnInterfaceOptions { get; } = ["Default Interface", "Custom Char", "Custom Picture"];
    public static IReadOnlyList<string> OnOffOptions { get; } = ["Off", "On"];

    public string PowerOnInterfaceText
    {
        get => LabelFor(PowerOnInterface, PowerOnInterfaceOptions);
        set => PowerOnInterface = IndexFor(value, PowerOnInterfaceOptions, PowerOnInterface);
    }

    public string PowerOnPasswordText
    {
        get => LabelFor(PowerOnPassword, OnOffOptions);
        set => PowerOnPassword = IndexFor(value, OnOffOptions, PowerOnPassword);
    }

    // Digits only, max 8 chars, matching the real vendor CPS (a boot-lock
    // PIN, not free text) and the field's own AsciiTextCodec 8-byte
    // allocation - added 2026-07-28 after finding the UI let
    // any character through. Rejects and reverts on invalid input, same
    // pattern as ChannelEntry.ColorCodeText.
    public string PowerOnPasswordChar
    {
        get => _powerOnPasswordChar;
        set
        {
            if (value.Length <= 8 && value.All(char.IsAsciiDigit))
            {
                if (SetProperty(ref _powerOnPasswordChar, value))
                {
                    NotifyPendingRadioWriteProperties();
                }
            }
            else
            {
                OnPropertyChanged(nameof(PowerOnPasswordChar));
            }
        }
    }

    public string DefaultStartupChannelText
    {
        get => LabelFor(DefaultStartupChannel, OnOffOptions);
        set => DefaultStartupChannel = IndexFor(value, OnOffOptions, DefaultStartupChannel);
    }

    public string StartupGpsTestText
    {
        get => LabelFor(StartupGpsTest, OnOffOptions);
        set => StartupGpsTest = IndexFor(value, OnOffOptions, StartupGpsTest);
    }

    public string StartupResetText
    {
        get => LabelFor(StartupReset, OnOffOptions);
        set => StartupReset = IndexFor(value, OnOffOptions, StartupReset);
    }

    // Alert Tone tab, confirmed 2026-07-20 against the vendor CPS's own UI
    // text (english.ini: "Type 1/2/3" ids 3025371/3025381/3025391 for Digi
    // Idle Channel Tone, "Digital&Analog" id 39040 alongside "Digital"
    // 39038/"Analog" 39039 for Talk Permit).
    public static IReadOnlyList<string> NoneRingOptions { get; } = ["None", "Ring"];
    public static IReadOnlyList<string> TalkPermitOptions { get; } = ["Off", "Digital", "Analog", "Digital&Analog"];
    public static IReadOnlyList<string> DigiIdleChannelToneOptions { get; } = ["Off", "Type 1", "Type 2", "Type 3"];

    public string SmsAlertText
    {
        get => LabelFor(SmsAlert, NoneRingOptions);
        set => SmsAlert = IndexFor(value, NoneRingOptions, SmsAlert);
    }

    public string CallAlertText
    {
        get => LabelFor(CallAlert, NoneRingOptions);
        set => CallAlert = IndexFor(value, NoneRingOptions, CallAlert);
    }

    public string DigiCallResetToneText
    {
        get => LabelFor(DigiCallResetTone, OnOffOptions);
        set => DigiCallResetTone = IndexFor(value, OnOffOptions, DigiCallResetTone);
    }

    public string TalkPermitText
    {
        get => LabelFor(TalkPermit, TalkPermitOptions);
        set => TalkPermit = IndexFor(value, TalkPermitOptions, TalkPermit);
    }

    public string KeyToneText
    {
        get => LabelFor(KeyTone, OnOffOptions);
        set => KeyTone = IndexFor(value, OnOffOptions, KeyTone);
    }

    public string DigiIdleChannelToneText
    {
        get => LabelFor(DigiIdleChannelTone, DigiIdleChannelToneOptions);
        set => DigiIdleChannelTone = IndexFor(value, DigiIdleChannelToneOptions, DigiIdleChannelTone);
    }

    public string StartupSoundText
    {
        get => LabelFor(StartupSound, OnOffOptions);
        set => StartupSound = IndexFor(value, OnOffOptions, StartupSound);
    }

    public string AnalogIdleChannelToneText
    {
        get => LabelFor(AnalogIdleChannelTone, OnOffOptions);
        set => AnalogIdleChannelTone = IndexFor(value, OnOffOptions, AnalogIdleChannelTone);
    }

    // 3 of the Alert Tone tab's 5 tone-group matrices (Call Permit/Match End
    // (=CallEnd)/Call Reset) - the other 2 (UnMatchEnd/CallAll) are just
    // below. Split into separate properties rather than one flat 25-entry
    // list purely for UI convenience (each vendor CPS category gets its own
    // section). Always exactly 5 entries each (fixed, see this class's
    // constructor) - no re-notification needed, AlertTones itself never
    // changes shape.
    public IReadOnlyList<AlertToneEntry> CallPermitTones => AlertTones.Where(t => t.Category == "CallPermit").ToList();
    public IReadOnlyList<AlertToneEntry> MatchEndTones => AlertTones.Where(t => t.Category == "CallEnd").ToList();
    public IReadOnlyList<AlertToneEntry> CallResetTones => AlertTones.Where(t => t.Category == "CallReset").ToList();

    // The other 2 of the Alert Tone tab's 5 tone-group matrices (merged in
    // 2026-07-28 from the former separate "Alert Tone1" tab), confirmed
    // against the vendor CPS's own english.ini ids 39303/39304: "UnMatch End
    // Tone", "All Call End Tone".
    public IReadOnlyList<AlertToneEntry> UnMatchEndTones => AlertTones.Where(t => t.Category == "UnMatchEnd").ToList();
    public IReadOnlyList<AlertToneEntry> CallAllTones => AlertTones.Where(t => t.Category == "CallAll").ToList();

    // Was ["Off","1","2","3"] (Constants::VOX_LEVEL, "VOX Level") - corrected
    // 2026-07-25 after finding the real vendor CPS's field (shown
    // as "Vox On/Off", not "VOX Level") only has 2 options. The reference
    // project's own constant table doesn't match this installed CPS
    // version for this field - same class of issue as the Language/
    // TimeDisplay/EncryptionType corrections above, found before a live
    // write rather than after this time.
    public static IReadOnlyList<string> VoxLevelOptions { get; } = ["Off", "On"];
    // Corrected 2026-07-24 against the reference project's own combo-box
    // population code (desktop/src/ui/optional_settings_dialog.cpp +
    // Constants::LANGUAGE/TIME_DISPLAY/ENCRYPTION_TYPE/VF_MR in
    // anytone-lib/src/constants.cpp) - the previous values here (5-item
    // Language list, "24 Hours"/"12 Hours" Time Display, "Normal/Enhanced
    // Encryption" wording, "MR" instead of "MEM") predate this correction and
    // don't match what the vendor CPS's own comboboxes actually contain.
    // Like every other option list sourced this way, NOT yet independently
    // confirmed via live radio read/write.
    // Was ["English","German"] - corrected 2026-07-27: the real vendor CPS
    // only has English/Chinese/Russian. Left
    // at English (its likely already-default value) in the live test that
    // caught this, so the offset itself is not independently transition-
    // confirmed, just the option list.
    public static IReadOnlyList<string> LanguageOptions { get; } = ["English", "Chinese", "Russian"];
    public static IReadOnlyList<string> TimeDisplayOptions { get; } = ["Off", "On"];
    public static IReadOnlyList<string> DistanceUnitOptions { get; } = ["Metric", "Inch System"];
    public static IReadOnlyList<string> GpsModeOptions { get; } = ["GPS", "BDS", "GPS+BDS", "GLONASS", "GPS+GLONASS", "BDS+GLONASS", "All"];
    // Was ["Common","Extended"] - corrected 2026-07-27: the real vendor CPS's
    // 2 options are Common/AES-ARC4; a live differential write confirmed
    // both the offset and
    // that Common is genuinely index 0 (raw 0x00 for "Common").
    public static IReadOnlyList<string> EncryptionTypeOptions { get; } = ["Common", "AES/ARC4"];
    public static IReadOnlyList<string> VfoMemModeOptions { get; } = ["MEM", "VFO"];

    // --- Options sourced from the reference project's own combo-box
    // population code (desktop/src/ui/optional_settings_dialog.cpp,
    // Constants::* string tables in anytone-lib/src/constants.cpp) -
    // this is the SAME literal text the vendor CPS's own comboboxes
    // show, not a guess. NOT yet independently confirmed against a live
    // radio read/write for these particular fields (unlike the Power-on/
    // Alert Zone tabs' enums, which went through live differential
    // testing) - pending a future verification pass, per the same
    // discipline used everywhere else in this codebase.
    public static IReadOnlyList<string> AutoShutdownOptions { get; } = ["Off", "10", "30", "60", "120"];
    public static IReadOnlyList<string> PowerSaveOptions { get; } = ["Off", "1:1", "2:1"];
    public static IReadOnlyList<string> AutoShutdownTypeOptions { get; } = ["is affected by call", "is not affected by call"];
    public static IReadOnlyList<string> BrightnessOptions { get; } = ["1", "2", "3", "4", "5"];
    public static IReadOnlyList<string> AutoBacklightDurationOptions { get; } = ["Always", "5s", "10s", "15s", "20s", "25s", "30s", "1m", "2m", "3m", "4m", "5m", "15m", "30m", "45m", "60m"];
    public static IReadOnlyList<string> BacklightTxDelayOptions { get; } = ["Off", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20", "21", "22", "23", "24", "25", "26", "27", "28", "29", "30"];
    public static IReadOnlyList<string> MenuExitTimeOptions { get; } = ["5", "10", "15", "20", "25", "30", "35", "40", "45", "50", "55", "60"];
    public static IReadOnlyList<string> LastCallerOptions { get; } = ["Off", "Display ID", "Display Callsign", "Show Both"];
    public static IReadOnlyList<string> CallDisplayModeOptions { get; } = ["Turn off Talker Alias", "Call Sign Based", "Name Based"];
    public static IReadOnlyList<string> Color1Options { get; } = ["Orange", "Red", "Yellow", "Green", "Turquiose", "Blue", "White"];
    public static IReadOnlyList<string> Color2Options { get; } = ["White", "Black", "Orange", "Red", "Yellow", "Green", "Turquiose", "Blue"];
    public static IReadOnlyList<string> DisplayChannelNumberOptions { get; } = ["Actual Channel Number", "Sequence Number In Zone"];
    public static IReadOnlyList<string> DisplayStandbyPictureOptions { get; } = ["Default", "Custom 1", "Custom 2"];
    public static IReadOnlyList<string> BacklightRxDelayOptions { get; } = ["Always", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20", "21", "22", "23", "24", "25", "26", "27", "28", "29", "30"];
    public static IReadOnlyList<string> DateDisplayFormatOptions { get; } = ["yyyy/m/d", "d/m/yyyy"];
    public static IReadOnlyList<string> DisplayModeOptions { get; } = ["Channel", "Frequency"];
    public static IReadOnlyList<string> MainChannelSetOptions { get; } = ["A", "B"];
    public static IReadOnlyList<string> WorkingModeOptions { get; } = ["Amateur", "Professional"];
    // Extended to 3.0s 2026-07-27 - confirmed the real vendor CPS's
    // Vox Delay combobox range is 0.5s-3.0s inclusive, one more step than the
    // reference project's 0.5s-2.9s list.
    public static IReadOnlyList<string> VoxDelayOptions { get; } = ["0.5", "0.6", "0.7", "0.8", "0.9", "1.0", "1.1", "1.2", "1.3", "1.4", "1.5", "1.6", "1.7", "1.8", "1.9", "2.0", "2.1", "2.2", "2.3", "2.4", "2.5", "2.6", "2.7", "2.8", "2.9", "3.0"];
    public static IReadOnlyList<string> VoxDetectionOptions { get; } = ["Built-in Microphone", "External Microphone", "Both"];
    public static IReadOnlyList<string> BtGainOptions { get; } = ["1", "2", "3", "4", "5"];
    public static IReadOnlyList<string> BtHoldTimeOptions { get; } = ["Off", "1s", "2s", "3s", "4s", "5s", "6s", "7s", "8s", "9s", "10s", "11s", "12s", "13s", "14s", "15s", "16s", "17s", "18s", "19s", "20s", "21s", "22s", "23s", "24s", "25s", "26s", "27s", "28s", "29s", "30s", "60s", "120s", "Infinite"];
    public static IReadOnlyList<string> BtRxDelayOptions { get; } = ["30ms", "1.0s", "1.5s", "2.0s", "2.5s", "3.0s", "3.5s", "4.0s", "4.5s", "5.0s", "5.5s"];
    public static IReadOnlyList<string> BtPttSleepOptions { get; } = ["Infinity", "1min", "2min", "3min", "4min"];
    public static IReadOnlyList<string> SteCtcssTypeOptions { get; } = ["Off", "Silent", "120 Degree", "180 Degree", "240 Degree"];
    public static IReadOnlyList<string> SteNoSignalOptions { get; } = ["Off", "55.2", "259.2"];

    // 10-1000 step 10 (100 entries) - the "1000" entry (raw 100) was missing
    // until 2026-07-29, cut off at 990 (raw 99); confirmed against the real
    // vendor CPS's own STE Time list, which goes one step further. Generated
    // rather than hand-listed to avoid another off-by-one slip like that one.
    public static IReadOnlyList<string> SteTimeOptions { get; } =
        Enumerable.Range(1, 100).Select(step => (step * 10).ToString(CultureInfo.InvariantCulture)).ToList();
    public static IReadOnlyList<string> KeyLockOptions { get; } = ["Manual", "Auto"];
    // Corrected 2026-07-30 - the ported list had drifted from the real
    // vendor CPS list: 5 entries that don't exist in the vendor CPS at all
    // ("GPS Information", "Ranging", "Channel Ranging", "APRS Type Switch",
    // "APRS Set") were shifting every entry after them out of alignment
    // with the real raw-byte index, and the list was missing the vendor
    // CPS's last 2 entries ("Freq Sync", "Freq Step") entirely. User
    // transcribed the real 59-item list directly from the vendor CPS UI;
    // used verbatim (including its exact spelling/casing) rather than
    // guessed at, since this is the option list every one of the 10
    // PF1-3/P1-2 Short+Long Key fields draws from - "Hot Key 1"-"Hot Key 6"
    // are entries WITHIN this list (an assignable meaning for a key), not
    // separate fields of their own. Not yet independently live-write-
    // confirmed field-by-field beyond this transcription.
    public static IReadOnlyList<string> KeyFunctionOptions { get; } = ["Off", "Voltage", "Power", "Talk Around", "Reverse", "Digital Encryption", "Call", "Vox", "V/M", "Sub PTT", "Scan", "AM/FM", "Alarm", "Record Switch", "Record", "SMS", "Dial", "Monitor", "Main Channel Switch", "Hot Key 1", "Hot Key 2", "Hot Key 3", "Hot Key 4", "Hot Key 5", "Hot Key 6", "Work Alone", "Nuisance Delete", "Digital Monitor", "Sub CH Switch", "Priority Zone", "VFO Scan", "MIC Sound Quality", "LastCall Reply", "Channel Type Switch", "Roaming", "Max Volume", "Slot Switch", "Zone Select", "Timed Roaming Set", "Mute Timing", "CTC/DCS Set", "TBST Send", "BT Wireless", "GPS", "Ch. Name", "CDT Scan", "APRS Send", "Ana APRS Info", "GPS Roaming", "Dim Shut", "Satellite Predicting", "Sq Level", "NOAA Moni", "CH Setting", "RX NR", "TX NR", "Repeater", "Freq Sync", "Freq Step"];
    public static IReadOnlyList<string> LongKeyTimeOptions { get; } = ["1", "2", "3", "4", "5"];
    // Unit moved from the option text ("30s" etc.) into the field's own
    // label 2026-07-30, matching the app-wide "units in headers, not
    // values" rule.
    public static IReadOnlyList<string> TotOptions { get; } = ["Off", "30", "60", "90", "120", "150", "180", "210", "240"];
    // Unit ("K", kHz) moved into the field's own label 2026-07-30 - same
    // list backs both the AM/FM tab's Frequency Step and the Other tab's
    // General Frequency Step.
    public static IReadOnlyList<string> FrequencyStepOptions { get; } = ["2.5", "5", "6.25", "8.33", "10", "12.5", "20", "25", "30", "50"];
    public static IReadOnlyList<string> SqlLevelOptions { get; } = ["OFF", "1", "2", "3", "4", "5"];
    // Unit ("Hz") moved into the field's own label 2026-07-30.
    public static IReadOnlyList<string> TbstOptions { get; } = ["1000", "1450", "1750", "2100"];
    // Corrected 2026-07-30 - topped out at 29 (30 entries), the real
    // vendor CPS's own list goes one step further to 30 (31 entries).
    public static IReadOnlyList<string> AnalogCallHoldTimeOptions { get; } =
        Enumerable.Range(0, 31).Select(n => n.ToString(CultureInfo.InvariantCulture)).ToList();
    // Corrected 2026-07-30 - topped out at 255 (255 entries), the real
    // vendor CPS's own list goes one step further to 256 (256 entries).
    // Unit ("minute") moved into the field's own label the same day, and
    // switched from a hand-written literal to a generated list, matching
    // SteTimeOptions' precedent for the same class of bug.
    public static IReadOnlyList<string> MuteTimingOptions { get; } =
        Enumerable.Range(1, 256).Select(n => n.ToString(CultureInfo.InvariantCulture)).ToList();
    // Confirmed offset 0x154 via a live differential write 2026-07-28
    // (DMR -> Off, raw 1 -> 0). Digital Protocol itself (locked to "DMR",
    // the only option in the real vendor CPS) has no known offset - can't
    // isolate a wrong-vs-correct byte with only one selectable value, same
    // untestable class as FmWorkChannel/AmWorkZone.
    public static IReadOnlyList<string> ResetDigitalProtocolOptions { get; } = ["Off", "DMR"];
    // The plain "s" unit was stripped from the 30 numeric entries 2026-07-30
    // (they all share it, so it moved into the field's own label), but
    // "30min" deliberately KEEPS its own unit suffix - it's the one entry
    // using a different unit than the rest (30 real minutes, not 30
    // seconds), and dropping that distinction would make it look like just
    // another second value, silently 60x wrong if picked by mistake.
    // "Infinite" was never a number and needs no unit.
    public static IReadOnlyList<string> TgHoldTimeOptions { get; } = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20", "21", "22", "23", "24", "25", "26", "27", "28", "29", "30", "30min", "Infinite"];
    // Corrected 2026-07-30 - topped out at 7 (6 entries), the real vendor
    // CPS's own list goes to 10 (9 entries), confirmed.
    public static IReadOnlyList<string> VoiceHeaderRepetitionsOptions { get; } = ["2", "3", "4", "5", "6", "7", "8", "9", "10"];
    // Corrected 2026-07-30 - topped out at 2340 (40 entries, uniform step
    // 60), the real vendor CPS's own list has one more entry (2400) AND is
    // missing 2280 - not a uniform step-60 sequence throughout, confirmed
    // by transcribing it directly from the vendor CPS UI. Built as
    // "every step-60 value 0-2400, except 2280" rather than a flat literal,
    // so the one genuine gap is self-documenting instead of looking like a
    // typo. Unit ("ms") moved into the field's own label the same day.
    public static IReadOnlyList<string> TxPreambleDurationOptions { get; } =
        Enumerable.Range(0, 41).Select(step => step * 60).Where(ms => ms != 2280).Select(ms => ms.ToString(CultureInfo.InvariantCulture)).ToList();
    public static IReadOnlyList<string> DigitalMonitorOptions { get; } = ["Off", "Single Slot", "Double Slot"];
    public static IReadOnlyList<string> DigitalMonitorCcOptions { get; } = ["Any", "Same"];
    // "Customer DMR" removed 2026-07-30 - not present in the real vendor
    // CPS's own SMS Format list (confirmed), reason unconfirmed (a
    // guess: possibly tied to Digital Protocol - see
    // DigitalProtocolOptions below - only relevant once a 2nd protocol like
    // NXDN is supported). Removed rather than kept-disabled: unlike a whole
    // disabled FIELD (the established "keep vendor fields, just disable"
    // convention), disabling one option INSIDE an active combo box would
    // need a real per-item ComboBoxItem.IsEnabled setup, a different, more
    // complex pattern than the plain string-list ItemsSource used
    // everywhere else in this codebase - not worth introducing for one
    // option that may never come back. Re-add if it turns out to be real.
    public static IReadOnlyList<string> SmsFormatOptions { get; } = ["M-SMS", "H-SMS", "DMR Standard"];
    // Added 2026-07-30 - matches the real vendor CPS's own "Digital
    // Protocol" field (distinct from "Reset Digital Protocol" above, an
    // action, not a state), locked to its single option. No known byte
    // offset - by definition untestable via a differential write, since a
    // single-valued field has no value transition to isolate an offset
    // from (same class ResetDigitalProtocol's own "Digital Protocol"
    // sibling was always in - see its doc comment). UI-only, disabled, not
    // read from or written to the radio - kept per the "disable, don't
    // remove" convention: real in the vendor CPS, and a 2nd option (NXDN)
    // might plausibly appear with a future vendor CPS/firmware
    // update.
    public static IReadOnlyList<string> DigitalProtocolOptions { get; } = ["DMR"];
    // Live-write-confirmed 2026-07-28: the reference project's assumed 51-entry,
    // uniform-30-min-step "GMT..." list (Constants::TIME_ZONE in the reference
    // source) is WRONG for this radio's real vendor CPS - a live write of
    // "UTC+09:00" produced raw byte 27, which only matches this real 34-entry
    // list (transcribed directly from the vendor CPS's own dropdown, "UTC"
    // labels and all - only the real-world offsets actually in use, not every
    // theoretical 30-min step). Another instance of the documented "reference
    // clone is fine for byte layout, not authoritative for value ranges" gap.
    public static IReadOnlyList<string> TimeZoneOptions { get; } = ["UTC-12:00", "UTC-11:00", "UTC-10:00", "UTC-09:00", "UTC-08:00", "UTC-07:00", "UTC-06:00", "UTC-05:00", "UTC-04:00", "UTC-03:30", "UTC-03:00", "UTC-02:00", "UTC-01:00", "UTC", "UTC+01:00", "UTC+02:00", "UTC+03:00", "UTC+03:30", "UTC+04:00", "UTC+04:30", "UTC+05:00", "UTC+05:30", "UTC+05:45", "UTC+06:00", "UTC+07:00", "UTC+08:00", "UTC+08:30", "UTC+09:00", "UTC+09:30", "UTC+10:00", "UTC+10:30", "UTC+11:00", "UTC+12:00", "UTC+13:00"];
    public static IReadOnlyList<string> RangingIntervalOptions { get; } = ["5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20", "21", "22", "23", "24", "25", "26", "27", "28", "29", "30", "31", "32", "33", "34", "35", "36", "37", "38", "39", "40", "41", "42", "43", "44", "45", "46", "47", "48", "49", "50", "51", "52", "53", "54", "55", "56", "57", "58", "59", "60", "61", "62", "63", "64", "65", "66", "67", "68", "69", "70", "71", "72", "73", "74", "75", "76", "77", "78", "79", "80", "81", "82", "83", "84", "85", "86", "87", "88", "89", "90", "91", "92", "93", "94", "95", "96", "97", "98", "99", "100", "101", "102", "103", "104", "105", "106", "107", "108", "109", "110", "111", "112", "113", "114", "115", "116", "117", "118", "119", "120", "121", "122", "123", "124", "125", "126", "127", "128", "129", "130", "131", "132", "133", "134", "135", "136", "137", "138", "139", "140", "141", "142", "143", "144", "145", "146", "147", "148", "149", "150", "151", "152", "153", "154", "155", "156", "157", "158", "159", "160", "161", "162", "163", "164", "165", "166", "167", "168", "169", "170", "171", "172", "173", "174", "175", "176", "177", "178", "179", "180", "181", "182", "183", "184", "185", "186", "187", "188", "189", "190", "191", "192", "193", "194", "195", "196", "197", "198", "199", "200", "201", "202", "203", "204", "205", "206", "207", "208", "209", "210", "211", "212", "213", "214", "215", "216", "217", "218", "219", "220", "221", "222", "223", "224", "225", "226", "227", "228", "229", "230", "231", "232", "233", "234", "235", "236", "237", "238", "239", "240", "241", "242", "243", "244", "245", "246", "247", "248", "249", "250", "251", "252", "253", "254", "255"];
    public static IReadOnlyList<string> GpsInfoOnOffOptions { get; } = ["Off", "On"];
    public static IReadOnlyList<string> VfoScanTypeOptions { get; } = ["TO", "CO", "SE"];
    public static IReadOnlyList<string> AutoRepeaterEnabledOptions { get; } = ["Off", "Positive", "Negative"];
    public static IReadOnlyList<string> AutoRepeaterIntervalsOptions { get; } = ["5", "10", "15", "20", "25", "30", "35", "40", "45", "50"];
    // Corrected 2026-07-28 by a live differential write - the reference
    // project's claimed ["3","4","5"] doesn't match the real vendor CPS's
    // own dropdown ("1"/"2"/"3", raw = 0-based index into that).
    public static IReadOnlyList<string> RepeaterCheckReconnectionsOptions { get; } = ["1", "2", "3"];
    public static IReadOnlyList<string> RepeaterOutOfRangeNotifyOptions { get; } = ["Off", "Bell", "Voice"];
    public static IReadOnlyList<string> OutOfRangeNotifyCountOptions { get; } = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "10"];
    public static IReadOnlyList<string> AutoRoamingStartConditionOptions { get; } = ["Fixed Time", "Out Of Range"];
    // Range corrected 2026-08-01 - real vendor CPS goes 1-256 minutes, not
    // 1-8 (this app's original port topped out far too early, same class
    // of bug as AnalogCallHoldTimeOptions/MuteTimingOptions).
    public static IReadOnlyList<string> AutoRoamingFixedTimeOptions { get; } = Enumerable.Range(1, 256).Select(n => n.ToString(CultureInfo.InvariantCulture)).ToList();
    public static IReadOnlyList<string> RoamingEffectWaitTimeOptions { get; } = ["None", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20", "21", "22", "23", "24", "25", "26", "27", "28", "29", "30"];
    public static IReadOnlyList<string> MaxVolumeOptions { get; } = ["Indoors", "1", "2", "3", "4", "5", "6", "7", "8"];
    // Corrected 2026-07-28 by a live differential write - the real vendor
    // CPS has a 6th "Auto" option this app was missing (raw 5 = Auto,
    // confirmed via DigiMicGain/AnalogMicGain/NxMicGain all being set to
    // it and reading back 5).
    public static IReadOnlyList<string> MicGainOptions { get; } = ["1", "2", "3", "4", "5", "Auto"];

    public static IReadOnlyList<string> PowerOnVolumeTypeOptions { get; } = ["Preset", "Minimum"];

    // Confirmed 2026-07-28 by a live differential write - found from scratch,
    // no reference-project offset existed for either field.
    public static IReadOnlyList<string> NoiseReductionOptions { get; } = ["Off", "1", "2", "3", "4", "5"];
    public static IReadOnlyList<string> AmOffsetOptions { get; } = ["Positive", "Negative"];
    // Was OnOffOptions (2 values) - corrected 2026-07-27: the real vendor
    // CPS's field has 4 options; confirmed via a live differential write
    // (FM=index1, AM(A)=index2).
    public static IReadOnlyList<string> AmFmFunctionOptions { get; } = ["Off", "FM", "AM(A)", "AM(B)"];
    public static IReadOnlyList<string> OffNumber5Options { get; } = ["Off", "1", "2", "3", "4", "5"];
    public static IReadOnlyList<string> RepCcLimitOptions { get; } = ["Off", "Match Channel A Color Code", "Match Channel B Color Code"];
    public static IReadOnlyList<string> RepSlotAOptions { get; } = ["Off", "Channel A Fixed Time Slot 1", "Channel A Fixed Time Slot 2"];
    public static IReadOnlyList<string> RepSlotBOptions { get; } = ["Off", "Channel B Fixed Time Slot 1", "Channel B Fixed Time Slot 2"];
    // Unit ("s") moved to the Record tab's "Record Delay (s)" header 2026-08-01.
    public static IReadOnlyList<string> RecordDelayOptions { get; } = ["0.0", "0.2", "0.4", "0.6", "0.8", "1.0", "1.2", "1.4", "1.6", "1.8", "2.0", "2.2", "2.4", "2.6", "2.8", "3.0", "3.2", "3.4", "3.6", "3.8", "4.0", "4.2", "4.4", "4.6", "4.8", "5.0"];
    // Index 0 corrected 2026-08-01 from "Off" to "GPS", after a
    // direct vendor CPS comparison - this is a location SOURCE selector
    // (use the radio's own GPS fix, or one of 8 fixed/preset locations),
    // not an on/off toggle.
    public static IReadOnlyList<string> SatLocationOptions { get; } = ["GPS", "Fixed-1", "Fixed-2", "Fixed-3", "Fixed-4", "Fixed-5", "Fixed-6", "Fixed-7", "Fixed-8"];
    public static IReadOnlyList<string> SatAnaSqlOptions { get; } = ["0", "1", "2", "3", "4", "5"];
    public static IReadOnlyList<string> SatAosLimitOptions { get; } = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20", "21", "22", "23", "24", "25", "26", "27", "28", "29", "30"];
    public static IReadOnlyList<string> NoaaChannelCountOptions { get; } = ["1", "2", "3", "4", "5", "6", "7", "8", "9", "10"];
    public static IReadOnlyList<string> TxPowerOptions { get; } = ["Low", "Mid", "High", "Turbo"];

    public string VoxLevelText
    {
        get => LabelFor(VoxLevel, VoxLevelOptions);
        set => VoxLevel = IndexFor(value, VoxLevelOptions, VoxLevel);
    }

    public string LanguageText
    {
        get => LabelFor(Language, LanguageOptions);
        set => Language = IndexFor(value, LanguageOptions, Language);
    }

    public string TimeDisplayText
    {
        get => LabelFor(TimeDisplay, TimeDisplayOptions);
        set => TimeDisplay = IndexFor(value, TimeDisplayOptions, TimeDisplay);
    }

    public string DistanceUnitText
    {
        get => LabelFor(DistanceUnit, DistanceUnitOptions);
        set => DistanceUnit = IndexFor(value, DistanceUnitOptions, DistanceUnit);
    }

    public string GpsModeText
    {
        get => LabelFor(GpsMode, GpsModeOptions);
        set => GpsMode = IndexFor(value, GpsModeOptions, GpsMode);
    }

    public string EncryptionTypeText
    {
        get => LabelFor(EncryptionType, EncryptionTypeOptions);
        set => EncryptionType = IndexFor(value, EncryptionTypeOptions, EncryptionType);
    }

    // Channels' Digital/AES/ARC4 Key comboboxes should only be enabled
    // when this radio-wide setting is "AES/ARC4" (index 1) - the real
    // vendor CPS greys them out under "Common" (index 0).
    public bool IsAesArc4EncryptionTypeSelected => EncryptionType == 1;

    public string VfMrAText
    {
        get => LabelFor(VfMrA, VfoMemModeOptions);
        set => VfMrA = IndexFor(value, VfoMemModeOptions, VfMrA);
    }

    public string VfMrBText
    {
        get => LabelFor(VfMrB, VfoMemModeOptions);
        set => VfMrB = IndexFor(value, VfoMemModeOptions, VfMrB);
    }

    public string AutoShutdownText
    {
        get => LabelFor(AutoShutdown, AutoShutdownOptions);
        set => AutoShutdown = IndexFor(value, AutoShutdownOptions, AutoShutdown);
    }

    public string PowerSaveText
    {
        get => LabelFor(PowerSave, PowerSaveOptions);
        set => PowerSave = IndexFor(value, PowerSaveOptions, PowerSave);
    }

    public string AutoShutdownTypeText
    {
        get => LabelFor(AutoShutdownType, AutoShutdownTypeOptions);
        set => AutoShutdownType = IndexFor(value, AutoShutdownTypeOptions, AutoShutdownType);
    }

    public string BrightnessText
    {
        get => LabelFor(Brightness, BrightnessOptions);
        set => Brightness = IndexFor(value, BrightnessOptions, Brightness);
    }

    public string AutoBacklightDurationText
    {
        get => LabelFor(AutoBacklightDuration, AutoBacklightDurationOptions);
        set => AutoBacklightDuration = IndexFor(value, AutoBacklightDurationOptions, AutoBacklightDuration);
    }

    public string BacklightTxDelayText
    {
        get => LabelFor(BacklightTxDelay, BacklightTxDelayOptions);
        set => BacklightTxDelay = IndexFor(value, BacklightTxDelayOptions, BacklightTxDelay);
    }

    public string MenuExitTimeText
    {
        get => LabelFor(MenuExitTime, MenuExitTimeOptions);
        set => MenuExitTime = IndexFor(value, MenuExitTimeOptions, MenuExitTime);
    }

    public string LastCallerText
    {
        get => LabelFor(LastCaller, LastCallerOptions);
        set => LastCaller = IndexFor(value, LastCallerOptions, LastCaller);
    }

    public string CallDisplayModeText
    {
        get => LabelFor(CallDisplayMode, CallDisplayModeOptions);
        set => CallDisplayMode = IndexFor(value, CallDisplayModeOptions, CallDisplayMode);
    }

    public string CallsignDisplayColorText
    {
        get => LabelFor(CallsignDisplayColor, Color1Options);
        set => CallsignDisplayColor = IndexFor(value, Color1Options, CallsignDisplayColor);
    }

    public string CallEndPromptBoxText
    {
        get => LabelFor(CallEndPromptBox, OnOffOptions);
        set => CallEndPromptBox = IndexFor(value, OnOffOptions, CallEndPromptBox);
    }

    // Not in the reference project - see NightMode's Decode assignment doc
    // comment for how its offset was found.
    public string NightModeText
    {
        get => LabelFor(NightMode, OnOffOptions);
        set => NightMode = IndexFor(value, OnOffOptions, NightMode);
    }

    public string DisplayChannelNumberText
    {
        get => LabelFor(DisplayChannelNumber, DisplayChannelNumberOptions);
        set => DisplayChannelNumber = IndexFor(value, DisplayChannelNumberOptions, DisplayChannelNumber);
    }

    public string DisplayCurrentContactText
    {
        get => LabelFor(DisplayCurrentContact, OnOffOptions);
        set => DisplayCurrentContact = IndexFor(value, OnOffOptions, DisplayCurrentContact);
    }

    public string StandbyCharColorText
    {
        get => LabelFor(StandbyCharColor, Color2Options);
        set => StandbyCharColor = IndexFor(value, Color2Options, StandbyCharColor);
    }

    public string StandbyBkPictureText
    {
        get => LabelFor(StandbyBkPicture, DisplayStandbyPictureOptions);
        set => StandbyBkPicture = IndexFor(value, DisplayStandbyPictureOptions, StandbyBkPicture);
    }

    public string ShowLastCallOnLaunchText
    {
        get => LabelFor(ShowLastCallOnLaunch, OnOffOptions);
        set => ShowLastCallOnLaunch = IndexFor(value, OnOffOptions, ShowLastCallOnLaunch);
    }

    public string SeparateDisplayText
    {
        get => LabelFor(SeparateDisplay, OnOffOptions);
        set => SeparateDisplay = IndexFor(value, OnOffOptions, SeparateDisplay);
    }

    public string ChSwitchingKeepsCallerText
    {
        get => LabelFor(ChSwitchingKeepsCaller, OnOffOptions);
        set => ChSwitchingKeepsCaller = IndexFor(value, OnOffOptions, ChSwitchingKeepsCaller);
    }

    public string BacklightRxDelayText
    {
        get => LabelFor(BacklightRxDelay, BacklightRxDelayOptions);
        set => BacklightRxDelay = IndexFor(value, BacklightRxDelayOptions, BacklightRxDelay);
    }

    public string ChannelNameColorAText
    {
        get => LabelFor(ChannelNameColorA, Color1Options);
        set => ChannelNameColorA = IndexFor(value, Color1Options, ChannelNameColorA);
    }

    public string ChannelNameColorBText
    {
        get => LabelFor(ChannelNameColorB, Color1Options);
        set => ChannelNameColorB = IndexFor(value, Color1Options, ChannelNameColorB);
    }

    public string ZoneNameColorAText
    {
        get => LabelFor(ZoneNameColorA, Color1Options);
        set => ZoneNameColorA = IndexFor(value, Color1Options, ZoneNameColorA);
    }

    public string ZoneNameColorBText
    {
        get => LabelFor(ZoneNameColorB, Color1Options);
        set => ZoneNameColorB = IndexFor(value, Color1Options, ZoneNameColorB);
    }

    public string DateDisplayFormatText
    {
        get => LabelFor(DateDisplayFormat, DateDisplayFormatOptions);
        set => DateDisplayFormat = IndexFor(value, DateDisplayFormatOptions, DateDisplayFormat);
    }

    public string VolumeBarText
    {
        get => LabelFor(VolumeBar, OnOffOptions);
        set => VolumeBar = IndexFor(value, OnOffOptions, VolumeBar);
    }

    public string DisplayModeText
    {
        get => LabelFor(DisplayMode, DisplayModeOptions);
        set => DisplayMode = IndexFor(value, DisplayModeOptions, DisplayMode);
    }

    // Matches the real vendor CPS: VF/MR A and B are only editable while
    // Display Mode is Channel (index 0) - both disabled entirely when
    // Display Mode is Frequency (index 1). Confirmed 2026-07-29, same
    // IsEnabled pattern as IsVoxDetectionEditable.
    public bool IsVfMrEditable => DisplayMode == 0;

    public string MainChannelSetText
    {
        get => LabelFor(MainChannelSet, MainChannelSetOptions);
        set => MainChannelSet = IndexFor(value, MainChannelSetOptions, MainChannelSet);
    }

    public string SubChannelModeText
    {
        get => LabelFor(SubChannelMode, OnOffOptions);
        set => SubChannelMode = IndexFor(value, OnOffOptions, SubChannelMode);
    }

    public string WorkingModeText
    {
        get => LabelFor(WorkingMode, WorkingModeOptions);
        set => WorkingMode = IndexFor(value, WorkingModeOptions, WorkingMode);
    }

    public string VoxDelayText
    {
        get => LabelFor(VoxDelay, VoxDelayOptions);
        set => VoxDelay = IndexFor(value, VoxDelayOptions, VoxDelay);
    }

    public string VoxDetectionText
    {
        get => LabelFor(VoxDetection, VoxDetectionOptions);
        set => VoxDetection = IndexFor(value, VoxDetectionOptions, VoxDetection);
    }

    // Matches the real vendor CPS: Vox Detection is only editable while VOX
    // itself is Off (confirmed 2026-07-27, its own control is disabled
    // whenever Vox On/Off = On).
    public bool IsVoxDetectionEditable => VoxLevel == 0;

    // Safety-critical, added 2026-07-30: a radio with VOX enabled can start
    // transmitting on its own (triggered by ambient sound) while connected
    // to the PC for programming - a known hazard in the ham radio community,
    // RF energy can feed back through the cable and damage the PC. Drives a
    // warning banner on the Vox/BT tab, a warning before Read/Write, and a
    // VOX-aware hint in connection-failure messages - see
    // MainViewModel.Radio.cs/MainViewModel.RadioWrite.cs for where this gets
    // checked, and MainView.axaml's/MobileMainView.axaml's Vox/BT tab
    // section for the banner itself.
    public bool IsVoxOn => VoxLevel != 0;

    public string BtOnOffText
    {
        get => LabelFor(BtOnOff, OnOffOptions);
        set => BtOnOff = IndexFor(value, OnOffOptions, BtOnOff);
    }

    public string BtIntMicText
    {
        get => LabelFor(BtIntMic, OnOffOptions);
        set => BtIntMic = IndexFor(value, OnOffOptions, BtIntMic);
    }

    public string BtIntSpkText
    {
        get => LabelFor(BtIntSpk, OnOffOptions);
        set => BtIntSpk = IndexFor(value, OnOffOptions, BtIntSpk);
    }

    public string BtMicGainText
    {
        get => LabelFor(BtMicGain, BtGainOptions);
        set => BtMicGain = IndexFor(value, BtGainOptions, BtMicGain);
    }

    public string BtSpkGainText
    {
        get => LabelFor(BtSpkGain, BtGainOptions);
        set => BtSpkGain = IndexFor(value, BtGainOptions, BtSpkGain);
    }

    public string BtHoldTimeText
    {
        get => LabelFor(BtHoldTime, BtHoldTimeOptions);
        set => BtHoldTime = IndexFor(value, BtHoldTimeOptions, BtHoldTime);
    }

    public string BtRxDelayText
    {
        get => LabelFor(BtRxDelay, BtRxDelayOptions);
        set => BtRxDelay = IndexFor(value, BtRxDelayOptions, BtRxDelay);
    }

    public string BtPttHoldText
    {
        get => LabelFor(BtPttHold, OnOffOptions);
        set => BtPttHold = IndexFor(value, OnOffOptions, BtPttHold);
    }

    public string BtPttSleepTimeText
    {
        get => LabelFor(BtPttSleepTime, BtPttSleepOptions);
        set => BtPttSleepTime = IndexFor(value, BtPttSleepOptions, BtPttSleepTime);
    }

    public string BtNrBeforeText
    {
        get => LabelFor(BtNrBefore, OffNumber5Options);
        set => BtNrBefore = IndexFor(value, OffNumber5Options, BtNrBefore);
    }

    public string BtNrAfterText
    {
        get => LabelFor(BtNrAfter, OffNumber5Options);
        set => BtNrAfter = IndexFor(value, OffNumber5Options, BtNrAfter);
    }

    public string SteTypeOfCtcssText
    {
        get => LabelFor(SteTypeOfCtcss, SteCtcssTypeOptions);
        set => SteTypeOfCtcss = IndexFor(value, SteCtcssTypeOptions, SteTypeOfCtcss);
    }

    public string SteWhenNoSignalText
    {
        get => LabelFor(SteWhenNoSignal, SteNoSignalOptions);
        set => SteWhenNoSignal = IndexFor(value, SteNoSignalOptions, SteWhenNoSignal);
    }

    // Unlike every other Ste field, the raw byte is NOT a zero-based index
    // into SteTimeOptions - confirmed via a live differential write
    // 2026-07-27 (selecting exactly "150MS" in the real vendor CPS produced
    // raw byte 15, i.e. raw = milliseconds/10 directly, one position off
    // from the array's own 0-based indexing since SteTimeOptions[0] is
    // "10MS" not "0MS"). LabelFor/IndexFor's generic helpers assume
    // raw==index, so this field needs its own off-by-one-aware conversion.
    public string SteTimeText
    {
        get => SteTime is >= 1 && SteTime <= SteTimeOptions.Count ? SteTimeOptions[SteTime - 1] : SteTime.ToString();
        set
        {
            var index = SteTimeOptions.ToList().IndexOf(value);
            if (index >= 0)
            {
                SteTime = (byte)(index + 1);
            }
        }
    }

    public string AmFmFunctionText
    {
        get => LabelFor(AmFmFunction, AmFmFunctionOptions);
        set => AmFmFunction = IndexFor(value, AmFmFunctionOptions, AmFmFunction);
    }

    // AmFmFunctionOptions is ["Off", "FM", "AM(A)", "AM(B)"] (indexes 0-3) -
    // matches the real vendor CPS: the FM group box is only editable while
    // this is "FM", and the AM group box only while it's "AM(A)" or
    // "AM(B)" - both disabled while it's "Off". Confirmed 2026-07-29.
    public bool IsFmSectionEnabled => AmFmFunction == 1;
    public bool IsAmSectionEnabled => AmFmFunction is 2 or 3;

    public string FmVfoMemText
    {
        get => LabelFor(FmVfoMem, VfoMemModeOptions);
        set => FmVfoMem = IndexFor(value, VfoMemModeOptions, FmVfoMem);
    }

    public string FmMonitorText
    {
        get => LabelFor(FmMonitor, OnOffOptions);
        set => FmMonitor = IndexFor(value, OnOffOptions, FmMonitor);
    }

    public string AmVfoMemText
    {
        get => LabelFor(AmVfoMem, VfoMemModeOptions);
        set => AmVfoMem = IndexFor(value, VfoMemModeOptions, AmVfoMem);
    }

    public string AmOffsetText
    {
        get => LabelFor(AmOffset, AmOffsetOptions);
        set => AmOffset = IndexFor(value, AmOffsetOptions, AmOffset);
    }

    public string AmSqlLevelText
    {
        get => LabelFor(AmSqlLevel, OffNumber5Options);
        set => AmSqlLevel = IndexFor(value, OffNumber5Options, AmSqlLevel);
    }

    public string KeyLockText
    {
        get => LabelFor(KeyLock, KeyLockOptions);
        set => KeyLock = IndexFor(value, KeyLockOptions, KeyLock);
    }

    public string Pf1ShortKeyText
    {
        get => LabelFor(Pf1ShortKey, KeyFunctionOptions);
        set => Pf1ShortKey = IndexFor(value, KeyFunctionOptions, Pf1ShortKey);
    }

    public string Pf2ShortKeyText
    {
        get => LabelFor(Pf2ShortKey, KeyFunctionOptions);
        set => Pf2ShortKey = IndexFor(value, KeyFunctionOptions, Pf2ShortKey);
    }

    public string Pf3ShortKeyText
    {
        get => LabelFor(Pf3ShortKey, KeyFunctionOptions);
        set => Pf3ShortKey = IndexFor(value, KeyFunctionOptions, Pf3ShortKey);
    }

    public string P1ShortKeyText
    {
        get => LabelFor(P1ShortKey, KeyFunctionOptions);
        set => P1ShortKey = IndexFor(value, KeyFunctionOptions, P1ShortKey);
    }

    public string P2ShortKeyText
    {
        get => LabelFor(P2ShortKey, KeyFunctionOptions);
        set => P2ShortKey = IndexFor(value, KeyFunctionOptions, P2ShortKey);
    }

    public string Pf1LongKeyText
    {
        get => LabelFor(Pf1LongKey, KeyFunctionOptions);
        set => Pf1LongKey = IndexFor(value, KeyFunctionOptions, Pf1LongKey);
    }

    public string Pf2LongKeyText
    {
        get => LabelFor(Pf2LongKey, KeyFunctionOptions);
        set => Pf2LongKey = IndexFor(value, KeyFunctionOptions, Pf2LongKey);
    }

    public string Pf3LongKeyText
    {
        get => LabelFor(Pf3LongKey, KeyFunctionOptions);
        set => Pf3LongKey = IndexFor(value, KeyFunctionOptions, Pf3LongKey);
    }

    public string P1LongKeyText
    {
        get => LabelFor(P1LongKey, KeyFunctionOptions);
        set => P1LongKey = IndexFor(value, KeyFunctionOptions, P1LongKey);
    }

    public string P2LongKeyText
    {
        get => LabelFor(P2LongKey, KeyFunctionOptions);
        set => P2LongKey = IndexFor(value, KeyFunctionOptions, P2LongKey);
    }

    public string LongKeyTimeText
    {
        get => LabelFor(LongKeyTime, LongKeyTimeOptions);
        set => LongKeyTime = IndexFor(value, LongKeyTimeOptions, LongKeyTime);
    }

    public string AddressBookSentWithCodeText
    {
        get => LabelFor(AddressBookSentWithCode, OnOffOptions);
        set => AddressBookSentWithCode = IndexFor(value, OnOffOptions, AddressBookSentWithCode);
    }

    public string TotText
    {
        get => LabelFor(Tot, TotOptions);
        set => Tot = IndexFor(value, TotOptions, Tot);
    }

    public string FrequencyStepText
    {
        get => LabelFor(FrequencyStep, FrequencyStepOptions);
        set => FrequencyStep = IndexFor(value, FrequencyStepOptions, FrequencyStep);
    }

    public string GeneralFrequencyStepText
    {
        get => LabelFor(GeneralFrequencyStep, FrequencyStepOptions);
        set => GeneralFrequencyStep = IndexFor(value, FrequencyStepOptions, GeneralFrequencyStep);
    }

    public string SqlLevelAText
    {
        get => LabelFor(SqlLevelA, SqlLevelOptions);
        set => SqlLevelA = IndexFor(value, SqlLevelOptions, SqlLevelA);
    }

    public string SqlLevelBText
    {
        get => LabelFor(SqlLevelB, SqlLevelOptions);
        set => SqlLevelB = IndexFor(value, SqlLevelOptions, SqlLevelB);
    }

    public string TbstText
    {
        get => LabelFor(Tbst, TbstOptions);
        set => Tbst = IndexFor(value, TbstOptions, Tbst);
    }

    public string AnalogCallHoldTimeText
    {
        get => LabelFor(AnalogCallHoldTime, AnalogCallHoldTimeOptions);
        set => AnalogCallHoldTime = IndexFor(value, AnalogCallHoldTimeOptions, AnalogCallHoldTime);
    }

    public string CallChannelMaintainedText
    {
        get => LabelFor(CallChannelMaintained, OnOffOptions);
        set => CallChannelMaintained = IndexFor(value, OnOffOptions, CallChannelMaintained);
    }

    public string MuteTimingText
    {
        get => LabelFor(MuteTiming, MuteTimingOptions);
        set => MuteTiming = IndexFor(value, MuteTimingOptions, MuteTiming);
    }

    public string TotPredictText
    {
        get => LabelFor(TotPredict, OnOffOptions);
        set => TotPredict = IndexFor(value, OnOffOptions, TotPredict);
    }

    public string TxPowerAgcText
    {
        get => LabelFor(TxPowerAgc, OnOffOptions);
        set => TxPowerAgc = IndexFor(value, OnOffOptions, TxPowerAgc);
    }

    public string NoaaMoniText
    {
        get => LabelFor(NoaaMoni, OnOffOptions);
        set => NoaaMoni = IndexFor(value, OnOffOptions, NoaaMoni);
    }

    public string NoaaScanText
    {
        get => LabelFor(NoaaScan, OnOffOptions);
        set => NoaaScan = IndexFor(value, OnOffOptions, NoaaScan);
    }

    public string NoaaText
    {
        get => LabelFor(Noaa, OnOffOptions);
        set => Noaa = IndexFor(value, OnOffOptions, Noaa);
    }

    public string GroupCallHoldTimeText
    {
        get => OffsetLabelFor(GroupCallHoldTime, TgHoldTimeOptions, 1);
        set => GroupCallHoldTime = OffsetIndexFor(value, TgHoldTimeOptions, 1, GroupCallHoldTime);
    }

    public string PrivateCallHoldTimeText
    {
        get => OffsetLabelFor(PrivateCallHoldTime, TgHoldTimeOptions, 1);
        set => PrivateCallHoldTime = OffsetIndexFor(value, TgHoldTimeOptions, 1, PrivateCallHoldTime);
    }

    public string ManualDialGroupCallHoldTimeText
    {
        get => OffsetLabelFor(ManualDialGroupCallHoldTime, TgHoldTimeOptions, 1);
        set => ManualDialGroupCallHoldTime = OffsetIndexFor(value, TgHoldTimeOptions, 1, ManualDialGroupCallHoldTime);
    }

    public string ManualDialPrivateCallHoldTimeText
    {
        get => OffsetLabelFor(ManualDialPrivateCallHoldTime, TgHoldTimeOptions, 1);
        set => ManualDialPrivateCallHoldTime = OffsetIndexFor(value, TgHoldTimeOptions, 1, ManualDialPrivateCallHoldTime);
    }

    public string VoiceHeaderRepetitionsText
    {
        get => OffsetLabelFor(VoiceHeaderRepetitions, VoiceHeaderRepetitionsOptions, 2);
        set => VoiceHeaderRepetitions = OffsetIndexFor(value, VoiceHeaderRepetitionsOptions, 2, VoiceHeaderRepetitions);
    }

    public string TxPreambleDurationText
    {
        get => LabelFor(TxPreambleDuration, TxPreambleDurationOptions);
        set => TxPreambleDuration = IndexFor(value, TxPreambleDurationOptions, TxPreambleDuration);
    }

    public string FilterOwnIdText
    {
        get => LabelFor(FilterOwnId, OnOffOptions);
        set => FilterOwnId = IndexFor(value, OnOffOptions, FilterOwnId);
    }

    public string DigitalRemoteKillText
    {
        get => LabelFor(DigitalRemoteKill, OnOffOptions);
        set => DigitalRemoteKill = IndexFor(value, OnOffOptions, DigitalRemoteKill);
    }

    public string DigitalMonitorText
    {
        get => LabelFor(DigitalMonitor, DigitalMonitorOptions);
        set => DigitalMonitor = IndexFor(value, DigitalMonitorOptions, DigitalMonitor);
    }

    public string DigitalMonitorCcText
    {
        get => LabelFor(DigitalMonitorCc, DigitalMonitorCcOptions);
        set => DigitalMonitorCc = IndexFor(value, DigitalMonitorCcOptions, DigitalMonitorCc);
    }

    public string DigitalMonitorIdText
    {
        get => LabelFor(DigitalMonitorId, DigitalMonitorCcOptions);
        set => DigitalMonitorId = IndexFor(value, DigitalMonitorCcOptions, DigitalMonitorId);
    }

    public string MonitorSlotHoldText
    {
        get => LabelFor(MonitorSlotHold, OnOffOptions);
        set => MonitorSlotHold = IndexFor(value, OnOffOptions, MonitorSlotHold);
    }

    public string RemoteMonitorText
    {
        get => LabelFor(RemoteMonitor, OnOffOptions);
        set => RemoteMonitor = IndexFor(value, OnOffOptions, RemoteMonitor);
    }

    public string SmsFormatText
    {
        get => LabelFor(SmsFormat, SmsFormatOptions);
        set => SmsFormat = IndexFor(value, SmsFormatOptions, SmsFormat);
    }

    public string ResetDigitalProtocolText
    {
        get => LabelFor(ResetDigitalProtocol, ResetDigitalProtocolOptions);
        set => ResetDigitalProtocol = IndexFor(value, ResetDigitalProtocolOptions, ResetDigitalProtocol);
    }

    public string GpsPowerText
    {
        get => LabelFor(GpsPower, OnOffOptions);
        set => GpsPower = IndexFor(value, OnOffOptions, GpsPower);
    }

    public string GpsPositioningText
    {
        get => LabelFor(GpsPositioning, OnOffOptions);
        set => GpsPositioning = IndexFor(value, OnOffOptions, GpsPositioning);
    }

    public string TimeZoneText
    {
        get => LabelFor(TimeZone, TimeZoneOptions);
        set => TimeZone = IndexFor(value, TimeZoneOptions, TimeZone);
    }

    public string RangingIntervalText
    {
        get => LabelFor(RangingInterval, RangingIntervalOptions);
        set => RangingInterval = IndexFor(value, RangingIntervalOptions, RangingInterval);
    }

    public string GpsTemplateInformationText
    {
        get => LabelFor(GpsTemplateInformation, GpsInfoOnOffOptions);
        set => GpsTemplateInformation = IndexFor(value, GpsInfoOnOffOptions, GpsTemplateInformation);
    }

    public string GpsRoamingText
    {
        get => LabelFor(GpsRoaming, OnOffOptions);
        set => GpsRoaming = IndexFor(value, OnOffOptions, GpsRoaming);
    }

    public string VfoScanTypeText
    {
        get => LabelFor(VfoScanType, VfoScanTypeOptions);
        set => VfoScanType = IndexFor(value, VfoScanTypeOptions, VfoScanType);
    }

    // Confirmed 2026-07-28 by a live differential write (405.12300/475.98700/
    // 140.45600/170.65400 MHz entered, raw ints came back within 1 unit of
    // MHz*100000 - same convention as ChannelEntry's own frequency fields,
    // just plain binary here instead of BCD). Previously these 4 fields were
    // shown/edited as the raw unscaled int directly - a real display bug,
    // fixed by these Text wrappers.
    // Range limits (400-480 MHz UHF, 136-174 MHz VHF) added 2026-07-30,
    // confirmed against the real radio's actual band limits.
    // Originally enforced with reject-and-revert (force the TextBox back to
    // the last valid value on every keystroke that produced an out-of-range
    // number) - dropped 2026-07-30 because a lower bound above zero makes
    // that pattern impossible to type into: building "440" up one digit at
    // a time passes through "4" and "44", both below the 136/400 floor, so
    // every single keystroke got reverted and the field could never be
    // typed at all. Replaced with real validation instead: the raw text is
    // always accepted (never forced back), ValidateProperty attaches an
    // error via the CustomValidation attribute below, Avalonia's
    // INotifyDataErrorInfo support surfaces it on the control, and
    // MainViewModel.ValidateOptionalSettings blocks Save/Write while any
    // field on this entry HasErrors. See DigitOnlyInput.MaxValue for the
    // separate (and still-valid) upper-bound keystroke filter - that one
    // only ever blocks keystrokes that would exceed the max, so it never
    // exhibited this same "impossible to reach a high floor" problem.
    [CustomValidation(typeof(OptionalSettingsEntry), nameof(ValidateUhfFrequencyText))]
    public string VfoScanStartFreqUhfText
    {
        get => (VfoScanStartFreqUhf / 100000.0).ToString("F5", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(VfoScanStartFreqUhfText));
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz) && mhz is >= 400.0 and <= 480.0)
            {
                VfoScanStartFreqUhf = (int)Math.Round(mhz * 100000.0);
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    [CustomValidation(typeof(OptionalSettingsEntry), nameof(ValidateUhfFrequencyText))]
    public string VfoScanEndFreqUhfText
    {
        get => (VfoScanEndFreqUhf / 100000.0).ToString("F5", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(VfoScanEndFreqUhfText));
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz) && mhz is >= 400.0 and <= 480.0)
            {
                VfoScanEndFreqUhf = (int)Math.Round(mhz * 100000.0);
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    [CustomValidation(typeof(OptionalSettingsEntry), nameof(ValidateVhfFrequencyText))]
    public string VfoScanStartFreqVhfText
    {
        get => (VfoScanStartFreqVhf / 100000.0).ToString("F5", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(VfoScanStartFreqVhfText));
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz) && mhz is >= 136.0 and <= 174.0)
            {
                VfoScanStartFreqVhf = (int)Math.Round(mhz * 100000.0);
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    [CustomValidation(typeof(OptionalSettingsEntry), nameof(ValidateVhfFrequencyText))]
    public string VfoScanEndFreqVhfText
    {
        get => (VfoScanEndFreqVhf / 100000.0).ToString("F5", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(VfoScanEndFreqVhfText));
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz) && mhz is >= 136.0 and <= 174.0)
            {
                VfoScanEndFreqVhf = (int)Math.Round(mhz * 100000.0);
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    public static ValidationResult? ValidateUhfFrequencyText(string? value, ValidationContext context)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz))
        {
            return new ValidationResult("Enter a decimal frequency in MHz.", [context.MemberName!]);
        }

        return mhz is >= 400.0 and <= 480.0
            ? ValidationResult.Success
            : new ValidationResult("Must be 400.00000-480.00000 MHz - the radio's UHF band limits.", [context.MemberName!]);
    }

    public static ValidationResult? ValidateVhfFrequencyText(string? value, ValidationContext context)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz))
        {
            return new ValidationResult("Enter a decimal frequency in MHz.", [context.MemberName!]);
        }

        return mhz is >= 136.0 and <= 174.0
            ? ValidationResult.Success
            : new ValidationResult("Must be 136.00000-174.00000 MHz - the radio's VHF band limits.", [context.MemberName!]);
    }

    public static ValidationResult? ValidateFrequencyText(string? value, ValidationContext context)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
            ? ValidationResult.Success
            : new ValidationResult("Enter a decimal frequency in MHz.", [context.MemberName!]);

    public string AutoRepeaterAText
    {
        get => LabelFor(AutoRepeaterA, AutoRepeaterEnabledOptions);
        set => AutoRepeaterA = IndexFor(value, AutoRepeaterEnabledOptions, AutoRepeaterA);
    }

    public string AutoRepeaterBText
    {
        get => LabelFor(AutoRepeaterB, AutoRepeaterEnabledOptions);
        set => AutoRepeaterB = IndexFor(value, AutoRepeaterEnabledOptions, AutoRepeaterB);
    }

    // Confirmed 2026-07-28 by a live differential write - these 4 fields
    // are NOT a plain 3-item list as first assumed. Raw 0="600.00 kHz",
    // raw 1="5.00000 MHz"; "Off" is a separate sentinel (raw 0xFF, the
    // flash-blank value), not index 2 of a 3-item list.
    public static IReadOnlyList<string> AutoRepeaterOffsetOptions { get; } = ["Off", "600.00 kHz", "5.00000 MHz"];

    private static string AutoRepeaterOffsetLabelFor(byte value) => value switch
    {
        0 => "600.00 kHz",
        1 => "5.00000 MHz",
        _ => "Off"
    };

    private static byte AutoRepeaterOffsetIndexFor(string value, byte currentValue) => value switch
    {
        "600.00 kHz" => 0,
        "5.00000 MHz" => 1,
        "Off" => 0xFF,
        _ => currentValue
    };

    public string AutoRepeater1UhfText
    {
        get => AutoRepeaterOffsetLabelFor(AutoRepeater1Uhf);
        set => AutoRepeater1Uhf = AutoRepeaterOffsetIndexFor(value, AutoRepeater1Uhf);
    }

    public string AutoRepeater1VhfText
    {
        get => AutoRepeaterOffsetLabelFor(AutoRepeater1Vhf);
        set => AutoRepeater1Vhf = AutoRepeaterOffsetIndexFor(value, AutoRepeater1Vhf);
    }

    public string AutoRepeater2UhfText
    {
        get => AutoRepeaterOffsetLabelFor(AutoRepeater2Uhf);
        set => AutoRepeater2Uhf = AutoRepeaterOffsetIndexFor(value, AutoRepeater2Uhf);
    }

    public string AutoRepeater2VhfText
    {
        get => AutoRepeaterOffsetLabelFor(AutoRepeater2Vhf);
        set => AutoRepeater2Vhf = AutoRepeaterOffsetIndexFor(value, AutoRepeater2Vhf);
    }

    public string RepeaterCheckText
    {
        get => LabelFor(RepeaterCheck, OnOffOptions);
        set => RepeaterCheck = IndexFor(value, OnOffOptions, RepeaterCheck);
    }

    public string RepeaterCheckIntervalText
    {
        get => LabelFor(RepeaterCheckInterval, AutoRepeaterIntervalsOptions);
        set => RepeaterCheckInterval = IndexFor(value, AutoRepeaterIntervalsOptions, RepeaterCheckInterval);
    }

    public string RepeaterCheckReconnectionsText
    {
        get => LabelFor(RepeaterCheckReconnections, RepeaterCheckReconnectionsOptions);
        set => RepeaterCheckReconnections = IndexFor(value, RepeaterCheckReconnectionsOptions, RepeaterCheckReconnections);
    }

    public string RepeaterOutOfRangeNotifyText
    {
        get => LabelFor(RepeaterOutOfRangeNotify, RepeaterOutOfRangeNotifyOptions);
        set => RepeaterOutOfRangeNotify = IndexFor(value, RepeaterOutOfRangeNotifyOptions, RepeaterOutOfRangeNotify);
    }

    public string OutOfRangeNotifyText
    {
        get => LabelFor(OutOfRangeNotify, OutOfRangeNotifyCountOptions);
        set => OutOfRangeNotify = IndexFor(value, OutOfRangeNotifyCountOptions, OutOfRangeNotify);
    }

    public string AutoRoamingText
    {
        get => LabelFor(AutoRoaming, OnOffOptions);
        set => AutoRoaming = IndexFor(value, OnOffOptions, AutoRoaming);
    }

    public string AutoRoamingStartConditionText
    {
        get => LabelFor(AutoRoamingStartCondition, AutoRoamingStartConditionOptions);
        set => AutoRoamingStartCondition = IndexFor(value, AutoRoamingStartConditionOptions, AutoRoamingStartCondition);
    }

    public string AutoRoamingFixedTimeText
    {
        get => LabelFor(AutoRoamingFixedTime, AutoRoamingFixedTimeOptions);
        set => AutoRoamingFixedTime = IndexFor(value, AutoRoamingFixedTimeOptions, AutoRoamingFixedTime);
    }

    public string RoamingEffectWaitTimeText
    {
        get => LabelFor(RoamingEffectWaitTime, RoamingEffectWaitTimeOptions);
        set => RoamingEffectWaitTime = IndexFor(value, RoamingEffectWaitTimeOptions, RoamingEffectWaitTime);
    }

    public string RepeaterModeText
    {
        get => LabelFor(RepeaterMode, OnOffOptions);
        set => RepeaterMode = IndexFor(value, OnOffOptions, RepeaterMode);
    }

    public string RepCcLimitText
    {
        get => LabelFor(RepCcLimit, RepCcLimitOptions);
        set => RepCcLimit = IndexFor(value, RepCcLimitOptions, RepCcLimit);
    }

    public string RepSlotAText
    {
        get => LabelFor(RepSlotA, RepSlotAOptions);
        set => RepSlotA = IndexFor(value, RepSlotAOptions, RepSlotA);
    }

    public string RepSlotBText
    {
        get => LabelFor(RepSlotB, RepSlotBOptions);
        set => RepSlotB = IndexFor(value, RepSlotBOptions, RepSlotB);
    }

    public string RepeaterWhitelistText
    {
        get => LabelFor(RepeaterWhitelist, OnOffOptions);
        set => RepeaterWhitelist = IndexFor(value, OnOffOptions, RepeaterWhitelist);
    }

    // Same MHz*100000 convention as VfoScanStartFreqUhfText - confirmed
    // 2026-07-28 by a live differential write (Min freq fields matched
    // exactly; Max freq fields were validation-rejected in the first test
    // but confirmed correct on a second, in-range test).
    // Same reject-and-revert removal as the VFO Scan fields above. Range
    // limits added 2026-08-01, confirmed to be the same real VHF/UHF
    // band limits as VFO Scan (136.00000-174.00000 / 400.00000-480.00000) -
    // reuses VfoScanStartFreqUhfText's own ValidateUhfFrequencyText/
    // ValidateVhfFrequencyText validators directly rather than duplicating
    // them.
    [CustomValidation(typeof(OptionalSettingsEntry), nameof(ValidateVhfFrequencyText))]
    public string AutoRepeater1MinFreqVhfText
    {
        get => (AutoRepeater1MinFreqVhf / 100000.0).ToString("F5", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(AutoRepeater1MinFreqVhfText));
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz) && mhz is >= 136.0 and <= 174.0)
            {
                AutoRepeater1MinFreqVhf = (int)Math.Round(mhz * 100000.0);
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    [CustomValidation(typeof(OptionalSettingsEntry), nameof(ValidateVhfFrequencyText))]
    public string AutoRepeater1MaxFreqVhfText
    {
        get => (AutoRepeater1MaxFreqVhf / 100000.0).ToString("F5", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(AutoRepeater1MaxFreqVhfText));
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz) && mhz is >= 136.0 and <= 174.0)
            {
                AutoRepeater1MaxFreqVhf = (int)Math.Round(mhz * 100000.0);
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    [CustomValidation(typeof(OptionalSettingsEntry), nameof(ValidateUhfFrequencyText))]
    public string AutoRepeater1MinFreqUhfText
    {
        get => (AutoRepeater1MinFreqUhf / 100000.0).ToString("F5", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(AutoRepeater1MinFreqUhfText));
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz) && mhz is >= 400.0 and <= 480.0)
            {
                AutoRepeater1MinFreqUhf = (int)Math.Round(mhz * 100000.0);
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    [CustomValidation(typeof(OptionalSettingsEntry), nameof(ValidateUhfFrequencyText))]
    public string AutoRepeater1MaxFreqUhfText
    {
        get => (AutoRepeater1MaxFreqUhf / 100000.0).ToString("F5", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(AutoRepeater1MaxFreqUhfText));
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz) && mhz is >= 400.0 and <= 480.0)
            {
                AutoRepeater1MaxFreqUhf = (int)Math.Round(mhz * 100000.0);
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    [CustomValidation(typeof(OptionalSettingsEntry), nameof(ValidateVhfFrequencyText))]
    public string AutoRepeater2MinFreqVhfText
    {
        get => (AutoRepeater2MinFreqVhf / 100000.0).ToString("F5", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(AutoRepeater2MinFreqVhfText));
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz) && mhz is >= 136.0 and <= 174.0)
            {
                AutoRepeater2MinFreqVhf = (int)Math.Round(mhz * 100000.0);
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    [CustomValidation(typeof(OptionalSettingsEntry), nameof(ValidateVhfFrequencyText))]
    public string AutoRepeater2MaxFreqVhfText
    {
        get => (AutoRepeater2MaxFreqVhf / 100000.0).ToString("F5", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(AutoRepeater2MaxFreqVhfText));
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz) && mhz is >= 136.0 and <= 174.0)
            {
                AutoRepeater2MaxFreqVhf = (int)Math.Round(mhz * 100000.0);
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    [CustomValidation(typeof(OptionalSettingsEntry), nameof(ValidateUhfFrequencyText))]
    public string AutoRepeater2MinFreqUhfText
    {
        get => (AutoRepeater2MinFreqUhf / 100000.0).ToString("F5", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(AutoRepeater2MinFreqUhfText));
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz) && mhz is >= 400.0 and <= 480.0)
            {
                AutoRepeater2MinFreqUhf = (int)Math.Round(mhz * 100000.0);
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    [CustomValidation(typeof(OptionalSettingsEntry), nameof(ValidateUhfFrequencyText))]
    public string AutoRepeater2MaxFreqUhfText
    {
        get => (AutoRepeater2MaxFreqUhf / 100000.0).ToString("F5", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(AutoRepeater2MaxFreqUhfText));
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var mhz) && mhz is >= 400.0 and <= 480.0)
            {
                AutoRepeater2MaxFreqUhf = (int)Math.Round(mhz * 100000.0);
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    public string RecordFunctionText
    {
        get => LabelFor(RecordFunction, OnOffOptions);
        set => RecordFunction = IndexFor(value, OnOffOptions, RecordFunction);
    }

    public string RecordDelayText
    {
        get => LabelFor(RecordDelay, RecordDelayOptions);
        set => RecordDelay = IndexFor(value, RecordDelayOptions, RecordDelay);
    }

    public string MaxVolumeText
    {
        get => LabelFor(MaxVolume, MaxVolumeOptions);
        set => MaxVolume = IndexFor(value, MaxVolumeOptions, MaxVolume);
    }

    public string MaxHeadphoneVolumeText
    {
        get => LabelFor(MaxHeadphoneVolume, MaxVolumeOptions);
        set => MaxHeadphoneVolume = IndexFor(value, MaxVolumeOptions, MaxHeadphoneVolume);
    }

    public string DigiMicGainText
    {
        get => LabelFor(DigiMicGain, MicGainOptions);
        set => DigiMicGain = IndexFor(value, MicGainOptions, DigiMicGain);
    }

    public string EnhancedSoundQualityText
    {
        get => LabelFor(EnhancedSoundQuality, OnOffOptions);
        set => EnhancedSoundQuality = IndexFor(value, OnOffOptions, EnhancedSoundQuality);
    }

    public string AnalogMicGainText
    {
        get => LabelFor(AnalogMicGain, MicGainOptions);
        set => AnalogMicGain = IndexFor(value, MicGainOptions, AnalogMicGain);
    }

    public string PowerOnVolumeTypeText
    {
        get => LabelFor(PowerOnVolumeType, PowerOnVolumeTypeOptions);
        set => PowerOnVolumeType = IndexFor(value, PowerOnVolumeTypeOptions, PowerOnVolumeType);
    }

    // The 2026-07-28 live write test only confirmed the wire ENCODING is a
    // plain zero-based index (writing "5" produced raw 5) - it didn't
    // independently confirm MaxVolumeOptions' own Indoors/1-8 LABELS are
    // what the real vendor CPS shows for this field specifically, and they
    // aren't: corrected 2026-08-01 after a direct vendor CPS
    // comparison to a plain 0-15 scale, its own option list.
    public static IReadOnlyList<string> PowerOnVolumeOptions { get; } = Enumerable.Range(0, 16).Select(n => n.ToString(CultureInfo.InvariantCulture)).ToList();

    public string PowerOnVolumeText
    {
        get => LabelFor(PowerOnVolume, PowerOnVolumeOptions);
        set => PowerOnVolume = IndexFor(value, PowerOnVolumeOptions, PowerOnVolume);
    }

    // Off when Power On Volume Type is "Minimum" - the radio ignores the
    // preset volume entirely in that mode, matching the real vendor CPS's
    // own disabled-combobox behavior (confirmed 2026-08-01).
    public bool IsPowerOnVolumeEnabled => PowerOnVolumeType != 1;

    // Confirmed 2026-07-28 by a live differential write - previously assumed
    // to be some raw numeric value, actually a plain On/Off like most other
    // fields on this tab.
    public string RxAgcText
    {
        get => LabelFor(RxAgc, OnOffOptions);
        set => RxAgc = IndexFor(value, OnOffOptions, RxAgc);
    }

    public string NxMicGainText
    {
        get => LabelFor(NxMicGain, MicGainOptions);
        set => NxMicGain = IndexFor(value, MicGainOptions, NxMicGain);
    }

    public string SubSpkInTxText
    {
        get => LabelFor(SubSpkInTx, OnOffOptions);
        set => SubSpkInTx = IndexFor(value, OnOffOptions, SubSpkInTx);
    }

    public string RxNoiseReductionText
    {
        get => LabelFor(RxNoiseReduction, NoiseReductionOptions);
        set => RxNoiseReduction = IndexFor(value, NoiseReductionOptions, RxNoiseReduction);
    }

    public string TxNoiseReductionText
    {
        get => LabelFor(TxNoiseReduction, NoiseReductionOptions);
        set => TxNoiseReduction = IndexFor(value, NoiseReductionOptions, TxNoiseReduction);
    }

    public string SatLocationText
    {
        get => LabelFor(SatLocation, SatLocationOptions);
        set => SatLocation = IndexFor(value, SatLocationOptions, SatLocation);
    }

    public string SatTxPowerText
    {
        get => LabelFor(SatTxPower, TxPowerOptions);
        set => SatTxPower = IndexFor(value, TxPowerOptions, SatTxPower);
    }

    public string SatAnaSqlText
    {
        get => LabelFor(SatAnaSql, SatAnaSqlOptions);
        set => SatAnaSql = IndexFor(value, SatAnaSqlOptions, SatAnaSql);
    }

    public string SatAosLimitText
    {
        get => LabelFor(SatAosLimit, SatAosLimitOptions);
        set => SatAosLimit = IndexFor(value, SatAosLimitOptions, SatAosLimit);
    }

    public string NoaaChannelText
    {
        get => LabelFor(NoaaChannel, NoaaChannelCountOptions);
        set => NoaaChannel = IndexFor(value, NoaaChannelCountOptions, NoaaChannel);
    }


    // Power-on tab + Alert Tone tab's 8 scalar Alert Tone fields (see
    // PowerOnSnapshot's doc comment for the full list of fields with write
    // support). The Alert Tone tab's 25 tone-matrix entries (all 5
    // categories: CallPermit/CallEnd/CallReset/UnMatchEnd/CallAll) have
    // their OWN per-entry dirty tracking on AlertToneEntry instead, since
    // they live in the AlertTones collection, not as scalar properties
    // here. Fields with no write support yet get no IsXxxPendingRadioWrite
    // of their own.
    public bool IsPowerOnInterfacePendingRadioWrite => _radioSyncSnapshot is null || PowerOnInterface != _radioSyncSnapshot.PowerOnInterface;
    public bool IsPowerOnDisplayLine1PendingRadioWrite => _radioSyncSnapshot is null || PowerOnDisplayLine1 != _radioSyncSnapshot.PowerOnDisplayLine1;
    public bool IsPowerOnDisplayLine2PendingRadioWrite => _radioSyncSnapshot is null || PowerOnDisplayLine2 != _radioSyncSnapshot.PowerOnDisplayLine2;
    public bool IsPowerOnPasswordPendingRadioWrite => _radioSyncSnapshot is null || PowerOnPassword != _radioSyncSnapshot.PowerOnPassword;
    public bool IsPowerOnPasswordCharPendingRadioWrite => _radioSyncSnapshot is null || PowerOnPasswordChar != _radioSyncSnapshot.PowerOnPasswordChar;
    public bool IsDefaultStartupChannelPendingRadioWrite => _radioSyncSnapshot is null || DefaultStartupChannel != _radioSyncSnapshot.DefaultStartupChannel;
    public bool IsStartupZoneAPendingRadioWrite => _radioSyncSnapshot is null || StartupZoneA != _radioSyncSnapshot.StartupZoneA;
    public bool IsStartupChannelAPendingRadioWrite => _radioSyncSnapshot is null || StartupChannelA != _radioSyncSnapshot.StartupChannelA;
    public bool IsStartupZoneBPendingRadioWrite => _radioSyncSnapshot is null || StartupZoneB != _radioSyncSnapshot.StartupZoneB;
    public bool IsStartupChannelBPendingRadioWrite => _radioSyncSnapshot is null || StartupChannelB != _radioSyncSnapshot.StartupChannelB;
    public bool IsStartupResetPendingRadioWrite => _radioSyncSnapshot is null || StartupReset != _radioSyncSnapshot.StartupReset;
    public bool IsSmsAlertPendingRadioWrite => _radioSyncSnapshot is null || SmsAlert != _radioSyncSnapshot.SmsAlert;
    public bool IsCallAlertPendingRadioWrite => _radioSyncSnapshot is null || CallAlert != _radioSyncSnapshot.CallAlert;
    public bool IsDigiCallResetTonePendingRadioWrite => _radioSyncSnapshot is null || DigiCallResetTone != _radioSyncSnapshot.DigiCallResetTone;
    public bool IsTalkPermitPendingRadioWrite => _radioSyncSnapshot is null || TalkPermit != _radioSyncSnapshot.TalkPermit;
    public bool IsKeyTonePendingRadioWrite => _radioSyncSnapshot is null || KeyTone != _radioSyncSnapshot.KeyTone;
    public bool IsDigiIdleChannelTonePendingRadioWrite => _radioSyncSnapshot is null || DigiIdleChannelTone != _radioSyncSnapshot.DigiIdleChannelTone;
    public bool IsStartupSoundPendingRadioWrite => _radioSyncSnapshot is null || StartupSound != _radioSyncSnapshot.StartupSound;
    public bool IsAnalogIdleChannelTonePendingRadioWrite => _radioSyncSnapshot is null || AnalogIdleChannelTone != _radioSyncSnapshot.AnalogIdleChannelTone;
    public bool IsAutoShutdownPendingRadioWrite => _radioSyncSnapshot is null || AutoShutdown != _radioSyncSnapshot.AutoShutdown;
    public bool IsPowerSavePendingRadioWrite => _radioSyncSnapshot is null || PowerSave != _radioSyncSnapshot.PowerSave;
    public bool IsAutoShutdownTypePendingRadioWrite => _radioSyncSnapshot is null || AutoShutdownType != _radioSyncSnapshot.AutoShutdownType;
    public bool IsBrightnessPendingRadioWrite => _radioSyncSnapshot is null || Brightness != _radioSyncSnapshot.Brightness;
    public bool IsAutoBacklightDurationPendingRadioWrite => _radioSyncSnapshot is null || AutoBacklightDuration != _radioSyncSnapshot.AutoBacklightDuration;
    public bool IsBacklightTxDelayPendingRadioWrite => _radioSyncSnapshot is null || BacklightTxDelay != _radioSyncSnapshot.BacklightTxDelay;
    public bool IsMenuExitTimePendingRadioWrite => _radioSyncSnapshot is null || MenuExitTime != _radioSyncSnapshot.MenuExitTime;
    public bool IsTimeDisplayPendingRadioWrite => _radioSyncSnapshot is null || TimeDisplay != _radioSyncSnapshot.TimeDisplay;
    public bool IsLastCallerPendingRadioWrite => _radioSyncSnapshot is null || LastCaller != _radioSyncSnapshot.LastCaller;
    public bool IsCallDisplayModePendingRadioWrite => _radioSyncSnapshot is null || CallDisplayMode != _radioSyncSnapshot.CallDisplayMode;
    public bool IsCallsignDisplayColorPendingRadioWrite => _radioSyncSnapshot is null || CallsignDisplayColor != _radioSyncSnapshot.CallsignDisplayColor;
    public bool IsCallEndPromptBoxPendingRadioWrite => _radioSyncSnapshot is null || CallEndPromptBox != _radioSyncSnapshot.CallEndPromptBox;
    public bool IsDisplayChannelNumberPendingRadioWrite => _radioSyncSnapshot is null || DisplayChannelNumber != _radioSyncSnapshot.DisplayChannelNumber;
    public bool IsDisplayCurrentContactPendingRadioWrite => _radioSyncSnapshot is null || DisplayCurrentContact != _radioSyncSnapshot.DisplayCurrentContact;
    public bool IsStandbyCharColorPendingRadioWrite => _radioSyncSnapshot is null || StandbyCharColor != _radioSyncSnapshot.StandbyCharColor;
    public bool IsStandbyBkPicturePendingRadioWrite => _radioSyncSnapshot is null || StandbyBkPicture != _radioSyncSnapshot.StandbyBkPicture;
    public bool IsShowLastCallOnLaunchPendingRadioWrite => _radioSyncSnapshot is null || ShowLastCallOnLaunch != _radioSyncSnapshot.ShowLastCallOnLaunch;
    public bool IsSeparateDisplayPendingRadioWrite => _radioSyncSnapshot is null || SeparateDisplay != _radioSyncSnapshot.SeparateDisplay;
    public bool IsChSwitchingKeepsCallerPendingRadioWrite => _radioSyncSnapshot is null || ChSwitchingKeepsCaller != _radioSyncSnapshot.ChSwitchingKeepsCaller;
    public bool IsBacklightRxDelayPendingRadioWrite => _radioSyncSnapshot is null || BacklightRxDelay != _radioSyncSnapshot.BacklightRxDelay;
    public bool IsChannelNameColorAPendingRadioWrite => _radioSyncSnapshot is null || ChannelNameColorA != _radioSyncSnapshot.ChannelNameColorA;
    public bool IsChannelNameColorBPendingRadioWrite => _radioSyncSnapshot is null || ChannelNameColorB != _radioSyncSnapshot.ChannelNameColorB;
    public bool IsZoneNameColorAPendingRadioWrite => _radioSyncSnapshot is null || ZoneNameColorA != _radioSyncSnapshot.ZoneNameColorA;
    public bool IsZoneNameColorBPendingRadioWrite => _radioSyncSnapshot is null || ZoneNameColorB != _radioSyncSnapshot.ZoneNameColorB;
    public bool IsDisplayChannelTypePendingRadioWrite => _radioSyncSnapshot is null || DisplayChannelType != _radioSyncSnapshot.DisplayChannelType;
    public bool IsDisplayTimeSlotPendingRadioWrite => _radioSyncSnapshot is null || DisplayTimeSlot != _radioSyncSnapshot.DisplayTimeSlot;
    public bool IsDisplayColorCodePendingRadioWrite => _radioSyncSnapshot is null || DisplayColorCode != _radioSyncSnapshot.DisplayColorCode;
    public bool IsDateDisplayFormatPendingRadioWrite => _radioSyncSnapshot is null || DateDisplayFormat != _radioSyncSnapshot.DateDisplayFormat;
    public bool IsVolumeBarPendingRadioWrite => _radioSyncSnapshot is null || VolumeBar != _radioSyncSnapshot.VolumeBar;
    public bool IsNightModePendingRadioWrite => _radioSyncSnapshot is null || NightMode != _radioSyncSnapshot.NightMode;
    public bool IsDisplayModePendingRadioWrite => _radioSyncSnapshot is null || DisplayMode != _radioSyncSnapshot.DisplayMode;
    public bool IsVfMrAPendingRadioWrite => _radioSyncSnapshot is null || VfMrA != _radioSyncSnapshot.VfMrA;
    public bool IsMemZoneAPendingRadioWrite => _radioSyncSnapshot is null || MemZoneA != _radioSyncSnapshot.MemZoneA;
    public bool IsVfMrBPendingRadioWrite => _radioSyncSnapshot is null || VfMrB != _radioSyncSnapshot.VfMrB;
    public bool IsMemZoneBPendingRadioWrite => _radioSyncSnapshot is null || MemZoneB != _radioSyncSnapshot.MemZoneB;
    public bool IsMainChannelSetPendingRadioWrite => _radioSyncSnapshot is null || MainChannelSet != _radioSyncSnapshot.MainChannelSet;
    public bool IsSubChannelModePendingRadioWrite => _radioSyncSnapshot is null || SubChannelMode != _radioSyncSnapshot.SubChannelMode;
    public bool IsWorkingModePendingRadioWrite => _radioSyncSnapshot is null || WorkingMode != _radioSyncSnapshot.WorkingMode;
    public bool IsVoxLevelPendingRadioWrite => _radioSyncSnapshot is null || VoxLevel != _radioSyncSnapshot.VoxLevel;
    public bool IsVoxDelayPendingRadioWrite => _radioSyncSnapshot is null || VoxDelay != _radioSyncSnapshot.VoxDelay;
    public bool IsVoxDetectionPendingRadioWrite => _radioSyncSnapshot is null || VoxDetection != _radioSyncSnapshot.VoxDetection;
    public bool IsSteTypeOfCtcssPendingRadioWrite => _radioSyncSnapshot is null || SteTypeOfCtcss != _radioSyncSnapshot.SteTypeOfCtcss;
    public bool IsSteWhenNoSignalPendingRadioWrite => _radioSyncSnapshot is null || SteWhenNoSignal != _radioSyncSnapshot.SteWhenNoSignal;
    public bool IsSteTimePendingRadioWrite => _radioSyncSnapshot is null || SteTime != _radioSyncSnapshot.SteTime;
    public bool IsAddressBookSentWithCodePendingRadioWrite => _radioSyncSnapshot is null || AddressBookSentWithCode != _radioSyncSnapshot.AddressBookSentWithCode;
    public bool IsTotPendingRadioWrite => _radioSyncSnapshot is null || Tot != _radioSyncSnapshot.Tot;
    public bool IsLanguagePendingRadioWrite => _radioSyncSnapshot is null || Language != _radioSyncSnapshot.Language;
    public bool IsGeneralFrequencyStepPendingRadioWrite => _radioSyncSnapshot is null || GeneralFrequencyStep != _radioSyncSnapshot.GeneralFrequencyStep;
    public bool IsSqlLevelAPendingRadioWrite => _radioSyncSnapshot is null || SqlLevelA != _radioSyncSnapshot.SqlLevelA;
    public bool IsSqlLevelBPendingRadioWrite => _radioSyncSnapshot is null || SqlLevelB != _radioSyncSnapshot.SqlLevelB;
    public bool IsTbstPendingRadioWrite => _radioSyncSnapshot is null || Tbst != _radioSyncSnapshot.Tbst;
    public bool IsAnalogCallHoldTimePendingRadioWrite => _radioSyncSnapshot is null || AnalogCallHoldTime != _radioSyncSnapshot.AnalogCallHoldTime;
    public bool IsCallChannelMaintainedPendingRadioWrite => _radioSyncSnapshot is null || CallChannelMaintained != _radioSyncSnapshot.CallChannelMaintained;
    public bool IsPriorityZoneAPendingRadioWrite => _radioSyncSnapshot is null || PriorityZoneA != _radioSyncSnapshot.PriorityZoneA;
    public bool IsPriorityZoneBPendingRadioWrite => _radioSyncSnapshot is null || PriorityZoneB != _radioSyncSnapshot.PriorityZoneB;
    public bool IsMuteTimingPendingRadioWrite => _radioSyncSnapshot is null || MuteTiming != _radioSyncSnapshot.MuteTiming;
    public bool IsEncryptionTypePendingRadioWrite => _radioSyncSnapshot is null || EncryptionType != _radioSyncSnapshot.EncryptionType;
    public bool IsTotPredictPendingRadioWrite => _radioSyncSnapshot is null || TotPredict != _radioSyncSnapshot.TotPredict;
    public bool IsTxPowerAgcPendingRadioWrite => _radioSyncSnapshot is null || TxPowerAgc != _radioSyncSnapshot.TxPowerAgc;
    public bool IsNoaaMoniPendingRadioWrite => _radioSyncSnapshot is null || NoaaMoni != _radioSyncSnapshot.NoaaMoni;
    public bool IsNoaaScanPendingRadioWrite => _radioSyncSnapshot is null || NoaaScan != _radioSyncSnapshot.NoaaScan;
    public bool IsNoaaPendingRadioWrite => _radioSyncSnapshot is null || Noaa != _radioSyncSnapshot.Noaa;
    public bool IsNoaaChannelPendingRadioWrite => _radioSyncSnapshot is null || NoaaChannel != _radioSyncSnapshot.NoaaChannel;
    public bool IsGroupCallHoldTimePendingRadioWrite => _radioSyncSnapshot is null || GroupCallHoldTime != _radioSyncSnapshot.GroupCallHoldTime;
    public bool IsPrivateCallHoldTimePendingRadioWrite => _radioSyncSnapshot is null || PrivateCallHoldTime != _radioSyncSnapshot.PrivateCallHoldTime;
    public bool IsManualDialGroupCallHoldTimePendingRadioWrite => _radioSyncSnapshot is null || ManualDialGroupCallHoldTime != _radioSyncSnapshot.ManualDialGroupCallHoldTime;
    public bool IsManualDialPrivateCallHoldTimePendingRadioWrite => _radioSyncSnapshot is null || ManualDialPrivateCallHoldTime != _radioSyncSnapshot.ManualDialPrivateCallHoldTime;
    public bool IsVoiceHeaderRepetitionsPendingRadioWrite => _radioSyncSnapshot is null || VoiceHeaderRepetitions != _radioSyncSnapshot.VoiceHeaderRepetitions;
    public bool IsTxPreambleDurationPendingRadioWrite => _radioSyncSnapshot is null || TxPreambleDuration != _radioSyncSnapshot.TxPreambleDuration;
    public bool IsFilterOwnIdPendingRadioWrite => _radioSyncSnapshot is null || FilterOwnId != _radioSyncSnapshot.FilterOwnId;
    public bool IsDigitalRemoteKillPendingRadioWrite => _radioSyncSnapshot is null || DigitalRemoteKill != _radioSyncSnapshot.DigitalRemoteKill;
    public bool IsDigitalMonitorPendingRadioWrite => _radioSyncSnapshot is null || DigitalMonitor != _radioSyncSnapshot.DigitalMonitor;
    public bool IsDigitalMonitorCcPendingRadioWrite => _radioSyncSnapshot is null || DigitalMonitorCc != _radioSyncSnapshot.DigitalMonitorCc;
    public bool IsDigitalMonitorIdPendingRadioWrite => _radioSyncSnapshot is null || DigitalMonitorId != _radioSyncSnapshot.DigitalMonitorId;
    public bool IsMonitorSlotHoldPendingRadioWrite => _radioSyncSnapshot is null || MonitorSlotHold != _radioSyncSnapshot.MonitorSlotHold;
    public bool IsRemoteMonitorPendingRadioWrite => _radioSyncSnapshot is null || RemoteMonitor != _radioSyncSnapshot.RemoteMonitor;
    public bool IsSmsFormatPendingRadioWrite => _radioSyncSnapshot is null || SmsFormat != _radioSyncSnapshot.SmsFormat;
    public bool IsResetDigitalProtocolPendingRadioWrite => _radioSyncSnapshot is null || ResetDigitalProtocol != _radioSyncSnapshot.ResetDigitalProtocol;
    public bool IsGpsPositioningPendingRadioWrite => _radioSyncSnapshot is null || GpsPositioning != _radioSyncSnapshot.GpsPositioning;
    public bool IsTimeZonePendingRadioWrite => _radioSyncSnapshot is null || TimeZone != _radioSyncSnapshot.TimeZone;
    public bool IsGpsModePendingRadioWrite => _radioSyncSnapshot is null || GpsMode != _radioSyncSnapshot.GpsMode;
    public bool IsVfoScanTypePendingRadioWrite => _radioSyncSnapshot is null || VfoScanType != _radioSyncSnapshot.VfoScanType;
    public bool IsVfoScanStartFreqUhfPendingRadioWrite => _radioSyncSnapshot is null || VfoScanStartFreqUhf != _radioSyncSnapshot.VfoScanStartFreqUhf;
    public bool IsVfoScanEndFreqUhfPendingRadioWrite => _radioSyncSnapshot is null || VfoScanEndFreqUhf != _radioSyncSnapshot.VfoScanEndFreqUhf;
    public bool IsVfoScanStartFreqVhfPendingRadioWrite => _radioSyncSnapshot is null || VfoScanStartFreqVhf != _radioSyncSnapshot.VfoScanStartFreqVhf;
    public bool IsVfoScanEndFreqVhfPendingRadioWrite => _radioSyncSnapshot is null || VfoScanEndFreqVhf != _radioSyncSnapshot.VfoScanEndFreqVhf;
    public bool IsAutoRepeaterAPendingRadioWrite => _radioSyncSnapshot is null || AutoRepeaterA != _radioSyncSnapshot.AutoRepeaterA;
    public bool IsAutoRepeaterBPendingRadioWrite => _radioSyncSnapshot is null || AutoRepeaterB != _radioSyncSnapshot.AutoRepeaterB;
    public bool IsAutoRepeater1UhfPendingRadioWrite => _radioSyncSnapshot is null || AutoRepeater1Uhf != _radioSyncSnapshot.AutoRepeater1Uhf;
    public bool IsAutoRepeater1VhfPendingRadioWrite => _radioSyncSnapshot is null || AutoRepeater1Vhf != _radioSyncSnapshot.AutoRepeater1Vhf;
    public bool IsAutoRepeater2UhfPendingRadioWrite => _radioSyncSnapshot is null || AutoRepeater2Uhf != _radioSyncSnapshot.AutoRepeater2Uhf;
    public bool IsAutoRepeater2VhfPendingRadioWrite => _radioSyncSnapshot is null || AutoRepeater2Vhf != _radioSyncSnapshot.AutoRepeater2Vhf;
    public bool IsRepeaterCheckPendingRadioWrite => _radioSyncSnapshot is null || RepeaterCheck != _radioSyncSnapshot.RepeaterCheck;
    public bool IsRepeaterCheckIntervalPendingRadioWrite => _radioSyncSnapshot is null || RepeaterCheckInterval != _radioSyncSnapshot.RepeaterCheckInterval;
    public bool IsRepeaterCheckReconnectionsPendingRadioWrite => _radioSyncSnapshot is null || RepeaterCheckReconnections != _radioSyncSnapshot.RepeaterCheckReconnections;
    public bool IsRepeaterOutOfRangeNotifyPendingRadioWrite => _radioSyncSnapshot is null || RepeaterOutOfRangeNotify != _radioSyncSnapshot.RepeaterOutOfRangeNotify;
    public bool IsOutOfRangeNotifyPendingRadioWrite => _radioSyncSnapshot is null || OutOfRangeNotify != _radioSyncSnapshot.OutOfRangeNotify;
    public bool IsAutoRoamingPendingRadioWrite => _radioSyncSnapshot is null || AutoRoaming != _radioSyncSnapshot.AutoRoaming;
    public bool IsAutoRoamingStartConditionPendingRadioWrite => _radioSyncSnapshot is null || AutoRoamingStartCondition != _radioSyncSnapshot.AutoRoamingStartCondition;
    public bool IsAutoRoamingFixedTimePendingRadioWrite => _radioSyncSnapshot is null || AutoRoamingFixedTime != _radioSyncSnapshot.AutoRoamingFixedTime;
    public bool IsRoamingEffectWaitTimePendingRadioWrite => _radioSyncSnapshot is null || RoamingEffectWaitTime != _radioSyncSnapshot.RoamingEffectWaitTime;
    public bool IsAutoRepeater1MinFreqVhfPendingRadioWrite => _radioSyncSnapshot is null || AutoRepeater1MinFreqVhf != _radioSyncSnapshot.AutoRepeater1MinFreqVhf;
    public bool IsAutoRepeater1MaxFreqVhfPendingRadioWrite => _radioSyncSnapshot is null || AutoRepeater1MaxFreqVhf != _radioSyncSnapshot.AutoRepeater1MaxFreqVhf;
    public bool IsAutoRepeater1MinFreqUhfPendingRadioWrite => _radioSyncSnapshot is null || AutoRepeater1MinFreqUhf != _radioSyncSnapshot.AutoRepeater1MinFreqUhf;
    public bool IsAutoRepeater1MaxFreqUhfPendingRadioWrite => _radioSyncSnapshot is null || AutoRepeater1MaxFreqUhf != _radioSyncSnapshot.AutoRepeater1MaxFreqUhf;
    public bool IsAutoRepeater2MinFreqVhfPendingRadioWrite => _radioSyncSnapshot is null || AutoRepeater2MinFreqVhf != _radioSyncSnapshot.AutoRepeater2MinFreqVhf;
    public bool IsAutoRepeater2MaxFreqVhfPendingRadioWrite => _radioSyncSnapshot is null || AutoRepeater2MaxFreqVhf != _radioSyncSnapshot.AutoRepeater2MaxFreqVhf;
    public bool IsAutoRepeater2MinFreqUhfPendingRadioWrite => _radioSyncSnapshot is null || AutoRepeater2MinFreqUhf != _radioSyncSnapshot.AutoRepeater2MinFreqUhf;
    public bool IsAutoRepeater2MaxFreqUhfPendingRadioWrite => _radioSyncSnapshot is null || AutoRepeater2MaxFreqUhf != _radioSyncSnapshot.AutoRepeater2MaxFreqUhf;
    public bool IsRepeaterModePendingRadioWrite => _radioSyncSnapshot is null || RepeaterMode != _radioSyncSnapshot.RepeaterMode;
    public bool IsRepCcLimitPendingRadioWrite => _radioSyncSnapshot is null || RepCcLimit != _radioSyncSnapshot.RepCcLimit;
    public bool IsRepSlotAPendingRadioWrite => _radioSyncSnapshot is null || RepSlotA != _radioSyncSnapshot.RepSlotA;
    public bool IsRepSlotBPendingRadioWrite => _radioSyncSnapshot is null || RepSlotB != _radioSyncSnapshot.RepSlotB;
    public bool IsRepeaterWhitelistPendingRadioWrite => _radioSyncSnapshot is null || RepeaterWhitelist != _radioSyncSnapshot.RepeaterWhitelist;
    public bool IsRecordFunctionPendingRadioWrite => _radioSyncSnapshot is null || RecordFunction != _radioSyncSnapshot.RecordFunction;
    public bool IsRecordDelayPendingRadioWrite => _radioSyncSnapshot is null || RecordDelay != _radioSyncSnapshot.RecordDelay;
    public bool IsMaxVolumePendingRadioWrite => _radioSyncSnapshot is null || MaxVolume != _radioSyncSnapshot.MaxVolume;
    public bool IsPowerOnVolumeTypePendingRadioWrite => _radioSyncSnapshot is null || PowerOnVolumeType != _radioSyncSnapshot.PowerOnVolumeType;
    public bool IsPowerOnVolumePendingRadioWrite => _radioSyncSnapshot is null || PowerOnVolume != _radioSyncSnapshot.PowerOnVolume;
    public bool IsMaxHeadphoneVolumePendingRadioWrite => _radioSyncSnapshot is null || MaxHeadphoneVolume != _radioSyncSnapshot.MaxHeadphoneVolume;
    public bool IsDigiMicGainPendingRadioWrite => _radioSyncSnapshot is null || DigiMicGain != _radioSyncSnapshot.DigiMicGain;
    public bool IsEnhancedSoundQualityPendingRadioWrite => _radioSyncSnapshot is null || EnhancedSoundQuality != _radioSyncSnapshot.EnhancedSoundQuality;
    public bool IsAnalogMicGainPendingRadioWrite => _radioSyncSnapshot is null || AnalogMicGain != _radioSyncSnapshot.AnalogMicGain;
    public bool IsRxAgcPendingRadioWrite => _radioSyncSnapshot is null || RxAgc != _radioSyncSnapshot.RxAgc;
    public bool IsNxMicGainPendingRadioWrite => _radioSyncSnapshot is null || NxMicGain != _radioSyncSnapshot.NxMicGain;
    public bool IsSubSpkInTxPendingRadioWrite => _radioSyncSnapshot is null || SubSpkInTx != _radioSyncSnapshot.SubSpkInTx;
    public bool IsRxNoiseReductionPendingRadioWrite => _radioSyncSnapshot is null || RxNoiseReduction != _radioSyncSnapshot.RxNoiseReduction;
    public bool IsTxNoiseReductionPendingRadioWrite => _radioSyncSnapshot is null || TxNoiseReduction != _radioSyncSnapshot.TxNoiseReduction;
    public bool IsSatLocationPendingRadioWrite => _radioSyncSnapshot is null || SatLocation != _radioSyncSnapshot.SatLocation;
    public bool IsSatTxPowerPendingRadioWrite => _radioSyncSnapshot is null || SatTxPower != _radioSyncSnapshot.SatTxPower;
    public bool IsSatAnaSqlPendingRadioWrite => _radioSyncSnapshot is null || SatAnaSql != _radioSyncSnapshot.SatAnaSql;
    public bool IsSatAosLimitPendingRadioWrite => _radioSyncSnapshot is null || SatAosLimit != _radioSyncSnapshot.SatAosLimit;
    public bool IsRoamingZonePendingRadioWrite => _radioSyncSnapshot is null || RoamingZone != _radioSyncSnapshot.RoamingZone;
    public bool IsKeyLockPendingRadioWrite => _radioSyncSnapshot is null || KeyLock != _radioSyncSnapshot.KeyLock;
    public bool IsPf1ShortKeyPendingRadioWrite => _radioSyncSnapshot is null || Pf1ShortKey != _radioSyncSnapshot.Pf1ShortKey;
    public bool IsPf2ShortKeyPendingRadioWrite => _radioSyncSnapshot is null || Pf2ShortKey != _radioSyncSnapshot.Pf2ShortKey;
    public bool IsPf3ShortKeyPendingRadioWrite => _radioSyncSnapshot is null || Pf3ShortKey != _radioSyncSnapshot.Pf3ShortKey;
    public bool IsP1ShortKeyPendingRadioWrite => _radioSyncSnapshot is null || P1ShortKey != _radioSyncSnapshot.P1ShortKey;
    public bool IsP2ShortKeyPendingRadioWrite => _radioSyncSnapshot is null || P2ShortKey != _radioSyncSnapshot.P2ShortKey;
    public bool IsPf1LongKeyPendingRadioWrite => _radioSyncSnapshot is null || Pf1LongKey != _radioSyncSnapshot.Pf1LongKey;
    public bool IsPf2LongKeyPendingRadioWrite => _radioSyncSnapshot is null || Pf2LongKey != _radioSyncSnapshot.Pf2LongKey;
    public bool IsPf3LongKeyPendingRadioWrite => _radioSyncSnapshot is null || Pf3LongKey != _radioSyncSnapshot.Pf3LongKey;
    public bool IsP1LongKeyPendingRadioWrite => _radioSyncSnapshot is null || P1LongKey != _radioSyncSnapshot.P1LongKey;
    public bool IsP2LongKeyPendingRadioWrite => _radioSyncSnapshot is null || P2LongKey != _radioSyncSnapshot.P2LongKey;
    public bool IsLongKeyTimePendingRadioWrite => _radioSyncSnapshot is null || LongKeyTime != _radioSyncSnapshot.LongKeyTime;
    public bool IsKnobLockPendingRadioWrite => _radioSyncSnapshot is null || KnobLock != _radioSyncSnapshot.KnobLock;
    public bool IsKeyboardLockPendingRadioWrite => _radioSyncSnapshot is null || KeyboardLock != _radioSyncSnapshot.KeyboardLock;
    public bool IsSideKeyLockPendingRadioWrite => _radioSyncSnapshot is null || SideKeyLock != _radioSyncSnapshot.SideKeyLock;
    public bool IsForcedKeyLockPendingRadioWrite => _radioSyncSnapshot is null || ForcedKeyLock != _radioSyncSnapshot.ForcedKeyLock;
    public bool IsAmFmFunctionPendingRadioWrite => _radioSyncSnapshot is null || AmFmFunction != _radioSyncSnapshot.AmFmFunction;
    public bool IsFmVfoMemPendingRadioWrite => _radioSyncSnapshot is null || FmVfoMem != _radioSyncSnapshot.FmVfoMem;
    public bool IsFmWorkChannelPendingRadioWrite => _radioSyncSnapshot is null || FmWorkChannel != _radioSyncSnapshot.FmWorkChannel;
    public bool IsFmMonitorPendingRadioWrite => _radioSyncSnapshot is null || FmMonitor != _radioSyncSnapshot.FmMonitor;
    public bool IsAmVfoMemPendingRadioWrite => _radioSyncSnapshot is null || AmVfoMem != _radioSyncSnapshot.AmVfoMem;
    public bool IsAmOffsetPendingRadioWrite => _radioSyncSnapshot is null || AmOffset != _radioSyncSnapshot.AmOffset;
    public bool IsAmSqlLevelPendingRadioWrite => _radioSyncSnapshot is null || AmSqlLevel != _radioSyncSnapshot.AmSqlLevel;
    public bool IsFrequencyStepPendingRadioWrite => _radioSyncSnapshot is null || FrequencyStep != _radioSyncSnapshot.FrequencyStep;

    public bool HasAnyPendingRadioWrite =>
        IsPowerOnInterfacePendingRadioWrite
        || IsPowerOnDisplayLine1PendingRadioWrite
        || IsPowerOnDisplayLine2PendingRadioWrite
        || IsPowerOnPasswordPendingRadioWrite
        || IsPowerOnPasswordCharPendingRadioWrite
        || IsDefaultStartupChannelPendingRadioWrite
        || IsStartupZoneAPendingRadioWrite
        || IsStartupChannelAPendingRadioWrite
        || IsStartupZoneBPendingRadioWrite
        || IsStartupChannelBPendingRadioWrite
        || IsStartupResetPendingRadioWrite
        || IsSmsAlertPendingRadioWrite
        || IsCallAlertPendingRadioWrite
        || IsDigiCallResetTonePendingRadioWrite
        || IsTalkPermitPendingRadioWrite
        || IsKeyTonePendingRadioWrite
        || IsDigiIdleChannelTonePendingRadioWrite
        || IsStartupSoundPendingRadioWrite
        || IsAnalogIdleChannelTonePendingRadioWrite
        || IsAutoShutdownPendingRadioWrite
        || IsPowerSavePendingRadioWrite
        || IsAutoShutdownTypePendingRadioWrite
        || IsBrightnessPendingRadioWrite
        || IsAutoBacklightDurationPendingRadioWrite
        || IsBacklightTxDelayPendingRadioWrite
        || IsMenuExitTimePendingRadioWrite
        || IsTimeDisplayPendingRadioWrite
        || IsLastCallerPendingRadioWrite
        || IsCallDisplayModePendingRadioWrite
        || IsCallsignDisplayColorPendingRadioWrite
        || IsCallEndPromptBoxPendingRadioWrite
        || IsDisplayChannelNumberPendingRadioWrite
        || IsDisplayCurrentContactPendingRadioWrite
        || IsStandbyCharColorPendingRadioWrite
        || IsStandbyBkPicturePendingRadioWrite
        || IsShowLastCallOnLaunchPendingRadioWrite
        || IsSeparateDisplayPendingRadioWrite
        || IsChSwitchingKeepsCallerPendingRadioWrite
        || IsBacklightRxDelayPendingRadioWrite
        || IsChannelNameColorAPendingRadioWrite
        || IsChannelNameColorBPendingRadioWrite
        || IsZoneNameColorAPendingRadioWrite
        || IsZoneNameColorBPendingRadioWrite
        || IsDisplayChannelTypePendingRadioWrite
        || IsDisplayTimeSlotPendingRadioWrite
        || IsDisplayColorCodePendingRadioWrite
        || IsDateDisplayFormatPendingRadioWrite
        || IsVolumeBarPendingRadioWrite
        || IsNightModePendingRadioWrite
        || IsDisplayModePendingRadioWrite
        || IsVfMrAPendingRadioWrite
        || IsMemZoneAPendingRadioWrite
        || IsVfMrBPendingRadioWrite
        || IsMemZoneBPendingRadioWrite
        || IsMainChannelSetPendingRadioWrite
        || IsSubChannelModePendingRadioWrite
        || IsWorkingModePendingRadioWrite
        || IsVoxLevelPendingRadioWrite
        || IsVoxDelayPendingRadioWrite
        || IsVoxDetectionPendingRadioWrite
        || IsSteTypeOfCtcssPendingRadioWrite
        || IsSteWhenNoSignalPendingRadioWrite
        || IsSteTimePendingRadioWrite
        || IsAmFmFunctionPendingRadioWrite
        || IsFmVfoMemPendingRadioWrite
        || IsFmWorkChannelPendingRadioWrite
        || IsFmMonitorPendingRadioWrite
        || IsAmVfoMemPendingRadioWrite
        || IsAmOffsetPendingRadioWrite
        || IsAmSqlLevelPendingRadioWrite
        || IsFrequencyStepPendingRadioWrite
        || IsKeyLockPendingRadioWrite
        || IsPf1ShortKeyPendingRadioWrite
        || IsPf2ShortKeyPendingRadioWrite
        || IsPf3ShortKeyPendingRadioWrite
        || IsP1ShortKeyPendingRadioWrite
        || IsP2ShortKeyPendingRadioWrite
        || IsPf1LongKeyPendingRadioWrite
        || IsPf2LongKeyPendingRadioWrite
        || IsPf3LongKeyPendingRadioWrite
        || IsP1LongKeyPendingRadioWrite
        || IsP2LongKeyPendingRadioWrite
        || IsLongKeyTimePendingRadioWrite
        || IsKnobLockPendingRadioWrite
        || IsKeyboardLockPendingRadioWrite
        || IsSideKeyLockPendingRadioWrite
        || IsForcedKeyLockPendingRadioWrite
        || IsAddressBookSentWithCodePendingRadioWrite
        || IsTotPendingRadioWrite
        || IsLanguagePendingRadioWrite
        || IsGeneralFrequencyStepPendingRadioWrite
        || IsSqlLevelAPendingRadioWrite
        || IsSqlLevelBPendingRadioWrite
        || IsTbstPendingRadioWrite
        || IsAnalogCallHoldTimePendingRadioWrite
        || IsCallChannelMaintainedPendingRadioWrite
        || IsPriorityZoneAPendingRadioWrite
        || IsPriorityZoneBPendingRadioWrite
        || IsMuteTimingPendingRadioWrite
        || IsEncryptionTypePendingRadioWrite
        || IsTotPredictPendingRadioWrite
        || IsTxPowerAgcPendingRadioWrite
        || IsNoaaMoniPendingRadioWrite
        || IsNoaaScanPendingRadioWrite
        || IsNoaaPendingRadioWrite
        || IsNoaaChannelPendingRadioWrite
        || IsGroupCallHoldTimePendingRadioWrite
        || IsPrivateCallHoldTimePendingRadioWrite
        || IsManualDialGroupCallHoldTimePendingRadioWrite
        || IsManualDialPrivateCallHoldTimePendingRadioWrite
        || IsVoiceHeaderRepetitionsPendingRadioWrite
        || IsTxPreambleDurationPendingRadioWrite
        || IsFilterOwnIdPendingRadioWrite
        || IsDigitalRemoteKillPendingRadioWrite
        || IsDigitalMonitorPendingRadioWrite
        || IsDigitalMonitorCcPendingRadioWrite
        || IsDigitalMonitorIdPendingRadioWrite
        || IsMonitorSlotHoldPendingRadioWrite
        || IsRemoteMonitorPendingRadioWrite
        || IsSmsFormatPendingRadioWrite
        || IsResetDigitalProtocolPendingRadioWrite
        || IsGpsPositioningPendingRadioWrite
        || IsTimeZonePendingRadioWrite
        || IsGpsModePendingRadioWrite
        || IsVfoScanTypePendingRadioWrite
        || IsVfoScanStartFreqUhfPendingRadioWrite
        || IsVfoScanEndFreqUhfPendingRadioWrite
        || IsVfoScanStartFreqVhfPendingRadioWrite
        || IsVfoScanEndFreqVhfPendingRadioWrite
        || IsAutoRepeaterAPendingRadioWrite
        || IsAutoRepeaterBPendingRadioWrite
        || IsAutoRepeater1UhfPendingRadioWrite
        || IsAutoRepeater1VhfPendingRadioWrite
        || IsAutoRepeater2UhfPendingRadioWrite
        || IsAutoRepeater2VhfPendingRadioWrite
        || IsRepeaterCheckPendingRadioWrite
        || IsRepeaterCheckIntervalPendingRadioWrite
        || IsRepeaterCheckReconnectionsPendingRadioWrite
        || IsRepeaterOutOfRangeNotifyPendingRadioWrite
        || IsOutOfRangeNotifyPendingRadioWrite
        || IsAutoRoamingPendingRadioWrite
        || IsAutoRoamingStartConditionPendingRadioWrite
        || IsAutoRoamingFixedTimePendingRadioWrite
        || IsRoamingEffectWaitTimePendingRadioWrite
        || IsAutoRepeater1MinFreqVhfPendingRadioWrite
        || IsAutoRepeater1MaxFreqVhfPendingRadioWrite
        || IsAutoRepeater1MinFreqUhfPendingRadioWrite
        || IsAutoRepeater1MaxFreqUhfPendingRadioWrite
        || IsAutoRepeater2MinFreqVhfPendingRadioWrite
        || IsAutoRepeater2MaxFreqVhfPendingRadioWrite
        || IsAutoRepeater2MinFreqUhfPendingRadioWrite
        || IsAutoRepeater2MaxFreqUhfPendingRadioWrite
        || IsRepeaterModePendingRadioWrite
        || IsRepCcLimitPendingRadioWrite
        || IsRepSlotAPendingRadioWrite
        || IsRepSlotBPendingRadioWrite
        || IsRepeaterWhitelistPendingRadioWrite
        || IsRecordFunctionPendingRadioWrite
        || IsRecordDelayPendingRadioWrite
        || IsMaxVolumePendingRadioWrite
        || IsPowerOnVolumeTypePendingRadioWrite
        || IsPowerOnVolumePendingRadioWrite
        || IsMaxHeadphoneVolumePendingRadioWrite
        || IsDigiMicGainPendingRadioWrite
        || IsEnhancedSoundQualityPendingRadioWrite
        || IsAnalogMicGainPendingRadioWrite
        || IsRxAgcPendingRadioWrite
        || IsNxMicGainPendingRadioWrite
        || IsSubSpkInTxPendingRadioWrite
        || IsRxNoiseReductionPendingRadioWrite
        || IsTxNoiseReductionPendingRadioWrite
        || IsSatLocationPendingRadioWrite
        || IsSatTxPowerPendingRadioWrite
        || IsSatAnaSqlPendingRadioWrite
        || IsSatAosLimitPendingRadioWrite
        || IsRoamingZonePendingRadioWrite;

    /// <summary>Establishes the radio-write baseline - call after a
    /// successful Read From Radio (baseline = what the radio has now) or a
    /// successful Write (baseline = what was just confirmed written).
    /// Deliberately never called by project Save.</summary>
    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateSnapshot();
        NotifyPendingRadioWriteProperties();
    }

    private PowerOnSnapshot CreateSnapshot() => new(
        PowerOnInterface,
        PowerOnDisplayLine1,
        PowerOnDisplayLine2,
        PowerOnPassword,
        PowerOnPasswordChar,
        DefaultStartupChannel,
        StartupZoneA,
        StartupChannelA,
        StartupZoneB,
        StartupChannelB,
        StartupReset,
        SmsAlert,
        CallAlert,
        DigiCallResetTone,
        TalkPermit,
        KeyTone,
        DigiIdleChannelTone,
        StartupSound,
        AnalogIdleChannelTone,
        AutoShutdown,
        PowerSave,
        AutoShutdownType,
        Brightness,
        AutoBacklightDuration,
        BacklightTxDelay,
        MenuExitTime,
        TimeDisplay,
        LastCaller,
        CallDisplayMode,
        CallsignDisplayColor,
        CallEndPromptBox,
        DisplayChannelNumber,
        DisplayCurrentContact,
        StandbyCharColor,
        StandbyBkPicture,
        ShowLastCallOnLaunch,
        SeparateDisplay,
        ChSwitchingKeepsCaller,
        BacklightRxDelay,
        ChannelNameColorA,
        ChannelNameColorB,
        ZoneNameColorA,
        ZoneNameColorB,
        DisplayChannelType,
        DisplayTimeSlot,
        DisplayColorCode,
        DateDisplayFormat,
        VolumeBar,
        NightMode,
        DisplayMode,
        VfMrA,
        MemZoneA,
        VfMrB,
        MemZoneB,
        MainChannelSet,
        SubChannelMode,
        WorkingMode,
        VoxLevel,
        VoxDelay,
        VoxDetection,
        SteTypeOfCtcss,
        SteWhenNoSignal,
        SteTime,
        AmFmFunction,
        FmVfoMem,
        FmWorkChannel,
        FmMonitor,
        AmVfoMem,
        AmOffset,
        AmSqlLevel,
        FrequencyStep,
        KeyLock,
        Pf1ShortKey,
        Pf2ShortKey,
        Pf3ShortKey,
        P1ShortKey,
        P2ShortKey,
        Pf1LongKey,
        Pf2LongKey,
        Pf3LongKey,
        P1LongKey,
        P2LongKey,
        LongKeyTime,
        KnobLock,
        KeyboardLock,
        SideKeyLock,
        ForcedKeyLock,
        AddressBookSentWithCode,
        Tot,
        Language,
        GeneralFrequencyStep,
        SqlLevelA,
        SqlLevelB,
        Tbst,
        AnalogCallHoldTime,
        CallChannelMaintained,
        PriorityZoneA,
        PriorityZoneB,
        MuteTiming,
        EncryptionType,
        TotPredict,
        TxPowerAgc,
        NoaaMoni,
        NoaaScan,
        Noaa,
        NoaaChannel,
        GroupCallHoldTime,
        PrivateCallHoldTime,
        ManualDialGroupCallHoldTime,
        ManualDialPrivateCallHoldTime,
        VoiceHeaderRepetitions,
        TxPreambleDuration,
        FilterOwnId,
        DigitalRemoteKill,
        DigitalMonitor,
        DigitalMonitorCc,
        DigitalMonitorId,
        MonitorSlotHold,
        RemoteMonitor,
        SmsFormat,
        ResetDigitalProtocol,
        GpsPositioning,
        TimeZone,
        GpsMode,
        VfoScanType,
        VfoScanStartFreqUhf,
        VfoScanEndFreqUhf,
        VfoScanStartFreqVhf,
        VfoScanEndFreqVhf,
        AutoRepeaterA,
        AutoRepeaterB,
        AutoRepeater1Uhf,
        AutoRepeater1Vhf,
        AutoRepeater2Uhf,
        AutoRepeater2Vhf,
        RepeaterCheck,
        RepeaterCheckInterval,
        RepeaterCheckReconnections,
        RepeaterOutOfRangeNotify,
        OutOfRangeNotify,
        AutoRoaming,
        AutoRoamingStartCondition,
        AutoRoamingFixedTime,
        RoamingEffectWaitTime,
        AutoRepeater1MinFreqVhf,
        AutoRepeater1MaxFreqVhf,
        AutoRepeater1MinFreqUhf,
        AutoRepeater1MaxFreqUhf,
        AutoRepeater2MinFreqVhf,
        AutoRepeater2MaxFreqVhf,
        AutoRepeater2MinFreqUhf,
        AutoRepeater2MaxFreqUhf,
        RepeaterMode,
        RepCcLimit,
        RepSlotA,
        RepSlotB,
        RepeaterWhitelist,
        RecordFunction,
        RecordDelay,
        MaxVolume,
        PowerOnVolumeType,
        PowerOnVolume,
        MaxHeadphoneVolume,
        DigiMicGain,
        EnhancedSoundQuality,
        AnalogMicGain,
        RxAgc,
        NxMicGain,
        SubSpkInTx,
        RxNoiseReduction,
        TxNoiseReduction,
        SatLocation,
        SatTxPower,
        SatAnaSql,
        SatAosLimit,
        RoamingZone);

    private void NotifyPendingRadioWriteProperties()
    {
        OnPropertyChanged(nameof(IsPowerOnInterfacePendingRadioWrite));
        OnPropertyChanged(nameof(IsPowerOnDisplayLine1PendingRadioWrite));
        OnPropertyChanged(nameof(IsPowerOnDisplayLine2PendingRadioWrite));
        OnPropertyChanged(nameof(IsPowerOnPasswordPendingRadioWrite));
        OnPropertyChanged(nameof(IsPowerOnPasswordCharPendingRadioWrite));
        OnPropertyChanged(nameof(IsDefaultStartupChannelPendingRadioWrite));
        OnPropertyChanged(nameof(IsStartupZoneAPendingRadioWrite));
        OnPropertyChanged(nameof(IsStartupChannelAPendingRadioWrite));
        OnPropertyChanged(nameof(IsStartupZoneBPendingRadioWrite));
        OnPropertyChanged(nameof(IsStartupChannelBPendingRadioWrite));
        OnPropertyChanged(nameof(IsStartupResetPendingRadioWrite));
        OnPropertyChanged(nameof(IsSmsAlertPendingRadioWrite));
        OnPropertyChanged(nameof(IsCallAlertPendingRadioWrite));
        OnPropertyChanged(nameof(IsDigiCallResetTonePendingRadioWrite));
        OnPropertyChanged(nameof(IsTalkPermitPendingRadioWrite));
        OnPropertyChanged(nameof(IsKeyTonePendingRadioWrite));
        OnPropertyChanged(nameof(IsDigiIdleChannelTonePendingRadioWrite));
        OnPropertyChanged(nameof(IsStartupSoundPendingRadioWrite));
        OnPropertyChanged(nameof(IsAnalogIdleChannelTonePendingRadioWrite));
        OnPropertyChanged(nameof(IsAutoShutdownPendingRadioWrite));
        OnPropertyChanged(nameof(IsPowerSavePendingRadioWrite));
        OnPropertyChanged(nameof(IsAutoShutdownTypePendingRadioWrite));
        OnPropertyChanged(nameof(IsBrightnessPendingRadioWrite));
        OnPropertyChanged(nameof(IsAutoBacklightDurationPendingRadioWrite));
        OnPropertyChanged(nameof(IsBacklightTxDelayPendingRadioWrite));
        OnPropertyChanged(nameof(IsMenuExitTimePendingRadioWrite));
        OnPropertyChanged(nameof(IsTimeDisplayPendingRadioWrite));
        OnPropertyChanged(nameof(IsLastCallerPendingRadioWrite));
        OnPropertyChanged(nameof(IsCallDisplayModePendingRadioWrite));
        OnPropertyChanged(nameof(IsCallsignDisplayColorPendingRadioWrite));
        OnPropertyChanged(nameof(IsCallEndPromptBoxPendingRadioWrite));
        OnPropertyChanged(nameof(IsDisplayChannelNumberPendingRadioWrite));
        OnPropertyChanged(nameof(IsDisplayCurrentContactPendingRadioWrite));
        OnPropertyChanged(nameof(IsStandbyCharColorPendingRadioWrite));
        OnPropertyChanged(nameof(IsStandbyBkPicturePendingRadioWrite));
        OnPropertyChanged(nameof(IsShowLastCallOnLaunchPendingRadioWrite));
        OnPropertyChanged(nameof(IsSeparateDisplayPendingRadioWrite));
        OnPropertyChanged(nameof(IsChSwitchingKeepsCallerPendingRadioWrite));
        OnPropertyChanged(nameof(IsBacklightRxDelayPendingRadioWrite));
        OnPropertyChanged(nameof(IsChannelNameColorAPendingRadioWrite));
        OnPropertyChanged(nameof(IsChannelNameColorBPendingRadioWrite));
        OnPropertyChanged(nameof(IsZoneNameColorAPendingRadioWrite));
        OnPropertyChanged(nameof(IsZoneNameColorBPendingRadioWrite));
        OnPropertyChanged(nameof(IsDisplayChannelTypePendingRadioWrite));
        OnPropertyChanged(nameof(IsDisplayTimeSlotPendingRadioWrite));
        OnPropertyChanged(nameof(IsDisplayColorCodePendingRadioWrite));
        OnPropertyChanged(nameof(IsDateDisplayFormatPendingRadioWrite));
        OnPropertyChanged(nameof(IsVolumeBarPendingRadioWrite));
        OnPropertyChanged(nameof(IsNightModePendingRadioWrite));
        OnPropertyChanged(nameof(IsDisplayModePendingRadioWrite));
        OnPropertyChanged(nameof(IsVfMrAPendingRadioWrite));
        OnPropertyChanged(nameof(IsMemZoneAPendingRadioWrite));
        OnPropertyChanged(nameof(IsVfMrBPendingRadioWrite));
        OnPropertyChanged(nameof(IsMemZoneBPendingRadioWrite));
        OnPropertyChanged(nameof(IsMainChannelSetPendingRadioWrite));
        OnPropertyChanged(nameof(IsSubChannelModePendingRadioWrite));
        OnPropertyChanged(nameof(IsWorkingModePendingRadioWrite));
        OnPropertyChanged(nameof(IsVoxLevelPendingRadioWrite));
        OnPropertyChanged(nameof(IsVoxDelayPendingRadioWrite));
        OnPropertyChanged(nameof(IsVoxDetectionPendingRadioWrite));
        OnPropertyChanged(nameof(IsSteTypeOfCtcssPendingRadioWrite));
        OnPropertyChanged(nameof(IsSteWhenNoSignalPendingRadioWrite));
        OnPropertyChanged(nameof(IsSteTimePendingRadioWrite));
        OnPropertyChanged(nameof(IsAddressBookSentWithCodePendingRadioWrite));
        OnPropertyChanged(nameof(IsTotPendingRadioWrite));
        OnPropertyChanged(nameof(IsLanguagePendingRadioWrite));
        OnPropertyChanged(nameof(IsGeneralFrequencyStepPendingRadioWrite));
        OnPropertyChanged(nameof(IsSqlLevelAPendingRadioWrite));
        OnPropertyChanged(nameof(IsSqlLevelBPendingRadioWrite));
        OnPropertyChanged(nameof(IsTbstPendingRadioWrite));
        OnPropertyChanged(nameof(IsAnalogCallHoldTimePendingRadioWrite));
        OnPropertyChanged(nameof(IsCallChannelMaintainedPendingRadioWrite));
        OnPropertyChanged(nameof(IsPriorityZoneAPendingRadioWrite));
        OnPropertyChanged(nameof(IsPriorityZoneBPendingRadioWrite));
        OnPropertyChanged(nameof(IsMuteTimingPendingRadioWrite));
        OnPropertyChanged(nameof(IsEncryptionTypePendingRadioWrite));
        OnPropertyChanged(nameof(IsTotPredictPendingRadioWrite));
        OnPropertyChanged(nameof(IsTxPowerAgcPendingRadioWrite));
        OnPropertyChanged(nameof(IsNoaaMoniPendingRadioWrite));
        OnPropertyChanged(nameof(IsNoaaScanPendingRadioWrite));
        OnPropertyChanged(nameof(IsNoaaPendingRadioWrite));
        OnPropertyChanged(nameof(IsNoaaChannelPendingRadioWrite));
        OnPropertyChanged(nameof(IsGroupCallHoldTimePendingRadioWrite));
        OnPropertyChanged(nameof(IsPrivateCallHoldTimePendingRadioWrite));
        OnPropertyChanged(nameof(IsManualDialGroupCallHoldTimePendingRadioWrite));
        OnPropertyChanged(nameof(IsManualDialPrivateCallHoldTimePendingRadioWrite));
        OnPropertyChanged(nameof(IsVoiceHeaderRepetitionsPendingRadioWrite));
        OnPropertyChanged(nameof(IsTxPreambleDurationPendingRadioWrite));
        OnPropertyChanged(nameof(IsFilterOwnIdPendingRadioWrite));
        OnPropertyChanged(nameof(IsDigitalRemoteKillPendingRadioWrite));
        OnPropertyChanged(nameof(IsDigitalMonitorPendingRadioWrite));
        OnPropertyChanged(nameof(IsDigitalMonitorCcPendingRadioWrite));
        OnPropertyChanged(nameof(IsDigitalMonitorIdPendingRadioWrite));
        OnPropertyChanged(nameof(IsMonitorSlotHoldPendingRadioWrite));
        OnPropertyChanged(nameof(IsRemoteMonitorPendingRadioWrite));
        OnPropertyChanged(nameof(IsSmsFormatPendingRadioWrite));
        OnPropertyChanged(nameof(IsResetDigitalProtocolPendingRadioWrite));
        OnPropertyChanged(nameof(IsGpsPositioningPendingRadioWrite));
        OnPropertyChanged(nameof(IsTimeZonePendingRadioWrite));
        OnPropertyChanged(nameof(IsGpsModePendingRadioWrite));
        OnPropertyChanged(nameof(IsVfoScanTypePendingRadioWrite));
        OnPropertyChanged(nameof(IsVfoScanStartFreqUhfPendingRadioWrite));
        OnPropertyChanged(nameof(IsVfoScanEndFreqUhfPendingRadioWrite));
        OnPropertyChanged(nameof(IsVfoScanStartFreqVhfPendingRadioWrite));
        OnPropertyChanged(nameof(IsVfoScanEndFreqVhfPendingRadioWrite));
        OnPropertyChanged(nameof(IsAutoRepeaterAPendingRadioWrite));
        OnPropertyChanged(nameof(IsAutoRepeaterBPendingRadioWrite));
        OnPropertyChanged(nameof(IsAutoRepeater1UhfPendingRadioWrite));
        OnPropertyChanged(nameof(IsAutoRepeater1VhfPendingRadioWrite));
        OnPropertyChanged(nameof(IsAutoRepeater2UhfPendingRadioWrite));
        OnPropertyChanged(nameof(IsAutoRepeater2VhfPendingRadioWrite));
        OnPropertyChanged(nameof(IsRepeaterCheckPendingRadioWrite));
        OnPropertyChanged(nameof(IsRepeaterCheckIntervalPendingRadioWrite));
        OnPropertyChanged(nameof(IsRepeaterCheckReconnectionsPendingRadioWrite));
        OnPropertyChanged(nameof(IsRepeaterOutOfRangeNotifyPendingRadioWrite));
        OnPropertyChanged(nameof(IsOutOfRangeNotifyPendingRadioWrite));
        OnPropertyChanged(nameof(IsAutoRoamingPendingRadioWrite));
        OnPropertyChanged(nameof(IsAutoRoamingStartConditionPendingRadioWrite));
        OnPropertyChanged(nameof(IsAutoRoamingFixedTimePendingRadioWrite));
        OnPropertyChanged(nameof(IsRoamingEffectWaitTimePendingRadioWrite));
        OnPropertyChanged(nameof(IsAutoRepeater1MinFreqVhfPendingRadioWrite));
        OnPropertyChanged(nameof(IsAutoRepeater1MaxFreqVhfPendingRadioWrite));
        OnPropertyChanged(nameof(IsAutoRepeater1MinFreqUhfPendingRadioWrite));
        OnPropertyChanged(nameof(IsAutoRepeater1MaxFreqUhfPendingRadioWrite));
        OnPropertyChanged(nameof(IsAutoRepeater2MinFreqVhfPendingRadioWrite));
        OnPropertyChanged(nameof(IsAutoRepeater2MaxFreqVhfPendingRadioWrite));
        OnPropertyChanged(nameof(IsAutoRepeater2MinFreqUhfPendingRadioWrite));
        OnPropertyChanged(nameof(IsAutoRepeater2MaxFreqUhfPendingRadioWrite));
        OnPropertyChanged(nameof(IsRepeaterModePendingRadioWrite));
        OnPropertyChanged(nameof(IsRepCcLimitPendingRadioWrite));
        OnPropertyChanged(nameof(IsRepSlotAPendingRadioWrite));
        OnPropertyChanged(nameof(IsRepSlotBPendingRadioWrite));
        OnPropertyChanged(nameof(IsRepeaterWhitelistPendingRadioWrite));
        OnPropertyChanged(nameof(IsRecordFunctionPendingRadioWrite));
        OnPropertyChanged(nameof(IsRecordDelayPendingRadioWrite));
        OnPropertyChanged(nameof(IsMaxVolumePendingRadioWrite));
        OnPropertyChanged(nameof(IsPowerOnVolumeTypePendingRadioWrite));
        OnPropertyChanged(nameof(IsPowerOnVolumePendingRadioWrite));
        OnPropertyChanged(nameof(IsMaxHeadphoneVolumePendingRadioWrite));
        OnPropertyChanged(nameof(IsDigiMicGainPendingRadioWrite));
        OnPropertyChanged(nameof(IsEnhancedSoundQualityPendingRadioWrite));
        OnPropertyChanged(nameof(IsAnalogMicGainPendingRadioWrite));
        OnPropertyChanged(nameof(IsRxAgcPendingRadioWrite));
        OnPropertyChanged(nameof(IsNxMicGainPendingRadioWrite));
        OnPropertyChanged(nameof(IsSubSpkInTxPendingRadioWrite));
        OnPropertyChanged(nameof(IsRxNoiseReductionPendingRadioWrite));
        OnPropertyChanged(nameof(IsTxNoiseReductionPendingRadioWrite));
        OnPropertyChanged(nameof(IsSatLocationPendingRadioWrite));
        OnPropertyChanged(nameof(IsSatTxPowerPendingRadioWrite));
        OnPropertyChanged(nameof(IsSatAnaSqlPendingRadioWrite));
        OnPropertyChanged(nameof(IsSatAosLimitPendingRadioWrite));
        OnPropertyChanged(nameof(IsRoamingZonePendingRadioWrite));
        OnPropertyChanged(nameof(IsKeyLockPendingRadioWrite));
        OnPropertyChanged(nameof(IsPf1ShortKeyPendingRadioWrite));
        OnPropertyChanged(nameof(IsPf2ShortKeyPendingRadioWrite));
        OnPropertyChanged(nameof(IsPf3ShortKeyPendingRadioWrite));
        OnPropertyChanged(nameof(IsP1ShortKeyPendingRadioWrite));
        OnPropertyChanged(nameof(IsP2ShortKeyPendingRadioWrite));
        OnPropertyChanged(nameof(IsPf1LongKeyPendingRadioWrite));
        OnPropertyChanged(nameof(IsPf2LongKeyPendingRadioWrite));
        OnPropertyChanged(nameof(IsPf3LongKeyPendingRadioWrite));
        OnPropertyChanged(nameof(IsP1LongKeyPendingRadioWrite));
        OnPropertyChanged(nameof(IsP2LongKeyPendingRadioWrite));
        OnPropertyChanged(nameof(IsLongKeyTimePendingRadioWrite));
        OnPropertyChanged(nameof(IsKnobLockPendingRadioWrite));
        OnPropertyChanged(nameof(IsKeyboardLockPendingRadioWrite));
        OnPropertyChanged(nameof(IsSideKeyLockPendingRadioWrite));
        OnPropertyChanged(nameof(IsForcedKeyLockPendingRadioWrite));
        OnPropertyChanged(nameof(IsAmFmFunctionPendingRadioWrite));
        OnPropertyChanged(nameof(IsFmVfoMemPendingRadioWrite));
        OnPropertyChanged(nameof(IsFmWorkChannelPendingRadioWrite));
        OnPropertyChanged(nameof(IsFmMonitorPendingRadioWrite));
        OnPropertyChanged(nameof(IsAmVfoMemPendingRadioWrite));
        OnPropertyChanged(nameof(IsAmOffsetPendingRadioWrite));
        OnPropertyChanged(nameof(IsAmSqlLevelPendingRadioWrite));
        OnPropertyChanged(nameof(IsFrequencyStepPendingRadioWrite));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    private sealed record PowerOnSnapshot(
        byte PowerOnInterface,
        string PowerOnDisplayLine1,
        string PowerOnDisplayLine2,
        byte PowerOnPassword,
        string PowerOnPasswordChar,
        byte DefaultStartupChannel,
        byte StartupZoneA,
        byte StartupChannelA,
        byte StartupZoneB,
        byte StartupChannelB,
        byte StartupReset,
        byte SmsAlert,
        byte CallAlert,
        byte DigiCallResetTone,
        byte TalkPermit,
        byte KeyTone,
        byte DigiIdleChannelTone,
        byte StartupSound,
        byte AnalogIdleChannelTone,
        byte AutoShutdown,
        byte PowerSave,
        byte AutoShutdownType,
        byte Brightness,
        byte AutoBacklightDuration,
        byte BacklightTxDelay,
        byte MenuExitTime,
        byte TimeDisplay,
        byte LastCaller,
        byte CallDisplayMode,
        byte CallsignDisplayColor,
        byte CallEndPromptBox,
        byte DisplayChannelNumber,
        byte DisplayCurrentContact,
        byte StandbyCharColor,
        byte StandbyBkPicture,
        byte ShowLastCallOnLaunch,
        byte SeparateDisplay,
        byte ChSwitchingKeepsCaller,
        byte BacklightRxDelay,
        byte ChannelNameColorA,
        byte ChannelNameColorB,
        byte ZoneNameColorA,
        byte ZoneNameColorB,
        bool DisplayChannelType,
        bool DisplayTimeSlot,
        bool DisplayColorCode,
        byte DateDisplayFormat,
        byte VolumeBar,
        byte NightMode,
        byte DisplayMode,
        byte VfMrA,
        byte MemZoneA,
        byte VfMrB,
        byte MemZoneB,
        byte MainChannelSet,
        byte SubChannelMode,
        byte WorkingMode,
        byte VoxLevel,
        byte VoxDelay,
        byte VoxDetection,
        byte SteTypeOfCtcss,
        byte SteWhenNoSignal,
        byte SteTime,
        byte AmFmFunction,
        byte FmVfoMem,
        byte FmWorkChannel,
        byte FmMonitor,
        byte AmVfoMem,
        byte AmOffset,
        byte AmSqlLevel,
        byte FrequencyStep,
        byte KeyLock,
        byte Pf1ShortKey,
        byte Pf2ShortKey,
        byte Pf3ShortKey,
        byte P1ShortKey,
        byte P2ShortKey,
        byte Pf1LongKey,
        byte Pf2LongKey,
        byte Pf3LongKey,
        byte P1LongKey,
        byte P2LongKey,
        byte LongKeyTime,
        bool KnobLock,
        bool KeyboardLock,
        bool SideKeyLock,
        bool ForcedKeyLock,
        byte AddressBookSentWithCode,
        byte Tot,
        byte Language,
        byte GeneralFrequencyStep,
        byte SqlLevelA,
        byte SqlLevelB,
        byte Tbst,
        byte AnalogCallHoldTime,
        byte CallChannelMaintained,
        byte PriorityZoneA,
        byte PriorityZoneB,
        byte MuteTiming,
        byte EncryptionType,
        byte TotPredict,
        byte TxPowerAgc,
        byte NoaaMoni,
        byte NoaaScan,
        byte Noaa,
        byte NoaaChannel,
        byte GroupCallHoldTime,
        byte PrivateCallHoldTime,
        byte ManualDialGroupCallHoldTime,
        byte ManualDialPrivateCallHoldTime,
        byte VoiceHeaderRepetitions,
        byte TxPreambleDuration,
        byte FilterOwnId,
        byte DigitalRemoteKill,
        byte DigitalMonitor,
        byte DigitalMonitorCc,
        byte DigitalMonitorId,
        byte MonitorSlotHold,
        byte RemoteMonitor,
        byte SmsFormat,
        byte ResetDigitalProtocol,
        byte GpsPositioning,
        byte TimeZone,
        byte GpsMode,
        byte VfoScanType,
        int VfoScanStartFreqUhf,
        int VfoScanEndFreqUhf,
        int VfoScanStartFreqVhf,
        int VfoScanEndFreqVhf,
        byte AutoRepeaterA,
        byte AutoRepeaterB,
        byte AutoRepeater1Uhf,
        byte AutoRepeater1Vhf,
        byte AutoRepeater2Uhf,
        byte AutoRepeater2Vhf,
        byte RepeaterCheck,
        byte RepeaterCheckInterval,
        byte RepeaterCheckReconnections,
        byte RepeaterOutOfRangeNotify,
        byte OutOfRangeNotify,
        byte AutoRoaming,
        byte AutoRoamingStartCondition,
        byte AutoRoamingFixedTime,
        byte RoamingEffectWaitTime,
        int AutoRepeater1MinFreqVhf,
        int AutoRepeater1MaxFreqVhf,
        int AutoRepeater1MinFreqUhf,
        int AutoRepeater1MaxFreqUhf,
        int AutoRepeater2MinFreqVhf,
        int AutoRepeater2MaxFreqVhf,
        int AutoRepeater2MinFreqUhf,
        int AutoRepeater2MaxFreqUhf,
        byte RepeaterMode,
        byte RepCcLimit,
        byte RepSlotA,
        byte RepSlotB,
        byte RepeaterWhitelist,
        byte RecordFunction,
        byte RecordDelay,
        byte MaxVolume,
        byte PowerOnVolumeType,
        byte PowerOnVolume,
        byte MaxHeadphoneVolume,
        byte DigiMicGain,
        byte EnhancedSoundQuality,
        byte AnalogMicGain,
        byte RxAgc,
        byte NxMicGain,
        byte SubSpkInTx,
        byte RxNoiseReduction,
        byte TxNoiseReduction,
        byte SatLocation,
        byte SatTxPower,
        byte SatAnaSql,
        byte SatAosLimit,
        byte RoamingZone);

    /// <summary>Falls back to the raw number for any byte value beyond the
    /// documented option count - real radio data outside the known enum
    /// shouldn't become invisible/unrecoverable just because this app only
    /// has labels for the documented range.</summary>
    private static string LabelFor(byte value, IReadOnlyList<string> options) =>
        value < options.Count ? options[value] : value.ToString();

    private static byte IndexFor(string value, IReadOnlyList<string> options, byte currentValue)
    {
        var index = options.ToList().IndexOf(value);
        return index >= 0 ? (byte)index : currentValue;
    }

    /// <summary>For fields whose raw byte is NOT a zero-based index into
    /// <paramref name="options"/> - e.g. TgHoldTimeOptions/
    /// VoiceHeaderRepetitionsOptions, where the raw byte is the literal
    /// physical value (seconds, repetition count) and the option list just
    /// happens not to start counting at 0 - confirmed via live differential
    /// writes 2026-07-27/28, same class of encoding as SteTimeText's own
    /// fix. <paramref name="rawOffset"/> is added to the option index to
    /// get the real raw byte (e.g. 1 for "raw = seconds", 2 for "raw =
    /// repetition count starting at 2").</summary>
    private static string OffsetLabelFor(byte value, IReadOnlyList<string> options, int rawOffset)
    {
        var index = value - rawOffset;
        return index >= 0 && index < options.Count ? options[index] : value.ToString();
    }

    private static byte OffsetIndexFor(string value, IReadOnlyList<string> options, int rawOffset, byte currentValue)
    {
        var index = options.ToList().IndexOf(value);
        return index >= 0 ? (byte)(index + rawOffset) : currentValue;
    }

    partial void OnPowerOnInterfaceChanged(byte value)
    {
        OnPropertyChanged(nameof(PowerOnInterfaceText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnPowerOnDisplayLine1Changed(string value) => NotifyPendingRadioWriteProperties();
    partial void OnPowerOnDisplayLine2Changed(string value) => NotifyPendingRadioWriteProperties();

    partial void OnPowerOnPasswordChanged(byte value)
    {
        OnPropertyChanged(nameof(PowerOnPasswordText));
        NotifyPendingRadioWriteProperties();
    }


    partial void OnDefaultStartupChannelChanged(byte value)
    {
        OnPropertyChanged(nameof(DefaultStartupChannelText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnStartupZoneAChanged(byte value) => NotifyPendingRadioWriteProperties();
    partial void OnStartupChannelAChanged(byte value) => NotifyPendingRadioWriteProperties();
    partial void OnStartupZoneBChanged(byte value) => NotifyPendingRadioWriteProperties();
    partial void OnStartupChannelBChanged(byte value) => NotifyPendingRadioWriteProperties();
    partial void OnStartupGpsTestChanged(byte value) => OnPropertyChanged(nameof(StartupGpsTestText));

    partial void OnStartupResetChanged(byte value)
    {
        OnPropertyChanged(nameof(StartupResetText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnSmsAlertChanged(byte value)
    {
        OnPropertyChanged(nameof(SmsAlertText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnCallAlertChanged(byte value)
    {
        OnPropertyChanged(nameof(CallAlertText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnDigiCallResetToneChanged(byte value)
    {
        OnPropertyChanged(nameof(DigiCallResetToneText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnTalkPermitChanged(byte value)
    {
        OnPropertyChanged(nameof(TalkPermitText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnKeyToneChanged(byte value)
    {
        OnPropertyChanged(nameof(KeyToneText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnDigiIdleChannelToneChanged(byte value)
    {
        OnPropertyChanged(nameof(DigiIdleChannelToneText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnStartupSoundChanged(byte value)
    {
        OnPropertyChanged(nameof(StartupSoundText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnAnalogIdleChannelToneChanged(byte value)
    {
        OnPropertyChanged(nameof(AnalogIdleChannelToneText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnVoxLevelChanged(byte value)
    {
        OnPropertyChanged(nameof(VoxLevelText));
        OnPropertyChanged(nameof(IsVoxDetectionEditable));
        OnPropertyChanged(nameof(IsVoxOn));

        // Matches the real vendor CPS: turning Vox On/Off to On doesn't just
        // disable VOX Detection, it also resets it back to its first option
        // ("Built-in Microphone") - confirmed 2026-07-29. Set through the
        // VoxDetection property (not the backing field) so its own change
        // notification/dirty tracking still fires normally.
        if (value != 0)
        {
            VoxDetection = 0;
        }

        NotifyPendingRadioWriteProperties();
    }
    partial void OnLanguageChanged(byte value)
    {
        OnPropertyChanged(nameof(LanguageText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnTimeDisplayChanged(byte value)
    {
        OnPropertyChanged(nameof(TimeDisplayText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnDistanceUnitChanged(byte value) => OnPropertyChanged(nameof(DistanceUnitText));
    partial void OnGpsModeChanged(byte value)
    {
        OnPropertyChanged(nameof(GpsModeText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnEncryptionTypeChanged(byte value)
    {
        OnPropertyChanged(nameof(EncryptionTypeText));
        OnPropertyChanged(nameof(IsAesArc4EncryptionTypeSelected));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnVfMrAChanged(byte value)
    {
        OnPropertyChanged(nameof(VfMrAText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnVfMrBChanged(byte value)
    {
        OnPropertyChanged(nameof(VfMrBText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnAutoShutdownChanged(byte value)
    {
        OnPropertyChanged(nameof(AutoShutdownText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnPowerSaveChanged(byte value)
    {
        OnPropertyChanged(nameof(PowerSaveText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnAutoShutdownTypeChanged(byte value)
    {
        OnPropertyChanged(nameof(AutoShutdownTypeText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnBrightnessChanged(byte value)
    {
        OnPropertyChanged(nameof(BrightnessText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnAutoBacklightDurationChanged(byte value)
    {
        OnPropertyChanged(nameof(AutoBacklightDurationText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnBacklightTxDelayChanged(byte value)
    {
        OnPropertyChanged(nameof(BacklightTxDelayText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnMenuExitTimeChanged(byte value)
    {
        OnPropertyChanged(nameof(MenuExitTimeText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnLastCallerChanged(byte value)
    {
        OnPropertyChanged(nameof(LastCallerText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnCallDisplayModeChanged(byte value)
    {
        OnPropertyChanged(nameof(CallDisplayModeText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnCallsignDisplayColorChanged(byte value)
    {
        OnPropertyChanged(nameof(CallsignDisplayColorText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnCallEndPromptBoxChanged(byte value)
    {
        OnPropertyChanged(nameof(CallEndPromptBoxText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnDisplayChannelNumberChanged(byte value)
    {
        OnPropertyChanged(nameof(DisplayChannelNumberText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnDisplayCurrentContactChanged(byte value)
    {
        OnPropertyChanged(nameof(DisplayCurrentContactText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnStandbyCharColorChanged(byte value)
    {
        OnPropertyChanged(nameof(StandbyCharColorText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnStandbyBkPictureChanged(byte value)
    {
        OnPropertyChanged(nameof(StandbyBkPictureText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnShowLastCallOnLaunchChanged(byte value)
    {
        OnPropertyChanged(nameof(ShowLastCallOnLaunchText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnSeparateDisplayChanged(byte value)
    {
        OnPropertyChanged(nameof(SeparateDisplayText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnChSwitchingKeepsCallerChanged(byte value)
    {
        OnPropertyChanged(nameof(ChSwitchingKeepsCallerText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnBacklightRxDelayChanged(byte value)
    {
        OnPropertyChanged(nameof(BacklightRxDelayText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnChannelNameColorAChanged(byte value)
    {
        OnPropertyChanged(nameof(ChannelNameColorAText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnChannelNameColorBChanged(byte value)
    {
        OnPropertyChanged(nameof(ChannelNameColorBText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnZoneNameColorAChanged(byte value)
    {
        OnPropertyChanged(nameof(ZoneNameColorAText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnZoneNameColorBChanged(byte value)
    {
        OnPropertyChanged(nameof(ZoneNameColorBText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnDateDisplayFormatChanged(byte value)
    {
        OnPropertyChanged(nameof(DateDisplayFormatText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnVolumeBarChanged(byte value)
    {
        OnPropertyChanged(nameof(VolumeBarText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnNightModeChanged(byte value)
    {
        OnPropertyChanged(nameof(NightModeText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnDisplayChannelTypeChanged(bool value) => NotifyPendingRadioWriteProperties();
    partial void OnDisplayTimeSlotChanged(bool value) => NotifyPendingRadioWriteProperties();
    partial void OnDisplayColorCodeChanged(bool value) => NotifyPendingRadioWriteProperties();

    partial void OnDisplayModeChanged(byte value)
    {
        OnPropertyChanged(nameof(DisplayModeText));
        OnPropertyChanged(nameof(IsVfMrEditable));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnMainChannelSetChanged(byte value)
    {
        OnPropertyChanged(nameof(MainChannelSetText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnSubChannelModeChanged(byte value)
    {
        OnPropertyChanged(nameof(SubChannelModeText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnWorkingModeChanged(byte value)
    {
        OnPropertyChanged(nameof(WorkingModeText));
        NotifyPendingRadioWriteProperties();
    }

    partial void OnMemZoneAChanged(byte value) => NotifyPendingRadioWriteProperties();
    partial void OnMemZoneBChanged(byte value) => NotifyPendingRadioWriteProperties();

    partial void OnVoxDelayChanged(byte value)
    {
        OnPropertyChanged(nameof(VoxDelayText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnVoxDetectionChanged(byte value)
    {
        OnPropertyChanged(nameof(VoxDetectionText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnBtOnOffChanged(byte value) => OnPropertyChanged(nameof(BtOnOffText));
    partial void OnBtIntMicChanged(byte value) => OnPropertyChanged(nameof(BtIntMicText));
    partial void OnBtIntSpkChanged(byte value) => OnPropertyChanged(nameof(BtIntSpkText));
    partial void OnBtMicGainChanged(byte value) => OnPropertyChanged(nameof(BtMicGainText));
    partial void OnBtSpkGainChanged(byte value) => OnPropertyChanged(nameof(BtSpkGainText));
    partial void OnBtHoldTimeChanged(byte value) => OnPropertyChanged(nameof(BtHoldTimeText));
    partial void OnBtRxDelayChanged(byte value) => OnPropertyChanged(nameof(BtRxDelayText));
    partial void OnBtPttHoldChanged(byte value) => OnPropertyChanged(nameof(BtPttHoldText));
    partial void OnBtPttSleepTimeChanged(byte value) => OnPropertyChanged(nameof(BtPttSleepTimeText));
    partial void OnBtNrBeforeChanged(byte value) => OnPropertyChanged(nameof(BtNrBeforeText));
    partial void OnBtNrAfterChanged(byte value) => OnPropertyChanged(nameof(BtNrAfterText));
    partial void OnSteTypeOfCtcssChanged(byte value)
    {
        OnPropertyChanged(nameof(SteTypeOfCtcssText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnSteWhenNoSignalChanged(byte value)
    {
        OnPropertyChanged(nameof(SteWhenNoSignalText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnSteTimeChanged(byte value)
    {
        OnPropertyChanged(nameof(SteTimeText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAmFmFunctionChanged(byte value)
    {
        OnPropertyChanged(nameof(AmFmFunctionText));
        OnPropertyChanged(nameof(IsFmSectionEnabled));
        OnPropertyChanged(nameof(IsAmSectionEnabled));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnFmVfoMemChanged(byte value)
    {
        OnPropertyChanged(nameof(FmVfoMemText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnFmWorkChannelChanged(byte value) => NotifyPendingRadioWriteProperties();
    partial void OnFmMonitorChanged(byte value)
    {
        OnPropertyChanged(nameof(FmMonitorText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAmVfoMemChanged(byte value)
    {
        OnPropertyChanged(nameof(AmVfoMemText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAmOffsetChanged(byte value)
    {
        OnPropertyChanged(nameof(AmOffsetText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAmSqlLevelChanged(byte value)
    {
        OnPropertyChanged(nameof(AmSqlLevelText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnKeyLockChanged(byte value)
    {
        OnPropertyChanged(nameof(KeyLockText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnPf1ShortKeyChanged(byte value)
    {
        OnPropertyChanged(nameof(Pf1ShortKeyText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnPf2ShortKeyChanged(byte value)
    {
        OnPropertyChanged(nameof(Pf2ShortKeyText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnPf3ShortKeyChanged(byte value)
    {
        OnPropertyChanged(nameof(Pf3ShortKeyText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnP1ShortKeyChanged(byte value)
    {
        OnPropertyChanged(nameof(P1ShortKeyText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnP2ShortKeyChanged(byte value)
    {
        OnPropertyChanged(nameof(P2ShortKeyText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnPf1LongKeyChanged(byte value)
    {
        OnPropertyChanged(nameof(Pf1LongKeyText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnPf2LongKeyChanged(byte value)
    {
        OnPropertyChanged(nameof(Pf2LongKeyText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnPf3LongKeyChanged(byte value)
    {
        OnPropertyChanged(nameof(Pf3LongKeyText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnP1LongKeyChanged(byte value)
    {
        OnPropertyChanged(nameof(P1LongKeyText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnP2LongKeyChanged(byte value)
    {
        OnPropertyChanged(nameof(P2LongKeyText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnKnobLockChanged(bool value) => NotifyPendingRadioWriteProperties();
    partial void OnKeyboardLockChanged(bool value) => NotifyPendingRadioWriteProperties();
    partial void OnSideKeyLockChanged(bool value) => NotifyPendingRadioWriteProperties();
    partial void OnForcedKeyLockChanged(bool value) => NotifyPendingRadioWriteProperties();
    partial void OnLongKeyTimeChanged(byte value)
    {
        OnPropertyChanged(nameof(LongKeyTimeText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAddressBookSentWithCodeChanged(byte value)
    {
        OnPropertyChanged(nameof(AddressBookSentWithCodeText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnTotChanged(byte value)
    {
        OnPropertyChanged(nameof(TotText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnFrequencyStepChanged(byte value)
    {
        OnPropertyChanged(nameof(FrequencyStepText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnGeneralFrequencyStepChanged(byte value)
    {
        OnPropertyChanged(nameof(GeneralFrequencyStepText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnSqlLevelAChanged(byte value)
    {
        OnPropertyChanged(nameof(SqlLevelAText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnSqlLevelBChanged(byte value)
    {
        OnPropertyChanged(nameof(SqlLevelBText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnTbstChanged(byte value)
    {
        OnPropertyChanged(nameof(TbstText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAnalogCallHoldTimeChanged(byte value)
    {
        OnPropertyChanged(nameof(AnalogCallHoldTimeText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnCallChannelMaintainedChanged(byte value)
    {
        OnPropertyChanged(nameof(CallChannelMaintainedText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnPriorityZoneAChanged(byte value) => NotifyPendingRadioWriteProperties();
    partial void OnPriorityZoneBChanged(byte value) => NotifyPendingRadioWriteProperties();
    partial void OnMuteTimingChanged(byte value)
    {
        OnPropertyChanged(nameof(MuteTimingText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnTotPredictChanged(byte value)
    {
        OnPropertyChanged(nameof(TotPredictText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnTxPowerAgcChanged(byte value)
    {
        OnPropertyChanged(nameof(TxPowerAgcText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnNoaaMoniChanged(byte value)
    {
        OnPropertyChanged(nameof(NoaaMoniText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnNoaaScanChanged(byte value)
    {
        OnPropertyChanged(nameof(NoaaScanText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnNoaaChanged(byte value)
    {
        OnPropertyChanged(nameof(NoaaText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnGroupCallHoldTimeChanged(byte value)
    {
        OnPropertyChanged(nameof(GroupCallHoldTimeText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnPrivateCallHoldTimeChanged(byte value)
    {
        OnPropertyChanged(nameof(PrivateCallHoldTimeText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnManualDialGroupCallHoldTimeChanged(byte value)
    {
        OnPropertyChanged(nameof(ManualDialGroupCallHoldTimeText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnManualDialPrivateCallHoldTimeChanged(byte value)
    {
        OnPropertyChanged(nameof(ManualDialPrivateCallHoldTimeText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnVoiceHeaderRepetitionsChanged(byte value)
    {
        OnPropertyChanged(nameof(VoiceHeaderRepetitionsText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnTxPreambleDurationChanged(byte value)
    {
        OnPropertyChanged(nameof(TxPreambleDurationText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnFilterOwnIdChanged(byte value)
    {
        OnPropertyChanged(nameof(FilterOwnIdText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnDigitalRemoteKillChanged(byte value)
    {
        OnPropertyChanged(nameof(DigitalRemoteKillText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnDigitalMonitorChanged(byte value)
    {
        OnPropertyChanged(nameof(DigitalMonitorText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnDigitalMonitorCcChanged(byte value)
    {
        OnPropertyChanged(nameof(DigitalMonitorCcText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnDigitalMonitorIdChanged(byte value)
    {
        OnPropertyChanged(nameof(DigitalMonitorIdText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnMonitorSlotHoldChanged(byte value)
    {
        OnPropertyChanged(nameof(MonitorSlotHoldText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnRemoteMonitorChanged(byte value)
    {
        OnPropertyChanged(nameof(RemoteMonitorText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnSmsFormatChanged(byte value)
    {
        OnPropertyChanged(nameof(SmsFormatText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnResetDigitalProtocolChanged(byte value)
    {
        OnPropertyChanged(nameof(ResetDigitalProtocolText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnGpsPowerChanged(byte value) => OnPropertyChanged(nameof(GpsPowerText));
    partial void OnGpsPositioningChanged(byte value)
    {
        OnPropertyChanged(nameof(GpsPositioningText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnTimeZoneChanged(byte value)
    {
        OnPropertyChanged(nameof(TimeZoneText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnRangingIntervalChanged(byte value) => OnPropertyChanged(nameof(RangingIntervalText));
    partial void OnGpsTemplateInformationChanged(byte value) => OnPropertyChanged(nameof(GpsTemplateInformationText));
    partial void OnGpsRoamingChanged(byte value) => OnPropertyChanged(nameof(GpsRoamingText));
    partial void OnVfoScanTypeChanged(byte value)
    {
        OnPropertyChanged(nameof(VfoScanTypeText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnVfoScanStartFreqUhfChanged(int value)
    {
        ValidateProperty(VfoScanStartFreqUhfText, nameof(VfoScanStartFreqUhfText));
        OnPropertyChanged(nameof(VfoScanStartFreqUhfText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnVfoScanEndFreqUhfChanged(int value)
    {
        ValidateProperty(VfoScanEndFreqUhfText, nameof(VfoScanEndFreqUhfText));
        OnPropertyChanged(nameof(VfoScanEndFreqUhfText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnVfoScanStartFreqVhfChanged(int value)
    {
        ValidateProperty(VfoScanStartFreqVhfText, nameof(VfoScanStartFreqVhfText));
        OnPropertyChanged(nameof(VfoScanStartFreqVhfText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnVfoScanEndFreqVhfChanged(int value)
    {
        ValidateProperty(VfoScanEndFreqVhfText, nameof(VfoScanEndFreqVhfText));
        OnPropertyChanged(nameof(VfoScanEndFreqVhfText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAutoRepeaterAChanged(byte value)
    {
        OnPropertyChanged(nameof(AutoRepeaterAText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAutoRepeaterBChanged(byte value)
    {
        OnPropertyChanged(nameof(AutoRepeaterBText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAutoRepeater1UhfChanged(byte value)
    {
        OnPropertyChanged(nameof(AutoRepeater1UhfText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAutoRepeater1VhfChanged(byte value)
    {
        OnPropertyChanged(nameof(AutoRepeater1VhfText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAutoRepeater2UhfChanged(byte value)
    {
        OnPropertyChanged(nameof(AutoRepeater2UhfText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAutoRepeater2VhfChanged(byte value)
    {
        OnPropertyChanged(nameof(AutoRepeater2VhfText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnRepeaterCheckChanged(byte value)
    {
        OnPropertyChanged(nameof(RepeaterCheckText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnRepeaterCheckIntervalChanged(byte value)
    {
        OnPropertyChanged(nameof(RepeaterCheckIntervalText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnRepeaterCheckReconnectionsChanged(byte value)
    {
        OnPropertyChanged(nameof(RepeaterCheckReconnectionsText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnRepeaterOutOfRangeNotifyChanged(byte value)
    {
        OnPropertyChanged(nameof(RepeaterOutOfRangeNotifyText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnOutOfRangeNotifyChanged(byte value)
    {
        OnPropertyChanged(nameof(OutOfRangeNotifyText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAutoRoamingChanged(byte value)
    {
        OnPropertyChanged(nameof(AutoRoamingText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAutoRoamingStartConditionChanged(byte value)
    {
        OnPropertyChanged(nameof(AutoRoamingStartConditionText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAutoRoamingFixedTimeChanged(byte value)
    {
        OnPropertyChanged(nameof(AutoRoamingFixedTimeText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnRoamingEffectWaitTimeChanged(byte value)
    {
        OnPropertyChanged(nameof(RoamingEffectWaitTimeText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAutoRepeater1MinFreqVhfChanged(int value)
    {
        ValidateProperty(AutoRepeater1MinFreqVhfText, nameof(AutoRepeater1MinFreqVhfText));
        OnPropertyChanged(nameof(AutoRepeater1MinFreqVhfText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAutoRepeater1MaxFreqVhfChanged(int value)
    {
        ValidateProperty(AutoRepeater1MaxFreqVhfText, nameof(AutoRepeater1MaxFreqVhfText));
        OnPropertyChanged(nameof(AutoRepeater1MaxFreqVhfText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAutoRepeater1MinFreqUhfChanged(int value)
    {
        ValidateProperty(AutoRepeater1MinFreqUhfText, nameof(AutoRepeater1MinFreqUhfText));
        OnPropertyChanged(nameof(AutoRepeater1MinFreqUhfText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAutoRepeater1MaxFreqUhfChanged(int value)
    {
        ValidateProperty(AutoRepeater1MaxFreqUhfText, nameof(AutoRepeater1MaxFreqUhfText));
        OnPropertyChanged(nameof(AutoRepeater1MaxFreqUhfText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAutoRepeater2MinFreqVhfChanged(int value)
    {
        ValidateProperty(AutoRepeater2MinFreqVhfText, nameof(AutoRepeater2MinFreqVhfText));
        OnPropertyChanged(nameof(AutoRepeater2MinFreqVhfText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAutoRepeater2MaxFreqVhfChanged(int value)
    {
        ValidateProperty(AutoRepeater2MaxFreqVhfText, nameof(AutoRepeater2MaxFreqVhfText));
        OnPropertyChanged(nameof(AutoRepeater2MaxFreqVhfText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAutoRepeater2MinFreqUhfChanged(int value)
    {
        ValidateProperty(AutoRepeater2MinFreqUhfText, nameof(AutoRepeater2MinFreqUhfText));
        OnPropertyChanged(nameof(AutoRepeater2MinFreqUhfText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAutoRepeater2MaxFreqUhfChanged(int value)
    {
        ValidateProperty(AutoRepeater2MaxFreqUhfText, nameof(AutoRepeater2MaxFreqUhfText));
        OnPropertyChanged(nameof(AutoRepeater2MaxFreqUhfText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnRepeaterModeChanged(byte value)
    {
        OnPropertyChanged(nameof(RepeaterModeText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnRepCcLimitChanged(byte value)
    {
        OnPropertyChanged(nameof(RepCcLimitText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnRepSlotAChanged(byte value)
    {
        OnPropertyChanged(nameof(RepSlotAText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnRepSlotBChanged(byte value)
    {
        OnPropertyChanged(nameof(RepSlotBText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnRepeaterWhitelistChanged(byte value)
    {
        OnPropertyChanged(nameof(RepeaterWhitelistText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnRecordFunctionChanged(byte value)
    {
        OnPropertyChanged(nameof(RecordFunctionText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnRecordDelayChanged(byte value)
    {
        OnPropertyChanged(nameof(RecordDelayText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnMaxVolumeChanged(byte value)
    {
        OnPropertyChanged(nameof(MaxVolumeText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnMaxHeadphoneVolumeChanged(byte value)
    {
        OnPropertyChanged(nameof(MaxHeadphoneVolumeText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnDigiMicGainChanged(byte value)
    {
        OnPropertyChanged(nameof(DigiMicGainText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnEnhancedSoundQualityChanged(byte value)
    {
        OnPropertyChanged(nameof(EnhancedSoundQualityText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAnalogMicGainChanged(byte value)
    {
        OnPropertyChanged(nameof(AnalogMicGainText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnPowerOnVolumeTypeChanged(byte value)
    {
        OnPropertyChanged(nameof(PowerOnVolumeTypeText));
        OnPropertyChanged(nameof(IsPowerOnVolumeEnabled));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnPowerOnVolumeChanged(byte value)
    {
        OnPropertyChanged(nameof(PowerOnVolumeText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnRxAgcChanged(byte value)
    {
        OnPropertyChanged(nameof(RxAgcText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnNxMicGainChanged(byte value)
    {
        OnPropertyChanged(nameof(NxMicGainText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnSubSpkInTxChanged(byte value)
    {
        OnPropertyChanged(nameof(SubSpkInTxText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnRxNoiseReductionChanged(byte value)
    {
        OnPropertyChanged(nameof(RxNoiseReductionText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnTxNoiseReductionChanged(byte value)
    {
        OnPropertyChanged(nameof(TxNoiseReductionText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnSatLocationChanged(byte value)
    {
        OnPropertyChanged(nameof(SatLocationText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnSatTxPowerChanged(byte value)
    {
        OnPropertyChanged(nameof(SatTxPowerText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnSatAnaSqlChanged(byte value)
    {
        OnPropertyChanged(nameof(SatAnaSqlText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnSatAosLimitChanged(byte value)
    {
        OnPropertyChanged(nameof(SatAosLimitText));
        NotifyPendingRadioWriteProperties();
    }
    partial void OnRoamingZoneChanged(byte value) => NotifyPendingRadioWriteProperties();
    partial void OnNoaaChannelChanged(byte value)
    {
        OnPropertyChanged(nameof(NoaaChannelText));
        NotifyPendingRadioWriteProperties();
    }
}

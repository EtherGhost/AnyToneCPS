using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Pure decoder for the D890UV's Optional Settings - a single instance, not
/// a list, like Master ID/Talk Alias/Alarm/APRS Settings. Byte layout
/// transcribed field-for-field from the MIT-licensed reference project
/// github.com/xbenkozx/anytone-cps (optional_settings.cpp, decode_D890UV -
/// confirmed fully populated for D890UV, same good news as APRS/Alarm).
///
/// Started as a deliberate partial port (Power-on/Display/Key Function only,
/// 54 fields - this ~230-field entity was deliberately scoped down to
/// a smaller first slice); this pass adds the rest: Alert Tone (as a
/// 25-entry sub-list, see <see cref="AlertToneCodec"/>, rather than 60 flat
/// properties - 5 categories x 5 tones x freq/period), GPS/Ranging, VFO
/// Scan, Auto Repeater, Record, Volume/Audio, Work Mode, VOX/Bluetooth, STE,
/// AM/FM, Power Save, Other, Digital Func, Satellite-related settings - all
/// as flat properties (kept simple, matching the first-slice pattern -
/// only Alert Tone's 5x5x2 structure was regular/large enough to justify a
/// sub-list, same judgment call as APRS's Fix Locations/Digital Reports).
///
/// <c>digital_protocol</c> is declared on the class but its assignment is
/// commented out in the reference's own <c>decode_D890UV</c> (a second field,
/// <c>sms_format</c>, reads the same offset instead) - not ported, matching
/// the established "don't port fields the reference itself never assigns
/// for this model" rule (same as Alarm Settings' <c>work_mode_*</c>).
/// <c>bt_on_off_D878UVII</c> is similarly never assigned by
/// <c>decode_D890UV</c> (only the D890UV-suffixed variant is) - not ported,
/// same reasoning as Key Function's D878UVII PF-key fields in the first slice.
///
/// **Repeated-byte-offset pattern** - worth noting as a class of upstream
/// issue rather than isolated slips (all re-checked twice against the actual
/// C++, not transcription errors on this end): <c>RoamingZone</c>/
/// <c>AddressBookSentWithCode</c> (0xd5) is the one remaining unresolved
/// case - ported faithfully (each field reads its documented,
/// possibly-duplicated offset) since there's no confirmed correct offset to
/// use instead for either of them.
///
/// Three OTHER pairs were ALSO assumed to be "genuine collision" pairs but
/// turned out not to be, all found via the same method (a live
/// differential write, then - once the vendor CPS itself proved both
/// fields hold independent values simultaneously - a focused single-field
/// toggle-and-diff to find the real offset):
/// <list type="bullet">
/// <item><c>GpsPositioning</c>/<c>AutoShutdownType</c> (0x3f) - disproved
/// 2026-07-25 toggling Power Save tab values; AutoShutdownType's real offset
/// is 0x10f, 0x3f is genuinely GpsPositioning alone.</item>
/// <item><c>BacklightTxDelay</c>/<c>SeparateDisplay</c> (0xe1) - disproved
/// 2026-07-25 toggling Display tab values (vendor CPS showed Backlight Delay
/// Of TX=5 AND Separate Display=On simultaneously after a write that this
/// app's own decode showed as colliding); BacklightTxDelay's real offset is
/// 0xe0 (one byte earlier), 0xe1 is genuinely SeparateDisplay alone.</item>
/// <item><c>AmFmFunction</c>/<c>FmVfoMem</c> (0x1e) - disproved 2026-07-27
/// via 5 live differential writes on the AM/FM tab; <c>FmVfoMem</c> is
/// genuinely alone at 0x1e, <c>AmFmFunction</c>'s real offset is 0x21. The
/// same 5 writes also found 3 more wrong (not colliding, just wrong)
/// AM/FM-tab offsets with no collision theory behind them at all -
/// <c>FmMonitor</c> (0x2b claimed, really 0x2a), <c>AmOffset</c> (0x141
/// claimed, really 0x140), <c>FrequencyStep</c> (0x08 claimed, really
/// 0x159) - and disproved <c>AmWorkZone</c>'s claimed 0x140 without finding
/// a replacement (see its own assignment below).</item>
/// </list>
///
/// The Power-On/Startup fields (<c>DefaultStartupChannel</c>,
/// <c>StartupZoneA/B</c>, <c>StartupChannelA/B</c>, <c>StartupReset</c>) were
/// NOT actually at the reference's claimed offsets at all - three separate
/// live differential write captures 2026-07-20 found every one of them
/// (except <c>StartupChannelB</c>, which happened to already be correct)
/// sitting 1-2 bytes earlier than the reference's port, not sharing a byte
/// with an unrelated field the way the pattern above does. <c>StartupGpsTest</c>'s
/// real offset is still unconfirmed. See each field's own assignment below
/// for the specific evidence.
///
/// Text field encoding: <c>GpsInformationChar</c> decoded as UTF-16LE by
/// default (not narrow ASCII) - a recurring lesson in this codebase
/// (APRS <c>SendingText</c>, then this class's own Power-on text fields,
/// both initially guessed narrow and both wrong) is that UTF-16LE is the
/// default convention on this radio, narrow ASCII the exception - verify
/// against a live read same as those two before fully trusting this one too.
/// </summary>
public static class OptionalSettingsCodec
{
    public const int MainDataLength = 0x160;
    // Was 0x30 - extended 2026-07-24 after a live WRITE capture found
    // PowerOnPasswordChar's real bytes at offset 0x40 (see Decode's doc
    // comment), past the old length. Line1/Line2 turned out to each occupy
    // a full 0x20-byte slot (only the first 0xe bytes of each are ever
    // populated/decoded - no evidence the radio uses more), so
    // Line1(0x0)+Line2(0x20)+PasswordChar(0x40, 8 bytes of headroom) fits
    // exactly in 0x50.
    public const int SecondaryDataLength = 0x50;
    public const int TertiaryDataLength = 0x30;

    public static DecodedOptionalSettings Decode(ReadOnlySpan<byte> data3500000, ReadOnlySpan<byte> data3500900, ReadOnlySpan<byte> data3501280)
    {
        return new DecodedOptionalSettings
        {
            // Power On
            PowerOnInterface = data3500000[0x6],
            // Confirmed via live hardware read 2026-07-15: NOT narrow ASCII
            // despite being right next to other narrow fields elsewhere in
            // this record (the "look at what's adjacent" heuristic failed
            // here) - decoding as narrow ASCII produced the same
            // space-between-every-character signature as the APRS
            // SendingText bug found earlier. Fixed to UTF-16LE.
            //
            // Line 2/Password Char were themselves SWAPPED - found
            // 2026-07-20 comparing a live read against the vendor CPS's own
            // display: the 8-byte slice at 0x20 (originally labeled
            // "password char") read "ANYT", exactly the first 4 characters
            // of the real Line 2 text "ANYTONE" - i.e. that offset holds
            // Line 2's content, just truncated by too-short a slice.
            //
            // The 0x10 slice this correction moved Password Char to was
            // ITSELF wrong - a live WRITE capture 2026-07-24 (vendor CPS
            // wrote Password Char = "1357", used moments later as the
            // radio's real power-on password, confirming it's genuinely
            // load-bearing) found "1357" written in plain ASCII at 0x40,
            // not UTF-16LE at 0x10 (which stayed all-zero the whole time -
            // just Line1's own unused padding, not a real field at all: the
            // previous "genuinely empty at 0x10" read confirmation was a
            // coincidence, exactly like the earlier StartupZoneA/
            // DefaultStartupChannel mixup). Password Char is the one
            // exception to this record's otherwise-uniform UTF-16LE
            // convention - see AsciiTextCodec's doc comment for the other
            // known narrow-ASCII fields in this codebase.
            //
            // Corrected 2026-07-28: the real vendor CPS
            // allows 14 characters per line, not 7. This app was only
            // reading/writing the first half of each line's real 0x1c
            // (28-byte) UTF-16LE allocation - the "Line1's own unused
            // padding" noted above IS the second half of Line1's own field,
            // not spare space. Widened Line1 (0x0) and Line2 (0x20) from
            // 0xe to 0x1c bytes each; both still fit cleanly before the
            // next field (Line1 ends at 0x1c, 4 bytes before Line2 starts
            // at 0x20; Line2 ends at 0x3c, 4 bytes before Password Char at
            // 0x40) - that alignment is itself supporting evidence this is
            // the real field size, not a guess.
            PowerOnDisplayLine1 = TextFieldCodec.DecodeName(data3500900.Slice(0x0, 0x1c)),
            PowerOnDisplayLine2 = TextFieldCodec.DecodeName(data3500900.Slice(0x20, 0x1c)),
            PowerOnPassword = data3500000[0x7],
            PowerOnPasswordChar = AsciiTextCodec.Decode(data3500900.Slice(0x40, 0x8)),
            // Startup Zone/Channel A/B are ALL one byte earlier than the
            // reference project's port, confirmed 2026-07-20 via two live
            // write captures against the real project's zone membership:
            //   - Zone A "ANA RPTR"->"ANA SIMP" (index 1->2) changed only
            //     0xd7 (was assumed to be DefaultStartupChannel - that
            //     field doesn't actually live here at all; both
            //     interpretations happened to read 1, a coincidence that
            //     masked the bug).
            //   - Zone B "CALL"->"JAKT" (index 0->4) changed only 0xd8.
            //   - Channel A "AV00"->"AV01" changed only 0xd9.
            //   - Channel B (already at "J03") stayed at 0xda, consistent
            //     across two separate live reads.
            // Channel A/B are a POSITION within the referenced zone's
            // Members list, not a global channel index - "PMR01" is
            // Channel Number 600, which can't fit in a byte.
            // DefaultStartupChannel confirmed at 0xd6 (not 0xd7) via a
            // third live write - toggling it On->Off changed only 0xd6,
            // 0x01->0x00, with Zone/Channel A/B all unchanged as expected.
            // 0xdb remains unidentified - untouched by every test so far.
            StartupZoneA = data3500000[0xd7],
            StartupZoneB = data3500000[0xd8],
            StartupChannelA = data3500000[0xd9],
            StartupChannelB = data3500000[0xda],
            DefaultStartupChannel = data3500000[0xd6],
            // StartupReset confirmed at 0xea (not 0xec) via the same write -
            // toggling it On->Off changed only 0xea, 0x01->0x00. 0xeb is
            // genuinely BtHoldTime (unchanged, held its plausible duration
            // value 0x0a throughout every test) - the doc comment above
            // about a 0xeb/BtHoldTime collision was based on the reference
            // project's wrong offset for StartupReset, not a real
            // collision. StartupGpsTest's real offset is still
            // UNCONFIRMED, and appears NOT to be in any codeplug memory
            // this app can currently read or write at all: two separate
            // live differential tests 2026-07-20 - a full-codeplug diff
            // around a vendor CPS write, and a clean before/after Read
            // From Radio diff (this app's own read, guaranteeing identical
            // address coverage on both sides) across 5300+ addresses -
            // found ZERO byte changes despite the setting demonstrably
            // persisting on the real radio (independently reproduced: a
            // saved project file showed "Off" while a Read From Radio on
            // the same physical radio showed "On", with no edit in
            // between). Two plausible explanations, neither confirmed:
            // it lives in some other subsystem (calibration/config area)
            // outside the normal codeplug address space, or this
            // particular D890UV doesn't actually implement/store this
            // reference-project field at all (the reference targets
            // several AnyTone models, and a GPS test toggle may be
            // meaningful only on some of them). Left at its default (0)
            // and the UI control disabled rather than guessed at.
            StartupReset = data3500000[0xea],

            // Display - BacklightTxDelay moved from the reference's claimed
            // 0xe1 (genuinely just SeparateDisplay, not a real collision)
            // to the confirmed 0xe0 via a live differential write
            // 2026-07-25 (see this class's doc comment).
            Brightness = data3500000[0x26],
            AutoBacklightDuration = data3500000[0x27],
            BacklightTxDelay = data3500000[0xe0],
            MenuExitTime = data3500000[0x37],
            TimeDisplay = data3500000[0x51],
            LastCaller = data3500000[0x4d],
            CallDisplayMode = data3500000[0xaf],
            CallsignDisplayColor = data3500000[0xbc],
            CallEndPromptBox = data3500000[0x3a],
            DisplayChannelNumber = data3500000[0xb8],
            DisplayCurrentContact = data3500000[0xb9],
            StandbyCharColor = data3500000[0xc0],
            StandbyBkPicture = data3500000[0xc1],
            ShowLastCallOnLaunch = data3500000[0xc2],
            SeparateDisplay = data3500000[0xe1],
            ChSwitchingKeepsCaller = data3500000[0xe2],
            BacklightRxDelay = data3500000[0xe5],
            ChannelNameColorA = data3500000[0xe3],
            ChannelNameColorB = data3500000[0x109],
            ZoneNameColorA = data3500000[0x10d],
            ZoneNameColorB = data3500000[0x10e],
            DisplayChannelType = (data3500000[0x110] & 0x01) != 0,
            DisplayTimeSlot = (data3500000[0x110] & 0x02) != 0,
            DisplayColorCode = (data3500000[0x110] & 0x04) != 0,
            DateDisplayFormat = data3500000[0x112],
            VolumeBar = data3500000[0x47],
            // Not in the reference project at all (confirmed absent from
            // its own decode_D890UV/optional_settings_dialog.cpp/
            // constants.cpp) - only found in the vendor CPS's own
            // english.ini (id 302523, "Night Mode"). Offset found via a
            // live differential write 2026-07-25 (toggle-only, one byte
            // changed) since there was no reference offset to start from at all.
            NightMode = data3500000[0x14d],

            // Key Function
            KeyLock = data3500000[0x02],
            Pf1ShortKey = data3500000[0x10],
            Pf2ShortKey = data3500000[0x11],
            Pf3ShortKey = data3500000[0x12],
            P1ShortKey = data3500000[0x13],
            P2ShortKey = data3500000[0x14],
            Pf1LongKey = data3500000[0x41],
            Pf2LongKey = data3500000[0x42],
            Pf3LongKey = data3500000[0x43],
            P1LongKey = data3500000[0x44],
            P2LongKey = data3500000[0x45],
            LongKeyTime = data3500000[0x46],
            KnobLock = (data3500000[0xbe] & 0x01) != 0,
            KeyboardLock = (data3500000[0xbe] & 0x02) != 0,
            SideKeyLock = (data3500000[0xbe] & 0x08) != 0,
            ForcedKeyLock = (data3500000[0xbe] & 0x10) != 0,

            // Alert Tone (simple fields only - the 25 freq/period pairs are
            // decoded separately via AlertToneCodec into a sub-list)
            SmsAlert = data3500000[0x29],
            CallAlert = data3500000[0x2f],
            DigiCallResetTone = data3500000[0x32],
            TalkPermit = data3500000[0x31],
            KeyTone = data3500000[0x00],
            DigiIdleChannelTone = data3500000[0x36],
            StartupSound = data3500000[0x39],
            ToneKeySoundAdjustable = data3500000[0xbb],
            AnalogIdleChannelTone = data3500000[0x111],
            PluginRecordingTone = data3500000[0xb4],

            // GPS/Ranging
            GpsPower = data3500000[0x28],
            GpsPositioning = data3500000[0x3f],
            TimeZone = data3500000[0x30],
            RangingInterval = data3500000[0xb5],
            DistanceUnit = data3500000[0xbd],
            GpsTemplateInformation = data3500000[0x53],
            GpsInformationChar = TextFieldCodec.DecodeName(data3501280.Slice(0x0, 0x30)),
            GpsMode = data3500000[0x105],
            GpsRoaming = data3500000[0x114],

            // VFO Scan
            VfoScanType = data3500000[0x0e],
            VfoScanStartFreqUhf = (int)BinaryPrimitives.ReadUInt32LittleEndian(data3500000.Slice(0x58, 4)),
            VfoScanEndFreqUhf = (int)BinaryPrimitives.ReadUInt32LittleEndian(data3500000.Slice(0x5c, 4)),
            VfoScanStartFreqVhf = (int)BinaryPrimitives.ReadUInt32LittleEndian(data3500000.Slice(0x60, 4)),
            VfoScanEndFreqVhf = (int)BinaryPrimitives.ReadUInt32LittleEndian(data3500000.Slice(0x64, 4)),

            // Auto Repeater
            AutoRepeaterA = data3500000[0x48],
            AutoRepeaterB = data3500000[0xd4],
            AutoRepeater1Uhf = data3500000[0x68],
            AutoRepeater1Vhf = data3500000[0x69],
            AutoRepeater2Uhf = data3500000[0xf1],
            AutoRepeater2Vhf = data3500000[0xf2],
            // Offsets 0xdc-0xdf/0xe4/0xe9 corrected 2026-07-28 - live-write-
            // confirmed to be shifted one byte from the reference project's
            // claims (0xdd-0xe0/0xe5/0xea) - see PowerOnFieldPatch's doc comment.
            RepeaterCheck = data3500000[0xdc],
            RepeaterCheckInterval = data3500000[0xdd],
            RepeaterCheckReconnections = data3500000[0xde],
            RepeaterOutOfRangeNotify = data3500000[0xe4],
            OutOfRangeNotify = data3500000[0xe9],
            AutoRoaming = data3500000[0xe7],
            AutoRoamingStartCondition = data3500000[0xdf],
            AutoRoamingFixedTime = data3500000[0xba],
            RoamingEffectWaitTime = data3500000[0xbf],
            // Corrected 2026-07-28 - live-write-confirmed to be at 0xdb, not
            // the reference project's claimed 0xd5 (that offset is genuinely
            // AddressBookSentWithCode alone, not a real collision).
            RoamingZone = data3500000[0xdb],
            AutoRepeater1MinFreqVhf = (int)BinaryPrimitives.ReadUInt32LittleEndian(data3500000.Slice(0xc4, 4)),
            AutoRepeater1MaxFreqVhf = (int)BinaryPrimitives.ReadUInt32LittleEndian(data3500000.Slice(0xc8, 4)),
            AutoRepeater1MinFreqUhf = (int)BinaryPrimitives.ReadUInt32LittleEndian(data3500000.Slice(0xcc, 4)),
            AutoRepeater1MaxFreqUhf = (int)BinaryPrimitives.ReadUInt32LittleEndian(data3500000.Slice(0xd0, 4)),
            AutoRepeater2MinFreqVhf = (int)BinaryPrimitives.ReadUInt32LittleEndian(data3500000.Slice(0xf4, 4)),
            AutoRepeater2MaxFreqVhf = (int)BinaryPrimitives.ReadUInt32LittleEndian(data3500000.Slice(0xf8, 4)),
            AutoRepeater2MinFreqUhf = (int)BinaryPrimitives.ReadUInt32LittleEndian(data3500000.Slice(0xfc, 4)),
            AutoRepeater2MaxFreqUhf = (int)BinaryPrimitives.ReadUInt32LittleEndian(data3500000.Slice(0x100, 4)),
            RepeaterMode = data3500000[0x143],
            RepCcLimit = data3500000[0x144],
            RepSlotA = data3500000[0x145],
            RepSlotB = data3500000[0x146],
            // Found from scratch 2026-07-28 - no reference-project offset
            // existed for this field at all.
            RepeaterWhitelist = data3500000[0x15a],

            // Record
            RecordFunction = data3500000[0x22],
            RecordDelay = data3500000[0xae],

            // Volume/Audio
            MaxVolume = data3500000[0x3b],
            PowerOnVolumeType = data3500000[0x155],
            PowerOnVolume = data3500000[0x156],
            MaxHeadphoneVolume = data3500000[0x52],
            DigiMicGain = data3500000[0x0f],
            EnhancedSoundQuality = data3500000[0x57],
            AnalogMicGain = data3500000[0x113],
            RxAgc = data3500000[0x147],
            NxMicGain = data3500000[0x153],
            // Found from scratch 2026-07-28 - no reference-project offset
            // existed for any of these.
            SubSpkInTx = data3500000[0x142],
            RxNoiseReduction = data3500000[0x148],
            TxNoiseReduction = data3500000[0x149],

            // Work Mode
            DisplayMode = data3500000[0x01],
            VfMrA = data3500000[0x15],
            VfMrB = data3500000[0x16],
            MemZoneA = data3500000[0x1f],
            MemZoneB = data3500000[0x20],
            MainChannelSet = data3500000[0x2c],
            SubChannelMode = data3500000[0x2d],
            WorkingMode = data3500000[0x34],

            // VOX/BT
            // VoxLevel's offset+encoding confirmed via a live differential
            // write 2026-07-27 (toggle-only, one byte at 0x0c changed from
            // 0x00 to 0x01 = "On", matching VoxLevelOptions' index order
            // exactly). VoxDelay/VoxDetection offsets still carried from the
            // reference project, not yet independently confirmed.
            VoxLevel = data3500000[0x0c],
            VoxDelay = data3500000[0x0d],
            VoxDetection = data3500000[0x33],
            BtOnOff = data3500000[0xb1],
            BtIntMic = data3500000[0xb2],
            BtIntSpk = data3500000[0xb3],
            BtMicGain = data3500000[0xb6],
            BtSpkGain = data3500000[0xb7],
            BtHoldTime = data3500000[0xeb],
            BtRxDelay = data3500000[0xec],
            BtPttHold = data3500000[0xf0],
            BtPttSleepTime = data3500000[0x104],
            BtNrBefore = data3500000[0x14b],
            BtNrAfter = data3500000[0x14c],

            // STE
            SteTypeOfCtcss = data3500000[0x17],
            SteWhenNoSignal = data3500000[0x18],
            SteTime = data3500000[0x106],

            // AM/FM - AmFmFunction/FmMonitor/AmOffset/AmSqlLevel/FrequencyStep
            // (below) all moved from their reference-project-claimed offsets
            // via live differential writes 2026-07-27 - see this class's doc
            // comment for the full story (the claimed AmFmFunction/FmVfoMem
            // collision at 0x1e was ALSO disproven the same way: FmVfoMem
            // genuinely is alone at 0x1e, AmFmFunction is really at 0x21).
            AmFmFunction = data3500000[0x21],
            FmVfoMem = data3500000[0x1e],
            FmWorkChannel = data3500000[0x1d],
            FmMonitor = data3500000[0x2a],
            AmVfoMem = data3500000[0x13f],
            // AmWorkZone's reference-project-claimed offset (0x140) is
            // DISPROVEN, not just unconfirmed - 2 live differential writes
            // 2026-07-27 showed 0x140 is genuinely AmOffset. AmWorkZone's
            // real offset is unknown and this radio has only 1 AM zone, so
            // there's no way to isolate it via a value change. Left
            // undecoded (defaults to 0) rather than reading a byte known to
            // belong to a different field - same precedent as
            // StartupGpsTest above.
            AmOffset = data3500000[0x140],
            AmSqlLevel = data3500000[0x141],

            // Power Save - AutoShutdown/PowerSave confirmed via live write
            // 2026-07-25 (30m/2:1 -> raw 2/2, matching AutoShutdownOptions/
            // PowerSaveOptions index order). AutoShutdownType moved from the
            // reference's claimed 0x3f (genuinely just GpsPositioning, not a
            // collision - see this class's doc comment) to the real
            // confirmed 0x10f.
            AutoShutdown = data3500000[0x3],
            PowerSave = data3500000[0xb],
            AutoShutdownType = data3500000[0x10f],

            // Other
            AddressBookSentWithCode = data3500000[0xd5],
            Tot = data3500000[0x04],
            Language = data3500000[0x05],
            // CORRECTION 2026-07-27: an earlier AM/FM-tab pass, same day,
            // concluded 0x08 was simply the WRONG offset for
            // Frequency Step, moved the field's Decode/write to the real
            // AM/FM-tab offset (0x159), and removed the Other-tab UI
            // control on the theory both tabs shared one field. A live
            // differential write against the real vendor CPS's OWN Other
            // tab this same day disproved that: 0x08 is a real, genuinely
            // SEPARATE Frequency Step setting (25K -> 50K, raw 7 -> 9,
            // independently confirmed via the vendor CPS's own Read From
            // Radio) - the two tabs have their own independent settings
            // that just happen to share the same label and option list.
            // Restored as GeneralFrequencyStep (kept FrequencyStep's name
            // for the already-wired AM/FM one to avoid unnecessary churn).
            GeneralFrequencyStep = data3500000[0x08],
            FrequencyStep = data3500000[0x159],
            SqlLevelA = data3500000[0x09],
            SqlLevelB = data3500000[0x0a],
            Tbst = data3500000[0x2e],
            AnalogCallHoldTime = data3500000[0x50],
            CallChannelMaintained = data3500000[0x6e],
            PriorityZoneA = data3500000[0x6f],
            PriorityZoneB = data3500000[0x70],
            MuteTiming = data3500000[0xe8],
            EncryptionType = data3500000[0x10a],
            TotPredict = data3500000[0x10b],
            TxPowerAgc = data3500000[0x10c],
            NoaaMoni = data3500000[0x157],
            NoaaScan = data3500000[0x158],
            Noaa = data3500000[0xef],
            NoaaChannel = data3500000[0x13e],

            // Digital Func
            GroupCallHoldTime = data3500000[0x19],
            PrivateCallHoldTime = data3500000[0x1a],
            ManualDialGroupCallHoldTime = data3500000[0x107],
            ManualDialPrivateCallHoldTime = data3500000[0x108],
            VoiceHeaderRepetitions = data3500000[0x1b],
            TxPreambleDuration = data3500000[0x1c],
            FilterOwnId = data3500000[0x38],
            DigitalRemoteKill = data3500000[0x3c],
            DigitalMonitor = data3500000[0x49],
            DigitalMonitorCc = data3500000[0x4a],
            DigitalMonitorId = data3500000[0x4b],
            MonitorSlotHold = data3500000[0x4c],
            RemoteMonitor = data3500000[0x3e],
            SmsFormat = data3500000[0xc3],
            ResetDigitalProtocol = data3500000[0x154],

            // Satellite
            SatLocation = data3500000[0x14e],
            SatTxPower = data3500000[0x14f],
            SatAnaSql = data3500000[0x150],
            SatAosLimit = data3500000[0x151],

            AlertTones = AlertToneCodec.DecodeAll(data3500000)
        };
    }

    /// <summary>
    /// Write-safe patch for the Power-on tab's 11 fields plus the Alert Tone
    /// tab's (renamed 2026-07-28 from "Alert Zone", and merged with the
    /// former separate "Alert Tone1" tab into one) 8 scalar Alert Tone
    /// fields and all 5 tone-group matrices (CallPermit/CallEnd/CallReset/
    /// UnMatchEnd/CallAll) - all of them share the same data_3500000/
    /// data_3500900 blocks, so one patch record and one pair of encode
    /// functions cover both tabs. Named for its original Power-on-only
    /// scope; not renamed to avoid unnecessary churn across existing call
    /// sites/tests. Every Power-on offset was confirmed via live
    /// differential captures during the read-side bug fixes 2026-07-20 (see
    /// <see cref="Decode"/>'s doc comment); the CallPermit/CallEnd/
    /// CallReset fields/offsets were confirmed the same way 2026-07-25 (all
    /// 8 scalar fields and all 3 tone-group offset pairs matched a real
    /// vendor CPS write byte-for-byte); UnMatchEnd/CallAll were confirmed
    /// 2026-07-28. <c>StartupGpsTest</c> is deliberately excluded - its real
    /// offset (if it even exists in this app's addressable codeplug memory)
    /// remains unconfirmed, so there's no field to safely write.
    /// </summary>
    public sealed record PowerOnFieldPatch
    {
        public byte? PowerOnInterface { get; init; }
        public string? PowerOnDisplayLine1 { get; init; }
        public string? PowerOnDisplayLine2 { get; init; }
        public byte? PowerOnPassword { get; init; }
        public string? PowerOnPasswordChar { get; init; }
        public byte? DefaultStartupChannel { get; init; }
        public byte? StartupZoneA { get; init; }
        public byte? StartupChannelA { get; init; }
        public byte? StartupZoneB { get; init; }
        public byte? StartupChannelB { get; init; }
        public byte? StartupReset { get; init; }

        public byte? SmsAlert { get; init; }
        public byte? CallAlert { get; init; }
        public byte? DigiCallResetTone { get; init; }
        public byte? TalkPermit { get; init; }
        public byte? KeyTone { get; init; }
        public byte? DigiIdleChannelTone { get; init; }
        public byte? StartupSound { get; init; }
        public byte? AnalogIdleChannelTone { get; init; }

        // Each is exactly 5 (Frequency, Period) pairs, raw wire units (Period
        // is NOT pre-multiplied by 10 - see AlertToneEntry.PeriodText's doc
        // comment) - present (non-null) means "re-encode all 5 tones of this
        // category", matching ScanListCodec.Encode's "always re-encode the
        // whole thing, not per-field" safety reasoning, since every tone in
        // a category is read/shown together as one matrix.
        public IReadOnlyList<(ushort Frequency, ushort Period)>? CallPermitTones { get; init; }
        public IReadOnlyList<(ushort Frequency, ushort Period)>? MatchEndTones { get; init; }
        public IReadOnlyList<(ushort Frequency, ushort Period)>? CallResetTones { get; init; }
        public IReadOnlyList<(ushort Frequency, ushort Period)>? UnMatchEndTones { get; init; }
        public IReadOnlyList<(ushort Frequency, ushort Period)>? CallAllTones { get; init; }

        // Power Save tab - AutoShutdown/PowerSave confirmed at their
        // original offsets via live write 2026-07-25; AutoShutdownType
        // confirmed at 0x10f (NOT the reference's claimed 0x3f, which is
        // genuinely just GpsPositioning - see Decode's doc comment) via a
        // focused single-field differential test the same day.
        public byte? AutoShutdown { get; init; }
        public byte? PowerSave { get; init; }
        public byte? AutoShutdownType { get; init; }

        // Display tab - all offsets confirmed via a live combined write
        // 2026-07-25 (26 fields written together in one round trip,
        // matching WriteChangesToRadioAsync's own batching behavior);
        // NightMode has no reference-project offset at all (see Decode's
        // doc comment) - found via its own dedicated toggle-only
        // differential test the same day.
        public byte? Brightness { get; init; }
        public byte? AutoBacklightDuration { get; init; }
        public byte? BacklightTxDelay { get; init; }
        public byte? MenuExitTime { get; init; }
        public byte? TimeDisplay { get; init; }
        public byte? LastCaller { get; init; }
        public byte? CallDisplayMode { get; init; }
        public byte? CallsignDisplayColor { get; init; }
        public byte? CallEndPromptBox { get; init; }
        public byte? DisplayChannelNumber { get; init; }
        public byte? DisplayCurrentContact { get; init; }
        public byte? StandbyCharColor { get; init; }
        public byte? StandbyBkPicture { get; init; }
        public byte? ShowLastCallOnLaunch { get; init; }
        public byte? SeparateDisplay { get; init; }
        public byte? ChSwitchingKeepsCaller { get; init; }
        public byte? BacklightRxDelay { get; init; }
        public byte? ChannelNameColorA { get; init; }
        public byte? ChannelNameColorB { get; init; }
        public byte? ZoneNameColorA { get; init; }
        public byte? ZoneNameColorB { get; init; }
        public bool? DisplayChannelType { get; init; }
        public bool? DisplayTimeSlot { get; init; }
        public bool? DisplayColorCode { get; init; }
        public byte? DateDisplayFormat { get; init; }
        public byte? VolumeBar { get; init; }
        public byte? NightMode { get; init; }

        // Work Mode tab - all 8 fields confirmed via live combined
        // write 2026-07-25 (Mem Zone A/B store the zone's radio index,
        // resolved to/from a name by MainViewModel - see
        // OptionalSettingsMemZoneAName/BName's doc comment).
        public byte? DisplayMode { get; init; }
        public byte? VfMrA { get; init; }
        public byte? MemZoneA { get; init; }
        public byte? VfMrB { get; init; }
        public byte? MemZoneB { get; init; }
        public byte? MainChannelSet { get; init; }
        public byte? SubChannelMode { get; init; }
        public byte? WorkingMode { get; init; }

        // Vox/BT tab - the real vendor CPS (1.05) only has the 3 VOX fields;
        // no BT UI exists at all for this radio (confirmed 2026-07-25, see
        // MainView.axaml's BT GroupBox tooltip), so there's nothing to add
        // for BT here. All 3 confirmed via live differential writes
        // 2026-07-27 (VoxLevel toggle-only, VoxDelay+VoxDetection combined
        // in one write) - every byte matched this app's existing option
        // list order exactly, no offset/encoding bugs found.
        public byte? VoxLevel { get; init; }
        public byte? VoxDelay { get; init; }
        public byte? VoxDetection { get; init; }

        // STE tab - all 3 fields confirmed via a live differential write
        // 2026-07-27. SteTypeOfCtcss/SteWhenNoSignal are plain zero-based
        // indexes like every other enum here. SteTime is NOT - its raw byte
        // is milliseconds/10 directly (confirmed: selecting exactly "150MS"
        // produced raw 15), one position off from a zero-based index into
        // SteTimeOptions - see SteTimeText's doc comment for the conversion.
        public byte? SteTypeOfCtcss { get; init; }
        public byte? SteWhenNoSignal { get; init; }
        public byte? SteTime { get; init; }

        // AM/FM tab - all 7 offsets confirmed via 5 live differential
        // writes 2026-07-27, correcting 4 wrong reference-project offsets
        // in the process (AmFmFunction, FmMonitor, AmOffset, FrequencyStep -
        // see OptionalSettingsCodec's class doc comment for the full
        // story). FmWorkChannel's offset/mapping got its own confirmation
        // 2026-07-29 (see its own doc comment below) and is no longer
        // excluded. AmWorkZone remains deliberately excluded, but for a
        // different, now-conclusive reason: a 2026-07-29 live differential
        // write (2 AM zones on the test radio, work zone switched from one
        // to the other) found it doesn't persist as an independent radio
        // setting at all - only AmOffset's own byte changed. There is no
        // AmWorkZone byte to write.
        public byte? AmFmFunction { get; init; }
        public byte? FmVfoMem { get; init; }

        /// <summary>Confirmed 2026-07-29 via a live differential write - a
        /// second FM channel was added on the test radio, FM Work Channel
        /// was set to it in the vendor CPS, and the resulting raw byte at
        /// 0x1d matched a plain zero-based index into the FM channel list
        /// (Number - 1), same convention as every other zone/channel picker
        /// in this app. No longer excluded from the write patch.</summary>
        public byte? FmWorkChannel { get; init; }
        public byte? FmMonitor { get; init; }
        public byte? AmVfoMem { get; init; }
        public byte? AmOffset { get; init; }
        public byte? AmSqlLevel { get; init; }
        public byte? FrequencyStep { get; init; }

        // Key Function tab - every offset confirmed correct exactly as
        // originally coded, via 2 live differential writes 2026-07-27 (11
        // scalar fields in one combined write, the 4 lock booleans'
        // shared byte in a second, isolated write) - a clean result with
        // zero bugs, unlike the AM/FM tab just before it.
        public byte? KeyLock { get; init; }
        public byte? Pf1ShortKey { get; init; }
        public byte? Pf2ShortKey { get; init; }
        public byte? Pf3ShortKey { get; init; }
        public byte? P1ShortKey { get; init; }
        public byte? P2ShortKey { get; init; }
        public byte? Pf1LongKey { get; init; }
        public byte? Pf2LongKey { get; init; }
        public byte? Pf3LongKey { get; init; }
        public byte? P1LongKey { get; init; }
        public byte? P2LongKey { get; init; }
        public byte? LongKeyTime { get; init; }
        public bool? KnobLock { get; init; }
        public bool? KeyboardLock { get; init; }
        public bool? SideKeyLock { get; init; }
        public bool? ForcedKeyLock { get; init; }

        // Other tab - all 19 fields confirmed via 2 live differential
        // writes 2026-07-27, correcting 2 wrong option lists (Language,
        // EncryptionType) and clarifying that GeneralFrequencyStep (0x08)
        // is a real, separate field from the AM/FM tab's FrequencyStep
        // (0x159) - see Decode's doc comment. AddressBookSentWithCode's
        // possible collision with RoamingZone (0xd5, see this class's
        // doc comment) is still unresolved - would need a Roaming-tab
        // test to fully settle, out of scope for this pass.
        public byte? AddressBookSentWithCode { get; init; }
        public byte? Tot { get; init; }
        public byte? Language { get; init; }
        public byte? GeneralFrequencyStep { get; init; }
        public byte? SqlLevelA { get; init; }
        public byte? SqlLevelB { get; init; }
        public byte? Tbst { get; init; }
        public byte? AnalogCallHoldTime { get; init; }
        public byte? CallChannelMaintained { get; init; }
        public byte? PriorityZoneA { get; init; }
        public byte? PriorityZoneB { get; init; }
        public byte? MuteTiming { get; init; }
        public byte? EncryptionType { get; init; }
        public byte? TotPredict { get; init; }
        public byte? TxPowerAgc { get; init; }
        public byte? NoaaMoni { get; init; }
        public byte? NoaaScan { get; init; }
        public byte? Noaa { get; init; }
        public byte? NoaaChannel { get; init; }

        // Digital Func tab - all 15 fields confirmed via 3 live
        // differential writes 2026-07-28. Found a real encoding bug shared
        // by 5 fields: GroupCallHoldTime/PrivateCallHoldTime/
        // ManualDialGroupCallHoldTime/ManualDialPrivateCallHoldTime (all
        // TgHoldTimeOptions) and VoiceHeaderRepetitions are NOT zero-based
        // combo indexes - the raw byte is the literal physical value
        // (seconds, repetition count), same encoding class as SteTimeText -
        // see OffsetLabelFor/OffsetIndexFor and each Text property's use of
        // them. Digital Protocol has no known offset (locked to its one
        // option, untestable) and is deliberately not exposed.
        public byte? GroupCallHoldTime { get; init; }
        public byte? PrivateCallHoldTime { get; init; }
        public byte? ManualDialGroupCallHoldTime { get; init; }
        public byte? ManualDialPrivateCallHoldTime { get; init; }
        public byte? VoiceHeaderRepetitions { get; init; }
        public byte? TxPreambleDuration { get; init; }
        public byte? FilterOwnId { get; init; }
        public byte? DigitalRemoteKill { get; init; }
        public byte? DigitalMonitor { get; init; }
        public byte? DigitalMonitorCc { get; init; }
        public byte? DigitalMonitorId { get; init; }
        public byte? MonitorSlotHold { get; init; }
        public byte? RemoteMonitor { get; init; }
        public byte? SmsFormat { get; init; }
        public byte? ResetDigitalProtocol { get; init; }

        // GPS/Ranging tab - GpsPositioning/TimeZone/GpsMode live-write-confirmed
        // 2026-07-28 at their existing read-confirmed offsets. GpsPower,
        // RangingInterval, DistanceUnit, GpsTemplateInformation, and
        // GpsInformationChar are NOT included here - the real vendor
        // CPS doesn't show those fields for this radio at all (kept in the UI,
        // disabled, for a possible future different radio). GpsRoaming also
        // NOT included - its field stays disabled in the vendor CPS UI even
        // with GpsPositioning On, apparently gated by a setting on the Auto
        // Roaming tab; revisit when that tab is picked up.
        public byte? GpsPositioning { get; init; }
        public byte? TimeZone { get; init; }
        public byte? GpsMode { get; init; }

        // VFO Scan tab - live-write-confirmed 2026-07-28 at their existing
        // read-confirmed offsets. The 4 frequency ints use the same MHz*100000
        // convention as ChannelEntry's own frequency fields (confirmed within
        // 1 raw unit / 0.1 Hz, plain binary here rather than BCD).
        public byte? VfoScanType { get; init; }
        public int? VfoScanStartFreqUhf { get; init; }
        public int? VfoScanEndFreqUhf { get; init; }
        public int? VfoScanStartFreqVhf { get; init; }
        public int? VfoScanEndFreqVhf { get; init; }

        // Auto Repeater tab - live-write-confirmed 2026-07-28. AutoRepeaterA/B,
        // the 8 frequency fields, AutoRoaming/FixedTime/EffectWaitTime, and
        // RepeaterMode/CcLimit/SlotA/SlotB matched their reference-claimed
        // offsets exactly. AutoRepeater1/2Uhf/Vhf turned out to be a 2-item
        // list (600.00kHz/5.00000MHz) plus a separate 0xFF "Off" sentinel, not
        // a plain 3-item list - see AutoRepeaterOffsetOptions' doc comment.
        // RepeaterCheck/RepeaterCheckInterval/RepeaterCheckReconnections/
        // AutoRoamingStartCondition/RepeaterOutOfRangeNotify/OutOfRangeNotify
        // were ALL off by one byte from the reference project's claims (found
        // via focused single-field differential tests after a noisy batch
        // test made it clear something was wrong) - see the corrected offsets
        // in EncodeMain/Decode. RoamingZone (0xd5, shares its offset with the
        // already-confirmed AddressBookSentWithCode) is deliberately NOT
        // included here - needs its own dedicated differential test.
        public byte? AutoRepeaterA { get; init; }
        public byte? AutoRepeaterB { get; init; }
        public byte? AutoRepeater1Uhf { get; init; }
        public byte? AutoRepeater1Vhf { get; init; }
        public byte? AutoRepeater2Uhf { get; init; }
        public byte? AutoRepeater2Vhf { get; init; }
        public byte? RepeaterCheck { get; init; }
        public byte? RepeaterCheckInterval { get; init; }
        public byte? RepeaterCheckReconnections { get; init; }
        public byte? RepeaterOutOfRangeNotify { get; init; }
        public byte? OutOfRangeNotify { get; init; }
        public byte? AutoRoaming { get; init; }
        public byte? AutoRoamingStartCondition { get; init; }
        public byte? AutoRoamingFixedTime { get; init; }
        public byte? RoamingEffectWaitTime { get; init; }
        public int? AutoRepeater1MinFreqVhf { get; init; }
        public int? AutoRepeater1MaxFreqVhf { get; init; }
        public int? AutoRepeater1MinFreqUhf { get; init; }
        public int? AutoRepeater1MaxFreqUhf { get; init; }
        public int? AutoRepeater2MinFreqVhf { get; init; }
        public int? AutoRepeater2MaxFreqVhf { get; init; }
        public int? AutoRepeater2MinFreqUhf { get; init; }
        public int? AutoRepeater2MaxFreqUhf { get; init; }
        public byte? RepeaterMode { get; init; }
        public byte? RepCcLimit { get; init; }
        public byte? RepSlotA { get; init; }
        public byte? RepSlotB { get; init; }
        public byte? RepeaterWhitelist { get; init; }

        // Record tab - live-write-confirmed 2026-07-28 at their reference-
        // claimed offsets exactly, no bugs found.
        public byte? RecordFunction { get; init; }
        public byte? RecordDelay { get; init; }

        // Volume/Audio tab - live-write-confirmed 2026-07-28. MaxVolume/
        // MaxHeadphoneVolume/EnhancedSoundQuality/DigiMicGain/AnalogMicGain
        // matched their reference-claimed offsets exactly (MicGainOptions'
        // missing "Auto" 6th option fixed separately). PowerOnVolumeType/
        // PowerOnVolume/RxAgc were previously raw/unconfirmed - all 3
        // confirmed to be plain enums (Preset/Minimum, the MaxVolumeOptions
        // scale, and On/Off respectively). SubSpkInTx/RxNoiseReduction/
        // TxNoiseReduction found from scratch (0x142/0x148/0x149) - no
        // reference-project offset existed for any of them.
        public byte? MaxVolume { get; init; }
        public byte? PowerOnVolumeType { get; init; }
        public byte? PowerOnVolume { get; init; }
        public byte? MaxHeadphoneVolume { get; init; }
        public byte? DigiMicGain { get; init; }
        public byte? EnhancedSoundQuality { get; init; }
        public byte? AnalogMicGain { get; init; }
        public byte? RxAgc { get; init; }
        public byte? NxMicGain { get; init; }
        public byte? SubSpkInTx { get; init; }
        public byte? RxNoiseReduction { get; init; }
        public byte? TxNoiseReduction { get; init; }

        // Satellite tab - live-write-confirmed 2026-07-28 at their reference-
        // claimed offsets exactly, no bugs found. Last of the 18 Optional
        // Settings sub-tabs to get write support.
        public byte? SatLocation { get; init; }
        public byte? SatTxPower { get; init; }
        public byte? SatAnaSql { get; init; }
        public byte? SatAosLimit { get; init; }

        // RoamingZone (Auto Repeater tab) - the last remaining "not yet
        // writable" Optional Settings field. Live-write-confirmed 2026-07-28
        // via a dedicated two-step differential test to be at 0xdb (see
        // Decode's doc comment) - the reference project's claimed 0xd5 was
        // never a real collision with AddressBookSentWithCode, just a wrong
        // offset.
        public byte? RoamingZone { get; init; }
    }

    /// <summary>RMW encode for the data_3500000 block's Power-on fields (see
    /// <see cref="PowerOnFieldPatch"/>) - every other byte in this
    /// 0x160-byte record is left untouched, same discipline as
    /// <see cref="ChannelCodec.Encode"/>.</summary>
    public static byte[] EncodeMain(ReadOnlySpan<byte> currentData3500000, PowerOnFieldPatch patch)
    {
        if (currentData3500000.Length != MainDataLength)
        {
            throw new ArgumentException($"Optional Settings main record must be exactly {MainDataLength} bytes.", nameof(currentData3500000));
        }

        var result = currentData3500000.ToArray();

        if (patch.PowerOnInterface is { } powerOnInterface)
        {
            result[0x6] = powerOnInterface;
        }

        if (patch.PowerOnPassword is { } powerOnPassword)
        {
            result[0x7] = powerOnPassword;
        }

        if (patch.DefaultStartupChannel is { } defaultStartupChannel)
        {
            result[0xd6] = defaultStartupChannel;
        }

        if (patch.StartupZoneA is { } startupZoneA)
        {
            result[0xd7] = startupZoneA;
        }

        if (patch.StartupZoneB is { } startupZoneB)
        {
            result[0xd8] = startupZoneB;
        }

        if (patch.StartupChannelA is { } startupChannelA)
        {
            result[0xd9] = startupChannelA;
        }

        if (patch.StartupChannelB is { } startupChannelB)
        {
            result[0xda] = startupChannelB;
        }

        if (patch.StartupReset is { } startupReset)
        {
            result[0xea] = startupReset;
        }

        if (patch.SmsAlert is { } smsAlert)
        {
            result[0x29] = smsAlert;
        }

        if (patch.CallAlert is { } callAlert)
        {
            result[0x2f] = callAlert;
        }

        if (patch.DigiCallResetTone is { } digiCallResetTone)
        {
            result[0x32] = digiCallResetTone;
        }

        if (patch.TalkPermit is { } talkPermit)
        {
            result[0x31] = talkPermit;
        }

        if (patch.KeyTone is { } keyTone)
        {
            result[0x00] = keyTone;
        }

        if (patch.DigiIdleChannelTone is { } digiIdleChannelTone)
        {
            result[0x36] = digiIdleChannelTone;
        }

        if (patch.StartupSound is { } startupSound)
        {
            result[0x39] = startupSound;
        }

        if (patch.AnalogIdleChannelTone is { } analogIdleChannelTone)
        {
            result[0x111] = analogIdleChannelTone;
        }

        if (patch.CallPermitTones is { } callPermitTones)
        {
            EncodeToneGroup(result, 0x72, 0x7c, callPermitTones);
        }

        if (patch.MatchEndTones is { } matchEndTones)
        {
            EncodeToneGroup(result, 0x86, 0x90, matchEndTones);
        }

        if (patch.CallResetTones is { } callResetTones)
        {
            EncodeToneGroup(result, 0x9a, 0xa4, callResetTones);
        }

        if (patch.UnMatchEndTones is { } unMatchEndTones)
        {
            EncodeToneGroup(result, 0x116, 0x120, unMatchEndTones);
        }

        if (patch.CallAllTones is { } callAllTones)
        {
            EncodeToneGroup(result, 0x12a, 0x134, callAllTones);
        }

        if (patch.AutoShutdown is { } autoShutdown)
        {
            result[0x3] = autoShutdown;
        }

        if (patch.PowerSave is { } powerSave)
        {
            result[0xb] = powerSave;
        }

        if (patch.AutoShutdownType is { } autoShutdownType)
        {
            result[0x10f] = autoShutdownType;
        }


        if (patch.Brightness is { } brightness)
        {
            result[0x26] = brightness;
        }

        if (patch.AutoBacklightDuration is { } autoBacklightDuration)
        {
            result[0x27] = autoBacklightDuration;
        }

        if (patch.BacklightTxDelay is { } backlightTxDelay)
        {
            result[0xe0] = backlightTxDelay;
        }

        if (patch.MenuExitTime is { } menuExitTime)
        {
            result[0x37] = menuExitTime;
        }

        if (patch.TimeDisplay is { } timeDisplay)
        {
            result[0x51] = timeDisplay;
        }

        if (patch.LastCaller is { } lastCaller)
        {
            result[0x4d] = lastCaller;
        }

        if (patch.CallDisplayMode is { } callDisplayMode)
        {
            result[0xaf] = callDisplayMode;
        }

        if (patch.CallsignDisplayColor is { } callsignDisplayColor)
        {
            result[0xbc] = callsignDisplayColor;
        }

        if (patch.CallEndPromptBox is { } callEndPromptBox)
        {
            result[0x3a] = callEndPromptBox;
        }

        if (patch.DisplayChannelNumber is { } displayChannelNumber)
        {
            result[0xb8] = displayChannelNumber;
        }

        if (patch.DisplayCurrentContact is { } displayCurrentContact)
        {
            result[0xb9] = displayCurrentContact;
        }

        if (patch.StandbyCharColor is { } standbyCharColor)
        {
            result[0xc0] = standbyCharColor;
        }

        if (patch.StandbyBkPicture is { } standbyBkPicture)
        {
            result[0xc1] = standbyBkPicture;
        }

        if (patch.ShowLastCallOnLaunch is { } showLastCallOnLaunch)
        {
            result[0xc2] = showLastCallOnLaunch;
        }

        if (patch.SeparateDisplay is { } separateDisplay)
        {
            result[0xe1] = separateDisplay;
        }

        if (patch.ChSwitchingKeepsCaller is { } chSwitchingKeepsCaller)
        {
            result[0xe2] = chSwitchingKeepsCaller;
        }

        if (patch.BacklightRxDelay is { } backlightRxDelay)
        {
            result[0xe5] = backlightRxDelay;
        }

        if (patch.ChannelNameColorA is { } channelNameColorA)
        {
            result[0xe3] = channelNameColorA;
        }

        if (patch.ChannelNameColorB is { } channelNameColorB)
        {
            result[0x109] = channelNameColorB;
        }

        if (patch.ZoneNameColorA is { } zoneNameColorA)
        {
            result[0x10d] = zoneNameColorA;
        }

        if (patch.ZoneNameColorB is { } zoneNameColorB)
        {
            result[0x10e] = zoneNameColorB;
        }

        if (patch.DateDisplayFormat is { } dateDisplayFormat)
        {
            result[0x112] = dateDisplayFormat;
        }

        if (patch.VolumeBar is { } volumeBar)
        {
            result[0x47] = volumeBar;
        }

        if (patch.NightMode is { } nightMode)
        {
            result[0x14d] = nightMode;
        }

        if (patch.DisplayChannelType is { } displayChannelType)
        {
            result[0x110] = (byte)(displayChannelType ? (result[0x110] | 0x01) : (result[0x110] & ~0x01));
        }

        if (patch.DisplayTimeSlot is { } displayTimeSlot)
        {
            result[0x110] = (byte)(displayTimeSlot ? (result[0x110] | 0x02) : (result[0x110] & ~0x02));
        }

        if (patch.DisplayColorCode is { } displayColorCode)
        {
            result[0x110] = (byte)(displayColorCode ? (result[0x110] | 0x04) : (result[0x110] & ~0x04));
        }


        if (patch.DisplayMode is { } displayMode)
        {
            result[0x1] = displayMode;
        }

        if (patch.VfMrA is { } vfMrA)
        {
            result[0x15] = vfMrA;
        }

        if (patch.MemZoneA is { } memZoneA)
        {
            result[0x1f] = memZoneA;
        }

        if (patch.VfMrB is { } vfMrB)
        {
            result[0x16] = vfMrB;
        }

        if (patch.MemZoneB is { } memZoneB)
        {
            result[0x20] = memZoneB;
        }

        if (patch.MainChannelSet is { } mainChannelSet)
        {
            result[0x2c] = mainChannelSet;
        }

        if (patch.SubChannelMode is { } subChannelMode)
        {
            result[0x2d] = subChannelMode;
        }

        if (patch.WorkingMode is { } workingMode)
        {
            result[0x34] = workingMode;
        }

        if (patch.VoxLevel is { } voxLevel)
        {
            result[0x0c] = voxLevel;
        }

        if (patch.VoxDelay is { } voxDelay)
        {
            result[0x0d] = voxDelay;
        }

        if (patch.VoxDetection is { } voxDetection)
        {
            result[0x33] = voxDetection;
        }

        if (patch.SteTypeOfCtcss is { } steTypeOfCtcss)
        {
            result[0x17] = steTypeOfCtcss;
        }

        if (patch.SteWhenNoSignal is { } steWhenNoSignal)
        {
            result[0x18] = steWhenNoSignal;
        }

        if (patch.SteTime is { } steTime)
        {
            result[0x106] = steTime;
        }

        if (patch.AmFmFunction is { } amFmFunction)
        {
            result[0x21] = amFmFunction;
        }

        if (patch.FmVfoMem is { } fmVfoMem)
        {
            result[0x1e] = fmVfoMem;
        }

        if (patch.FmWorkChannel is { } fmWorkChannel)
        {
            result[0x1d] = fmWorkChannel;
        }

        if (patch.FmMonitor is { } fmMonitor)
        {
            result[0x2a] = fmMonitor;
        }

        if (patch.AmVfoMem is { } amVfoMem)
        {
            result[0x13f] = amVfoMem;
        }

        if (patch.AmOffset is { } amOffset)
        {
            result[0x140] = amOffset;
        }

        if (patch.AmSqlLevel is { } amSqlLevel)
        {
            result[0x141] = amSqlLevel;
        }

        if (patch.FrequencyStep is { } frequencyStep)
        {
            result[0x159] = frequencyStep;
        }

        if (patch.KeyLock is { } keyLock)
        {
            result[0x02] = keyLock;
        }

        if (patch.Pf1ShortKey is { } pf1ShortKey)
        {
            result[0x10] = pf1ShortKey;
        }

        if (patch.Pf2ShortKey is { } pf2ShortKey)
        {
            result[0x11] = pf2ShortKey;
        }

        if (patch.Pf3ShortKey is { } pf3ShortKey)
        {
            result[0x12] = pf3ShortKey;
        }

        if (patch.P1ShortKey is { } p1ShortKey)
        {
            result[0x13] = p1ShortKey;
        }

        if (patch.P2ShortKey is { } p2ShortKey)
        {
            result[0x14] = p2ShortKey;
        }

        if (patch.Pf1LongKey is { } pf1LongKey)
        {
            result[0x41] = pf1LongKey;
        }

        if (patch.Pf2LongKey is { } pf2LongKey)
        {
            result[0x42] = pf2LongKey;
        }

        if (patch.Pf3LongKey is { } pf3LongKey)
        {
            result[0x43] = pf3LongKey;
        }

        if (patch.P1LongKey is { } p1LongKey)
        {
            result[0x44] = p1LongKey;
        }

        if (patch.P2LongKey is { } p2LongKey)
        {
            result[0x45] = p2LongKey;
        }

        if (patch.LongKeyTime is { } longKeyTime)
        {
            result[0x46] = longKeyTime;
        }

        if (patch.KnobLock is { } knobLock)
        {
            result[0xbe] = (byte)(knobLock ? (result[0xbe] | 0x01) : (result[0xbe] & ~0x01));
        }

        if (patch.KeyboardLock is { } keyboardLock)
        {
            result[0xbe] = (byte)(keyboardLock ? (result[0xbe] | 0x02) : (result[0xbe] & ~0x02));
        }

        if (patch.SideKeyLock is { } sideKeyLock)
        {
            result[0xbe] = (byte)(sideKeyLock ? (result[0xbe] | 0x08) : (result[0xbe] & ~0x08));
        }

        if (patch.ForcedKeyLock is { } forcedKeyLock)
        {
            result[0xbe] = (byte)(forcedKeyLock ? (result[0xbe] | 0x10) : (result[0xbe] & ~0x10));
        }

        if (patch.AddressBookSentWithCode is { } addressBookSentWithCode)
        {
            result[0xd5] = addressBookSentWithCode;
        }

        if (patch.Tot is { } tot)
        {
            result[0x04] = tot;
        }

        if (patch.Language is { } language)
        {
            result[0x05] = language;
        }

        if (patch.GeneralFrequencyStep is { } generalFrequencyStep)
        {
            result[0x08] = generalFrequencyStep;
        }

        if (patch.SqlLevelA is { } sqlLevelA)
        {
            result[0x09] = sqlLevelA;
        }

        if (patch.SqlLevelB is { } sqlLevelB)
        {
            result[0x0a] = sqlLevelB;
        }

        if (patch.Tbst is { } tbst)
        {
            result[0x2e] = tbst;
        }

        if (patch.AnalogCallHoldTime is { } analogCallHoldTime)
        {
            result[0x50] = analogCallHoldTime;
        }

        if (patch.CallChannelMaintained is { } callChannelMaintained)
        {
            result[0x6e] = callChannelMaintained;
        }

        if (patch.PriorityZoneA is { } priorityZoneA)
        {
            result[0x6f] = priorityZoneA;
        }

        if (patch.PriorityZoneB is { } priorityZoneB)
        {
            result[0x70] = priorityZoneB;
        }

        if (patch.MuteTiming is { } muteTiming)
        {
            result[0xe8] = muteTiming;
        }

        if (patch.EncryptionType is { } encryptionType)
        {
            result[0x10a] = encryptionType;
        }

        if (patch.TotPredict is { } totPredict)
        {
            result[0x10b] = totPredict;
        }

        if (patch.TxPowerAgc is { } txPowerAgc)
        {
            result[0x10c] = txPowerAgc;
        }

        if (patch.NoaaMoni is { } noaaMoni)
        {
            result[0x157] = noaaMoni;
        }

        if (patch.NoaaScan is { } noaaScan)
        {
            result[0x158] = noaaScan;
        }

        if (patch.Noaa is { } noaa)
        {
            result[0xef] = noaa;
        }

        if (patch.NoaaChannel is { } noaaChannel)
        {
            result[0x13e] = noaaChannel;
        }

        if (patch.GroupCallHoldTime is { } groupCallHoldTime)
        {
            result[0x19] = groupCallHoldTime;
        }

        if (patch.PrivateCallHoldTime is { } privateCallHoldTime)
        {
            result[0x1a] = privateCallHoldTime;
        }

        if (patch.ManualDialGroupCallHoldTime is { } manualDialGroupCallHoldTime)
        {
            result[0x107] = manualDialGroupCallHoldTime;
        }

        if (patch.ManualDialPrivateCallHoldTime is { } manualDialPrivateCallHoldTime)
        {
            result[0x108] = manualDialPrivateCallHoldTime;
        }

        if (patch.VoiceHeaderRepetitions is { } voiceHeaderRepetitions)
        {
            result[0x1b] = voiceHeaderRepetitions;
        }

        if (patch.TxPreambleDuration is { } txPreambleDuration)
        {
            result[0x1c] = txPreambleDuration;
        }

        if (patch.FilterOwnId is { } filterOwnId)
        {
            result[0x38] = filterOwnId;
        }

        if (patch.DigitalRemoteKill is { } digitalRemoteKill)
        {
            result[0x3c] = digitalRemoteKill;
        }

        if (patch.DigitalMonitor is { } digitalMonitor)
        {
            result[0x49] = digitalMonitor;
        }

        if (patch.DigitalMonitorCc is { } digitalMonitorCc)
        {
            result[0x4a] = digitalMonitorCc;
        }

        if (patch.DigitalMonitorId is { } digitalMonitorId)
        {
            result[0x4b] = digitalMonitorId;
        }

        if (patch.MonitorSlotHold is { } monitorSlotHold)
        {
            result[0x4c] = monitorSlotHold;
        }

        if (patch.RemoteMonitor is { } remoteMonitor)
        {
            result[0x3e] = remoteMonitor;
        }

        if (patch.SmsFormat is { } smsFormat)
        {
            result[0xc3] = smsFormat;
        }

        if (patch.ResetDigitalProtocol is { } resetDigitalProtocol)
        {
            result[0x154] = resetDigitalProtocol;
        }

        if (patch.GpsPositioning is { } gpsPositioning)
        {
            result[0x3f] = gpsPositioning;
        }

        if (patch.TimeZone is { } timeZone)
        {
            result[0x30] = timeZone;
        }

        if (patch.GpsMode is { } gpsMode)
        {
            result[0x105] = gpsMode;
        }

        if (patch.VfoScanType is { } vfoScanType)
        {
            result[0xe] = vfoScanType;
        }

        if (patch.VfoScanStartFreqUhf is { } vfoScanStartFreqUhf)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x58, 4), (uint)vfoScanStartFreqUhf);
        }

        if (patch.VfoScanEndFreqUhf is { } vfoScanEndFreqUhf)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x5c, 4), (uint)vfoScanEndFreqUhf);
        }

        if (patch.VfoScanStartFreqVhf is { } vfoScanStartFreqVhf)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x60, 4), (uint)vfoScanStartFreqVhf);
        }

        if (patch.VfoScanEndFreqVhf is { } vfoScanEndFreqVhf)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x64, 4), (uint)vfoScanEndFreqVhf);
        }

        if (patch.AutoRepeaterA is { } autoRepeaterA)
        {
            result[0x48] = autoRepeaterA;
        }

        if (patch.AutoRepeaterB is { } autoRepeaterB)
        {
            result[0xd4] = autoRepeaterB;
        }

        if (patch.AutoRepeater1Uhf is { } autoRepeater1Uhf)
        {
            result[0x68] = autoRepeater1Uhf;
        }

        if (patch.AutoRepeater1Vhf is { } autoRepeater1Vhf)
        {
            result[0x69] = autoRepeater1Vhf;
        }

        if (patch.AutoRepeater2Uhf is { } autoRepeater2Uhf)
        {
            result[0xf1] = autoRepeater2Uhf;
        }

        if (patch.AutoRepeater2Vhf is { } autoRepeater2Vhf)
        {
            result[0xf2] = autoRepeater2Vhf;
        }

        // Confirmed offsets 0xdc-0xdf are shifted by one byte from the
        // reference project's claims (0xdd-0xe0) - see PowerOnFieldPatch's
        // doc comment for how this was found.
        if (patch.RepeaterCheck is { } repeaterCheck)
        {
            result[0xdc] = repeaterCheck;
        }

        if (patch.RepeaterCheckInterval is { } repeaterCheckInterval)
        {
            result[0xdd] = repeaterCheckInterval;
        }

        if (patch.RepeaterCheckReconnections is { } repeaterCheckReconnections)
        {
            result[0xde] = repeaterCheckReconnections;
        }

        if (patch.AutoRoamingStartCondition is { } autoRoamingStartCondition)
        {
            result[0xdf] = autoRoamingStartCondition;
        }

        if (patch.RepeaterOutOfRangeNotify is { } repeaterOutOfRangeNotify)
        {
            result[0xe4] = repeaterOutOfRangeNotify;
        }

        if (patch.OutOfRangeNotify is { } outOfRangeNotify)
        {
            result[0xe9] = outOfRangeNotify;
        }

        if (patch.AutoRoaming is { } autoRoaming)
        {
            result[0xe7] = autoRoaming;
        }

        if (patch.AutoRoamingFixedTime is { } autoRoamingFixedTime)
        {
            result[0xba] = autoRoamingFixedTime;
        }

        if (patch.RoamingEffectWaitTime is { } roamingEffectWaitTime)
        {
            result[0xbf] = roamingEffectWaitTime;
        }

        if (patch.AutoRepeater1MinFreqVhf is { } autoRepeater1MinFreqVhf)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0xc4, 4), (uint)autoRepeater1MinFreqVhf);
        }

        if (patch.AutoRepeater1MaxFreqVhf is { } autoRepeater1MaxFreqVhf)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0xc8, 4), (uint)autoRepeater1MaxFreqVhf);
        }

        if (patch.AutoRepeater1MinFreqUhf is { } autoRepeater1MinFreqUhf)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0xcc, 4), (uint)autoRepeater1MinFreqUhf);
        }

        if (patch.AutoRepeater1MaxFreqUhf is { } autoRepeater1MaxFreqUhf)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0xd0, 4), (uint)autoRepeater1MaxFreqUhf);
        }

        if (patch.AutoRepeater2MinFreqVhf is { } autoRepeater2MinFreqVhf)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0xf4, 4), (uint)autoRepeater2MinFreqVhf);
        }

        if (patch.AutoRepeater2MaxFreqVhf is { } autoRepeater2MaxFreqVhf)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0xf8, 4), (uint)autoRepeater2MaxFreqVhf);
        }

        if (patch.AutoRepeater2MinFreqUhf is { } autoRepeater2MinFreqUhf)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0xfc, 4), (uint)autoRepeater2MinFreqUhf);
        }

        if (patch.AutoRepeater2MaxFreqUhf is { } autoRepeater2MaxFreqUhf)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(0x100, 4), (uint)autoRepeater2MaxFreqUhf);
        }

        if (patch.RepeaterMode is { } repeaterMode)
        {
            result[0x143] = repeaterMode;
        }

        if (patch.RepCcLimit is { } repCcLimit)
        {
            result[0x144] = repCcLimit;
        }

        if (patch.RepSlotA is { } repSlotA)
        {
            result[0x145] = repSlotA;
        }

        if (patch.RepSlotB is { } repSlotB)
        {
            result[0x146] = repSlotB;
        }

        if (patch.RepeaterWhitelist is { } repeaterWhitelist)
        {
            result[0x15a] = repeaterWhitelist;
        }

        if (patch.RecordFunction is { } recordFunction)
        {
            result[0x22] = recordFunction;
        }

        if (patch.RecordDelay is { } recordDelay)
        {
            result[0xae] = recordDelay;
        }

        if (patch.MaxVolume is { } maxVolume)
        {
            result[0x3b] = maxVolume;
        }

        if (patch.PowerOnVolumeType is { } powerOnVolumeType)
        {
            result[0x155] = powerOnVolumeType;
        }

        if (patch.PowerOnVolume is { } powerOnVolume)
        {
            result[0x156] = powerOnVolume;
        }

        if (patch.MaxHeadphoneVolume is { } maxHeadphoneVolume)
        {
            result[0x52] = maxHeadphoneVolume;
        }

        if (patch.DigiMicGain is { } digiMicGain)
        {
            result[0xf] = digiMicGain;
        }

        if (patch.EnhancedSoundQuality is { } enhancedSoundQuality)
        {
            result[0x57] = enhancedSoundQuality;
        }

        if (patch.AnalogMicGain is { } analogMicGain)
        {
            result[0x113] = analogMicGain;
        }

        if (patch.RxAgc is { } rxAgc)
        {
            result[0x147] = rxAgc;
        }

        if (patch.NxMicGain is { } nxMicGain)
        {
            result[0x153] = nxMicGain;
        }

        if (patch.SubSpkInTx is { } subSpkInTx)
        {
            result[0x142] = subSpkInTx;
        }

        if (patch.RxNoiseReduction is { } rxNoiseReduction)
        {
            result[0x148] = rxNoiseReduction;
        }

        if (patch.TxNoiseReduction is { } txNoiseReduction)
        {
            result[0x149] = txNoiseReduction;
        }

        if (patch.SatLocation is { } satLocation)
        {
            result[0x14e] = satLocation;
        }

        if (patch.SatTxPower is { } satTxPower)
        {
            result[0x14f] = satTxPower;
        }

        if (patch.SatAnaSql is { } satAnaSql)
        {
            result[0x150] = satAnaSql;
        }

        if (patch.SatAosLimit is { } satAosLimit)
        {
            result[0x151] = satAosLimit;
        }

        if (patch.RoamingZone is { } roamingZone)
        {
            result[0xdb] = roamingZone;
        }

        return result;
    }

    /// <summary>Writes all 5 (Frequency, Period) tones of one Alert Tone
    /// category into <paramref name="result"/> at the given base offsets -
    /// see AlertToneCodec.Categories' doc comment for why CallEnd/
    /// UnMatchEnd's offset-to-name assignment matters here.</summary>
    private static void EncodeToneGroup(byte[] result, int freqBase, int periodBase, IReadOnlyList<(ushort Frequency, ushort Period)> tones)
    {
        for (var tone = 0; tone < 5; tone++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(freqBase + tone * 2, 2), tones[tone].Frequency);
            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(periodBase + tone * 2, 2), tones[tone].Period);
        }
    }

    /// <summary>RMW encode for the data_3500900 block's 3 Power-on text
    /// fields - see <see cref="Decode"/>'s doc comment for the Line2/
    /// PasswordChar offset history this mirrors.</summary>
    public static byte[] EncodeDisplay(ReadOnlySpan<byte> currentData3500900, PowerOnFieldPatch patch)
    {
        if (currentData3500900.Length != SecondaryDataLength)
        {
            throw new ArgumentException($"Optional Settings display record must be exactly {SecondaryDataLength} bytes.", nameof(currentData3500900));
        }

        var result = currentData3500900.ToArray();

        if (patch.PowerOnDisplayLine1 is { } line1)
        {
            TextFieldCodec.EncodeName(line1, 0x1c).CopyTo(result, 0x0);
        }

        if (patch.PowerOnDisplayLine2 is { } line2)
        {
            TextFieldCodec.EncodeName(line2, 0x1c).CopyTo(result, 0x20);
        }

        if (patch.PowerOnPasswordChar is { } passwordChar)
        {
            AsciiTextCodec.Encode(passwordChar, 0x8).CopyTo(result, 0x40);
        }

        return result;
    }

    public sealed record DecodedOptionalSettings
    {
        public byte PowerOnInterface { get; init; }
        public string PowerOnDisplayLine1 { get; init; } = "";
        public string PowerOnDisplayLine2 { get; init; } = "";
        public byte PowerOnPassword { get; init; }
        public string PowerOnPasswordChar { get; init; } = "";
        public byte DefaultStartupChannel { get; init; }
        public byte StartupZoneA { get; init; }
        public byte StartupChannelA { get; init; }
        public byte StartupZoneB { get; init; }
        public byte StartupChannelB { get; init; }
        public byte StartupGpsTest { get; init; }
        public byte StartupReset { get; init; }

        public byte Brightness { get; init; }
        public byte AutoBacklightDuration { get; init; }
        public byte BacklightTxDelay { get; init; }
        public byte MenuExitTime { get; init; }
        public byte TimeDisplay { get; init; }
        public byte LastCaller { get; init; }
        public byte CallDisplayMode { get; init; }
        public byte CallsignDisplayColor { get; init; }
        public byte CallEndPromptBox { get; init; }
        public byte DisplayChannelNumber { get; init; }
        public byte DisplayCurrentContact { get; init; }
        public byte StandbyCharColor { get; init; }
        public byte StandbyBkPicture { get; init; }
        public byte ShowLastCallOnLaunch { get; init; }
        public byte SeparateDisplay { get; init; }
        public byte ChSwitchingKeepsCaller { get; init; }
        public byte BacklightRxDelay { get; init; }
        public byte ChannelNameColorA { get; init; }
        public byte ChannelNameColorB { get; init; }
        public byte ZoneNameColorA { get; init; }
        public byte ZoneNameColorB { get; init; }
        public bool DisplayChannelType { get; init; }
        public bool DisplayTimeSlot { get; init; }
        public bool DisplayColorCode { get; init; }
        public byte DateDisplayFormat { get; init; }
        public byte VolumeBar { get; init; }
        public byte NightMode { get; init; }

        public byte KeyLock { get; init; }
        public byte Pf1ShortKey { get; init; }
        public byte Pf2ShortKey { get; init; }
        public byte Pf3ShortKey { get; init; }
        public byte P1ShortKey { get; init; }
        public byte P2ShortKey { get; init; }
        public byte Pf1LongKey { get; init; }
        public byte Pf2LongKey { get; init; }
        public byte Pf3LongKey { get; init; }
        public byte P1LongKey { get; init; }
        public byte P2LongKey { get; init; }
        public byte LongKeyTime { get; init; }
        public bool KnobLock { get; init; }
        public bool KeyboardLock { get; init; }
        public bool SideKeyLock { get; init; }
        public bool ForcedKeyLock { get; init; }

        public byte SmsAlert { get; init; }
        public byte CallAlert { get; init; }
        public byte DigiCallResetTone { get; init; }
        public byte TalkPermit { get; init; }
        public byte KeyTone { get; init; }
        public byte DigiIdleChannelTone { get; init; }
        public byte StartupSound { get; init; }
        public byte ToneKeySoundAdjustable { get; init; }
        public byte AnalogIdleChannelTone { get; init; }
        public byte PluginRecordingTone { get; init; }

        public byte GpsPower { get; init; }
        public byte GpsPositioning { get; init; }
        public byte TimeZone { get; init; }
        public byte RangingInterval { get; init; }
        public byte DistanceUnit { get; init; }
        public byte GpsTemplateInformation { get; init; }
        public string GpsInformationChar { get; init; } = "";
        public byte GpsMode { get; init; }
        public byte GpsRoaming { get; init; }

        public byte VfoScanType { get; init; }
        public int VfoScanStartFreqUhf { get; init; }
        public int VfoScanEndFreqUhf { get; init; }
        public int VfoScanStartFreqVhf { get; init; }
        public int VfoScanEndFreqVhf { get; init; }

        public byte AutoRepeaterA { get; init; }
        public byte AutoRepeaterB { get; init; }
        public byte AutoRepeater1Uhf { get; init; }
        public byte AutoRepeater1Vhf { get; init; }
        public byte AutoRepeater2Uhf { get; init; }
        public byte AutoRepeater2Vhf { get; init; }
        public byte RepeaterCheck { get; init; }
        public byte RepeaterCheckInterval { get; init; }
        public byte RepeaterCheckReconnections { get; init; }
        public byte RepeaterOutOfRangeNotify { get; init; }
        public byte OutOfRangeNotify { get; init; }
        public byte AutoRoaming { get; init; }
        public byte AutoRoamingStartCondition { get; init; }
        public byte AutoRoamingFixedTime { get; init; }
        public byte RoamingEffectWaitTime { get; init; }
        public byte RoamingZone { get; init; }
        public int AutoRepeater1MinFreqVhf { get; init; }
        public int AutoRepeater1MaxFreqVhf { get; init; }
        public int AutoRepeater1MinFreqUhf { get; init; }
        public int AutoRepeater1MaxFreqUhf { get; init; }
        public int AutoRepeater2MinFreqVhf { get; init; }
        public int AutoRepeater2MaxFreqVhf { get; init; }
        public int AutoRepeater2MinFreqUhf { get; init; }
        public int AutoRepeater2MaxFreqUhf { get; init; }
        public byte RepeaterMode { get; init; }
        public byte RepCcLimit { get; init; }
        public byte RepSlotA { get; init; }
        public byte RepSlotB { get; init; }
        public byte RepeaterWhitelist { get; init; }

        public byte RecordFunction { get; init; }
        public byte RecordDelay { get; init; }

        public byte MaxVolume { get; init; }
        public byte PowerOnVolumeType { get; init; }
        public byte PowerOnVolume { get; init; }
        public byte MaxHeadphoneVolume { get; init; }
        public byte DigiMicGain { get; init; }
        public byte EnhancedSoundQuality { get; init; }
        public byte AnalogMicGain { get; init; }
        public byte RxAgc { get; init; }
        public byte NxMicGain { get; init; }
        public byte SubSpkInTx { get; init; }
        public byte RxNoiseReduction { get; init; }
        public byte TxNoiseReduction { get; init; }

        public byte DisplayMode { get; init; }
        public byte VfMrA { get; init; }
        public byte VfMrB { get; init; }
        public byte MemZoneA { get; init; }
        public byte MemZoneB { get; init; }
        public byte MainChannelSet { get; init; }
        public byte SubChannelMode { get; init; }
        public byte WorkingMode { get; init; }

        public byte VoxLevel { get; init; }
        public byte VoxDelay { get; init; }
        public byte VoxDetection { get; init; }
        public byte BtOnOff { get; init; }
        public byte BtIntMic { get; init; }
        public byte BtIntSpk { get; init; }
        public byte BtMicGain { get; init; }
        public byte BtSpkGain { get; init; }
        public byte BtHoldTime { get; init; }
        public byte BtRxDelay { get; init; }
        public byte BtPttHold { get; init; }
        public byte BtPttSleepTime { get; init; }
        public byte BtNrBefore { get; init; }
        public byte BtNrAfter { get; init; }

        public byte SteTypeOfCtcss { get; init; }
        public byte SteWhenNoSignal { get; init; }
        public byte SteTime { get; init; }

        public byte AmFmFunction { get; init; }
        public byte FmVfoMem { get; init; }
        public byte FmWorkChannel { get; init; }
        public byte FmMonitor { get; init; }
        public byte AmVfoMem { get; init; }
        public byte AmWorkZone { get; init; }
        public byte AmOffset { get; init; }
        public byte AmSqlLevel { get; init; }

        public byte AutoShutdown { get; init; }
        public byte PowerSave { get; init; }
        public byte AutoShutdownType { get; init; }

        public byte AddressBookSentWithCode { get; init; }
        public byte Tot { get; init; }
        public byte Language { get; init; }
        public byte FrequencyStep { get; init; }
        public byte GeneralFrequencyStep { get; init; }
        public byte SqlLevelA { get; init; }
        public byte SqlLevelB { get; init; }
        public byte Tbst { get; init; }
        public byte AnalogCallHoldTime { get; init; }
        public byte CallChannelMaintained { get; init; }
        public byte PriorityZoneA { get; init; }
        public byte PriorityZoneB { get; init; }
        public byte MuteTiming { get; init; }
        public byte EncryptionType { get; init; }
        public byte TotPredict { get; init; }
        public byte TxPowerAgc { get; init; }
        public byte NoaaMoni { get; init; }
        public byte NoaaScan { get; init; }
        public byte Noaa { get; init; }
        public byte NoaaChannel { get; init; }

        public byte GroupCallHoldTime { get; init; }
        public byte PrivateCallHoldTime { get; init; }
        public byte ManualDialGroupCallHoldTime { get; init; }
        public byte ManualDialPrivateCallHoldTime { get; init; }
        public byte VoiceHeaderRepetitions { get; init; }
        public byte TxPreambleDuration { get; init; }
        public byte FilterOwnId { get; init; }
        public byte DigitalRemoteKill { get; init; }
        public byte DigitalMonitor { get; init; }
        public byte DigitalMonitorCc { get; init; }
        public byte DigitalMonitorId { get; init; }
        public byte MonitorSlotHold { get; init; }
        public byte RemoteMonitor { get; init; }
        public byte SmsFormat { get; init; }
        public byte ResetDigitalProtocol { get; init; }

        public byte SatLocation { get; init; }
        public byte SatTxPower { get; init; }
        public byte SatAnaSql { get; init; }
        public byte SatAosLimit { get; init; }

        public IReadOnlyList<AlertToneCodec.DecodedAlertTone> AlertTones { get; init; } = [];
    }
}

/// <summary>
/// Decodes the 25-entry Alert Tone sub-list (5 categories x 5 tones, each a
/// freq/period pair) out of <see cref="OptionalSettingsCodec"/>'s
/// data_3500000 block. All 5 categories use the identical pattern - a
/// 5-tone freq array immediately followed (at a possibly-distant offset) by
/// a matching 5-tone period array, both 2-byte-little-endian, 2-byte stride.
/// Byte offsets transcribed from optional_settings.cpp's decode_D890UV.
/// </summary>
public static class AlertToneCodec
{
    // Category names confirmed against the vendor CPS's own UI 2026-07-20
    // (english.ini ids 39300-39304): "Call Permit Tone", "Match End Tone"
    // (=CallEnd), "Call Reset Tone", "UnMatch End Tone" (=UnMatchEnd), "All
    // Call End Tone" (=CallAll). The 2nd category was ported from the
    // reference project as "IdleChannel", which doesn't match any of the 5
    // real names.
    //
    // The (0x86,0x90) and (0x116,0x120) pairs were THEMSELVES swapped
    // between CallEnd/UnMatchEnd - found and fixed via a live read
    // 2026-07-20: the vendor CPS's real "Match End Tone" values (1500,
    // 600, 0, 0, 0 / period 100, 100, 0, 0, 0) are the bytes at 0x86/0x90,
    // not 0x116/0x120 (which hold a different, genuinely "UnMatch End
    // Tone" set - 1200, 800, 0, 0, 0). CallPermit (0x72/0x7c) and
    // CallReset (0x9a/0xa4) were independently confirmed correct in the
    // same read (700/150.. and 1900,800/100,100.. respectively) - not
    // touched.
    //
    // UnMatchEnd (0x116/0x120) and CallAll (0x12a/0x134) freq/period offsets
    // re-confirmed via a live differential WRITE 2026-07-28 (no write path
    // existed before that - the 2026-07-20 read only proved the
    // decode side). All 4 test frequencies and 3 of 4 test periods matched
    // byte-for-byte; the 4th (UnMatchEnd tone1 period, requested raw 26 /
    // displayed 260) came back as raw 20 / displayed 200 - confirmed to be
    // the vendor CPS's own UI silently clamping period input to a
    // max of 200 displayed (raw 20), not an offset error. No clamp enforced
    // on this app's own PeriodText setter for this - not yet confirmed whether the
    // limit is universal across all 5 categories or specific to this one.
    private static readonly (string Category, int FreqBase, int PeriodBase)[] Categories =
    [
        ("CallPermit", 0x72, 0x7c),
        ("CallEnd", 0x86, 0x90),
        ("CallReset", 0x9a, 0xa4),
        ("UnMatchEnd", 0x116, 0x120),
        ("CallAll", 0x12a, 0x134)
    ];

    public static List<DecodedAlertTone> DecodeAll(ReadOnlySpan<byte> data3500000)
    {
        var results = new List<DecodedAlertTone>(25);
        foreach (var (category, freqBase, periodBase) in Categories)
        {
            for (var tone = 0; tone < 5; tone++)
            {
                var freq = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(data3500000.Slice(freqBase + tone * 2, 2));
                var period = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(data3500000.Slice(periodBase + tone * 2, 2));
                results.Add(new DecodedAlertTone(category, tone + 1) { Frequency = freq, Period = period });
            }
        }

        return results;
    }

    public sealed record DecodedAlertTone(string Category, int ToneNumber)
    {
        public ushort Frequency { get; init; }
        public ushort Period { get; init; }
    }
}

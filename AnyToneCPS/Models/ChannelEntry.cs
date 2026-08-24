using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using AnyToneCPS.Services.Radio.Codecs;

namespace AnyToneCPS.Models;

/// <summary>
/// Canonical Channel model - typed fields matching
/// <see cref="ChannelCodec.DecodedChannel"/>/<see cref="ChannelCodec.ChannelFieldPatch"/>
/// (the radio's own wire encoding: byte/bool/double/ushort) instead of the
/// display strings this class used to hold for every field - this is the
/// first entity migrated off the old CSV-first, string-everywhere design;
/// others follow later, one at a time.
///
/// Enum-like radio fields (ChannelType, TransmitPower, Bandwidth, CTCSS/DCS
/// mode, Squelch Mode, Optional Signal, Busy-Lock/TX-Permit, PTT ID) keep the
/// raw byte as the canonical value, with a "*Selection" string wrapper for
/// ComboBox binding - the same pattern this class already used for
/// <see cref="ScrambleModeSelection"/>/<see cref="CustomScramblerSelection"/>,
/// just applied uniformly now. Boolean toggle fields (Talkaround, Reverse,
/// etc.) are real `bool` now, not "Off"/"On" strings. Reference fields
/// (Contact/RadioId/ScanList/ReceiveGroupList) store the raw radio index;
/// the display name is resolved by whoever's showing the list (was a stored,
/// independently-editable string before - a latent staleness bug if the
/// referenced entity got renamed elsewhere).
///
/// <see cref="EncryptionMode"/> is no longer
/// independently stored - it's derived from which of
/// <see cref="AesEncryptionIndex"/>/<see cref="Arc4EncryptionKeyIndex"/>/
/// <see cref="DigitalEncryptionIndex"/> is nonzero, fixing a real
/// pre-existing bug where the stored mode string could drift out of sync
/// with the actual key indices (<c>RadioReadMapper.MapChannels</c> never
/// populated it from a live read at all).
/// </summary>
public partial class ChannelEntry : ObservableValidator
{
    private ChannelSnapshot? _cleanSnapshot;

    /// <summary>Separate baseline from <see cref="_cleanSnapshot"/> - "unsaved
    /// to the project file" and "not yet written to the radio" are two
    /// genuinely different targets that used to share one snapshot, which
    /// meant saving the project (<see cref="MarkClean"/>) silently made
    /// Write-to-Radio forget a pending edit it had never actually sent to
    /// the radio. <see cref="MarkRadioSynced"/> is only called after a
    /// successful Read From Radio (baseline = what the radio has now) or a
    /// successful Write (baseline = what was just confirmed written) -
    /// never by Save.</summary>
    private ChannelSnapshot? _radioSyncSnapshot;

    [ObservableProperty] private int _number;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private double _rxFrequencyMHz;
    [ObservableProperty] private double _offsetMHz;
    [ObservableProperty] private byte _offsetDirection; // 0=None,1=+,2=-
    [ObservableProperty] private byte _channelType; // 0=A-Analog,1=D-Digital,2=A+D TX A,3=D+A TX D
    [ObservableProperty] private byte _transmitPower; // 0=Low,1=Mid,2=High,3=Turbo
    [ObservableProperty] private byte _bandwidth; // 0=12.5K,1=25K
    [ObservableProperty] private byte _ctcssDcsDecode; // 0=Off,1=CTCSS,2=DCS
    [ObservableProperty] private byte _ctcssDcsEncode;
    [ObservableProperty] private byte _colorCode; // 0-15, RX color code
    // 0-15, TX color code - confirmed 2026-07-19 via a live differential
    // test to be a genuinely INDEPENDENT field from RX (byte 0x43 vs
    // 0x20) - previously assumed to always equal ColorCode/RX, which was
    // wrong (a real user test set RX=8/TX=9 and both stuck independently).
    [ObservableProperty] private byte _txColorCode;
    [ObservableProperty] private bool _repeaterSlot2; // false=slot 1, true=slot 2 (DecodedChannel.TimeSlot)
    [ObservableProperty] private ushort _contactIndex;
    [ObservableProperty] private ushort _radioIdIndex;
    [ObservableProperty] private byte _busyLock; // 0=Always/Off,1=Channel Free,2=Different CC,3=Same CC
    [ObservableProperty] private byte _squelchMode; // 0-4
    [ObservableProperty] private byte _optionalSignal; // 0=Off,1=DTMF,2=2Tone,3=5Tone
    [ObservableProperty] private byte _pttId; // 0=Off,1=Start,2=End,3=Start&End
    // 255 (0xFF), not 0, is the radio's real "none" sentinel for these two
    // fields - confirmed live 2026-07-19 (every channel read from a real
    // radio had exactly 255 here; 0 would mean "assigned to Scan List #1",
    // a real assignment, not "unassigned"). Defaulting the backing field
    // itself (rather than remembering to set it at every construction
    // site - AddChannel/DuplicateChannel/SeedData/etc.) means a brand new
    // ChannelEntry is correct without every caller needing to know this.
    [ObservableProperty] private ushort _scanListIndex = 255;
    [ObservableProperty] private ushort _receiveGroupListIndex = 255;
    [ObservableProperty] private bool _pttProhibit;
    [ObservableProperty] private bool _reverse;
    [ObservableProperty] private bool _slotSuit;
    [ObservableProperty] private byte _aesEncryptionIndex; // 0=Off
    [ObservableProperty] private bool _callConfirmation;
    [ObservableProperty] private bool _talkAround;
    [ObservableProperty] private bool _workAlone;
    [ObservableProperty] private ushort _customCtcss;
    // Confirmed write-safe 2026-08-02 via live differential tests against
    // real hardware (bytes 0x0a/0x0b/0x0c-0x0d/0x0e-0x0f) - 0-based index
    // into CtcssToneLabels when the corresponding CtcssDcsEncode/
    // CtcssDcsDecode mode is CTCSS, or into DcsCodeLabels when DCS. See
    // EncodeToneSelection/DecodeToneSelection, which combine these with
    // the mode byte into a single per-side dropdown.
    [ObservableProperty] private byte _ctcssEncodeTone;
    [ObservableProperty] private byte _ctcssDecodeTone;
    [ObservableProperty] private ushort _dcsEncodeTone;
    [ObservableProperty] private ushort _dcsDecodeTone;
    [ObservableProperty] private bool _autoScan;
    [ObservableProperty] private bool _smsConfirmation;
    [ObservableProperty] private byte _correctFrequencyHz;
    [ObservableProperty] private byte _dmrModeDcdm; // 0=Off/Normal, 1=Double Slot, 2=TS Split - see ChannelCodec.DmrModeSelectionToString
    // Confirmed write-safe 2026-07-31 - byte 0x34 bit 1, the actual
    // DMO/Simplex-vs-Repeater discriminator (see DmrModeSelection and
    // ChannelCodec.Encode's doc comment).
    [ObservableProperty] private bool _dmrMode;
    [ObservableProperty] private int _scrambleMode;
    [ObservableProperty] private int _customScrambleFrequencyIndex;
    [ObservableProperty] private byte _arc4EncryptionKeyIndex; // 0=Off
    [ObservableProperty] private byte _digitalEncryptionIndex; // 0=Off ("Basic" encryption code index)
    // Confirmed write-safe 2026-07-31 via a live differential test against
    // real hardware (byte 0x34 bit 7, byte 0x3b bits 4/2) - see
    // ChannelCodec.Decode's doc comments for the read-side confirmation.
    [ObservableProperty] private bool _dmrCrcIgnore;
    [ObservableProperty] private bool _sendTalkerAlias;
    [ObservableProperty] private bool _smsForbid;
    // Confirmed write-safe 2026-07-31 via 2 live differential tests against
    // real hardware (byte 0x34 bits 3/2, byte 0x3b bits 1/0) - see
    // ChannelCodec.Decode's doc comments for the read-side confirmation.
    // AesRandomKey/AesMultipleKey only meaningful once an AES key is
    // assigned (AesEncryptionIndex != 0).
    [ObservableProperty] private bool _dataAckDisable;
    [ObservableProperty] private bool _excludeChannelRoaming;
    [ObservableProperty] private bool _aesRandomKey;
    [ObservableProperty] private bool _aesMultipleKey;
    // Confirmed write-safe 2026-08-01 via a live differential test against
    // real hardware (byte 0x21 bit 5) - visible on digital channels only
    // (confirmed against the real vendor CPS).
    [ObservableProperty] private bool _aprsRx;
    // Confirmed write-safe 2026-08-01 via a live differential test against
    // real hardware (byte 0x1f) - 0-based index into the real vendor CPS's
    // M1-M16 DTMF ID list. Analog-only, only meaningful when OptionalSignal
    // is DTMF.
    [ObservableProperty] private byte _dtmfIdIndex;
    // Confirmed write-safe 2026-08-01 via a live differential test against
    // real hardware (byte 0x1d) - 0-based index into the real vendor CPS's
    // 2Tone settings list. Analog-only, only meaningful when
    // OptionalSignal is 2Tone.
    [ObservableProperty] private byte _tone2IdIndex;
    // Confirmed write-safe 2026-08-01 via a live differential test against
    // real hardware (byte 0x1e) - 0-based index into the real vendor CPS's
    // 5Tone settings list. Analog-only, only meaningful when
    // OptionalSignal is 5Tone.
    [ObservableProperty] private byte _tone5IdIndex;
    // Confirmed write-safe 2026-08-01 via a live differential test against
    // real hardware (byte 0x12, raw 0/1 tested) - 0-based, treated as the
    // same 16-slot list as DtmfIdIndex/Tone2IdIndex/Tone5IdIndex since the
    // vendor CPS most likely just shows as many items as are configured
    // (only 2 real 2Tone settings existed at test time - range beyond
    // raw 0/1 not independently confirmed). Analog-only, only meaningful
    // when OptionalSignal is 2Tone.
    [ObservableProperty] private byte _tone2Decode;
    // Confirmed write-safe 2026-08-01/2026-08-02 via live differential
    // tests against real hardware (bytes 0x40/0x41) - real vendor CPS
    // items are "1"/"2"/"Customize"; the 2 presets are raw 0/1, Customize
    // is raw 100 (a sentinel, not the next sequential index - see
    // R5ToneBotSelection). Analog-only, only meaningful when
    // OptionalSignal is 5Tone.
    [ObservableProperty] private byte _r5ToneBot;
    [ObservableProperty] private byte _r5ToneEot;
    // Confirmed write-safe 2026-08-01 via a live differential test against
    // real hardware (byte 0x42, previously unclaimed) - 0-based index into
    // the real vendor CPS's QDC1200 ID list. Analog-only, only meaningful
    // when OptionalSignal is QDC1200.
    [ObservableProperty] private byte _qdcIdIndex;
    // Confirmed write-safe 2026-08-01 via a live differential test against
    // real hardware (byte 0x3b bit 5) - false=AES, true=ARC4, independent
    // of which key index (AesEncryptionIndex/Arc4EncryptionKeyIndex) is
    // populated. Digital-only, gated the same way as the other encryption
    // controls (Optional Settings' Encryption Type must be AES/ARC4).
    [ObservableProperty] private bool _extendEncryption;
    // Confirmed write-safe 2026-08-01 via a live differential test against
    // real hardware (byte 0x34 bit 5) - discovered from scratch, no prior
    // offset known. Analog tab per the original findings list.
    [ObservableProperty] private bool _idleTx;
    // Confirmed write-safe 2026-08-02 via a live differential test against
    // real hardware (byte 0x34 bit 0) - digital-only per the real vendor
    // CPS's channel page (shown next to Call Confirmation/Slot Suit/SMS
    // Confirmation).
    [ObservableProperty] private bool _ranging;
    // Confirmed write-safe 2026-08-01 via a live differential test against
    // real hardware (byte 0x3b bit 7) - false=Off, true=Low priority. A
    // "High priority" write attempt failed with a communication error
    // before completing, so its encoding is unconfirmed and deliberately
    // not exposed. Digital-only per the original findings list.
    [ObservableProperty] private bool _txInterrupt;

    // Cached display names for the reference fields above (ContactIndex/
    // RadioIdIndex/ScanListIndex/ReceiveGroupListIndex are canonical - these
    // are a resolved-once-at-read-time convenience for the UI, refreshed by
    // whoever loads/edits the referenced entity, not independently editable
    // storage in their own right).
    [ObservableProperty] private string _contactDisplayName = "";
    [ObservableProperty] private string _radioIdDisplayName = "";
    [ObservableProperty] private string _receiveGroupListDisplayName = "None";

    public bool IsDigital => ChannelType is 1 or 2 or 3;
    public bool IsAnalog => !IsDigital;
    public bool CanUseEncryption => IsDigital;
    public bool UsesDigitalEncryption => CanUseEncryption && DigitalEncryptionIndex != 0;
    public bool UsesAesEncryption => CanUseEncryption && AesEncryptionIndex != 0;
    public bool UsesArc4Encryption => CanUseEncryption && Arc4EncryptionKeyIndex != 0;
    public string EncryptionMode => UsesAesEncryption ? "AES" : UsesArc4Encryption ? "ARC4" : UsesDigitalEncryption ? "Digital" : "Off";
    public bool IsCustomScramble => ScrambleMode == 15;

    // Confirmed 2026-07-19 against the reference vendor CPS source
    // (channel_edit_dialog.cpp's setModeFormVisibility) - unlike IsAnalog
    // (ChannelType == 0 only, used for Optional Signal/tone fields), the
    // analog signalling group (Reverse, Custom CTCSS, Scrambler) is also
    // available on the two mixed A+D channel types, only truly unavailable
    // on pure Digital (ChannelType == 1).
    public bool HasAnalogCapability => ChannelType != 1;
    // Reverse specifically is further restricted within that group -
    // enabled only for A-Analog and A+D TX A, not D+A TX D.
    public bool CanUseReverse => ChannelType is 0 or 2;
    // Bandwidth is analog-only in the vendor CPS - disabled and forced to
    // 12.5K (raw 0) for every non-Analog channel type.
    public bool CanUseBandwidth => ChannelType == 0;

    // --- ComboBox-friendly "*Selection" string wrappers (raw byte is canonical) ---
    public string ChannelTypeSelection
    {
        get => ChannelCodec.ChannelTypeToString(ChannelType);
        set { if (ChannelCodec.ParseChannelType(value) is { } v) ChannelType = v; }
    }

    public string TransmitPowerSelection
    {
        get => ChannelCodec.TxPowerToString(TransmitPower);
        set { if (ChannelCodec.ParseTxPower(value) is { } v) TransmitPower = v; }
    }

    public string BandwidthSelection
    {
        get => ChannelCodec.BandWidthToString(Bandwidth);
        set { if (ChannelCodec.ParseBandWidth(value) is { } v) Bandwidth = v; }
    }

    public string CtcssDecodeSelection
    {
        get => ChannelCodec.CtcssDcsModeToString(CtcssDcsDecode);
        set { if (ChannelCodec.ParseCtcssDcsMode(value) is { } v) CtcssDcsDecode = v; }
    }

    public string CtcssEncodeSelection
    {
        get => ChannelCodec.CtcssDcsModeToString(CtcssDcsEncode);
        set { if (ChannelCodec.ParseCtcssDcsMode(value) is { } v) CtcssDcsEncode = v; }
    }

    // A single per-side dropdown that shows CtcssToneLabels when the mode
    // is CTCSS or DcsCodeLabels when the mode is DCS - see
    // ChannelCodec.Decode's doc comment for why both raw fields
    // (CtcssEncodeTone/DcsEncodeTone) are plain 0-based list indices.
    public bool IsEncodeToneVisible => CtcssDcsEncode != 0;
    public IReadOnlyList<string> EncodeToneOptions => CtcssDcsEncode == 2 ? DcsCodeLabels : CtcssToneLabels;

    public string EncodeToneSelection
    {
        get => CtcssDcsEncode == 2
            ? DcsCodeLabels[Math.Clamp((int)DcsEncodeTone, 0, DcsCodeLabels.Count - 1)]
            : CtcssEncodeTone < CtcssToneCount ? CtcssToneLabels[CtcssEncodeTone] : CtcssEncodeTone.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (CtcssDcsEncode == 2)
            {
                var idx = DcsCodeLabels.ToList().IndexOf(value);
                if (idx >= 0) DcsEncodeTone = (ushort)idx;
            }
            else
            {
                var idx = CtcssToneLabels.ToList().IndexOf(value);
                if (idx >= 0 && idx < CtcssToneCount) CtcssEncodeTone = (byte)idx;
            }
        }
    }

    public bool IsDecodeToneVisible => CtcssDcsDecode != 0;
    public IReadOnlyList<string> DecodeToneOptions => CtcssDcsDecode == 2 ? DcsCodeLabels : CtcssToneLabels;

    public string DecodeToneSelection
    {
        get => CtcssDcsDecode == 2
            ? DcsCodeLabels[Math.Clamp((int)DcsDecodeTone, 0, DcsCodeLabels.Count - 1)]
            : CtcssDecodeTone < CtcssToneCount ? CtcssToneLabels[CtcssDecodeTone] : CtcssDecodeTone.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (CtcssDcsDecode == 2)
            {
                var idx = DcsCodeLabels.ToList().IndexOf(value);
                if (idx >= 0) DcsDecodeTone = (ushort)idx;
            }
            else
            {
                var idx = CtcssToneLabels.ToList().IndexOf(value);
                if (idx >= 0 && idx < CtcssToneCount) CtcssDecodeTone = (byte)idx;
            }
        }
    }

    public string SquelchModeSelection
    {
        get => ChannelCodec.SquelchModeToString(SquelchMode);
        set { if (ChannelCodec.ParseSquelchMode(value) is { } v) SquelchMode = v; }
    }

    public string OptionalSignalSelection
    {
        get => ChannelCodec.OptionalSignalToString(OptionalSignal);
        set { if (ChannelCodec.ParseOptionalSignal(value) is { } v) OptionalSignal = v; }
    }

    // DTMF ID is only meaningful (and only selectable in the real vendor
    // CPS) when Optional Signal is set to DTMF.
    public bool IsOptionalSignalDtmf => OptionalSignal == 1;

    // 2Tone ID is only meaningful (and only selectable in the real vendor
    // CPS) when Optional Signal is set to 2Tone.
    public bool IsOptionalSignalTwoTone => OptionalSignal == 2;

    // 5Tone ID is only meaningful (and only selectable in the real vendor
    // CPS) when Optional Signal is set to 5Tone.
    public bool IsOptionalSignalFiveTone => OptionalSignal == 3;

    // QDC1200 ID is only meaningful (and only selectable in the real
    // vendor CPS) when Optional Signal is set to QDC1200.
    public bool IsOptionalSignalQdc1200 => OptionalSignal == 4;

    public string BusyLockTxPermitSelection
    {
        get => ChannelCodec.BusyLockToString(BusyLock, IsDigital);
        set { if (ChannelCodec.ParseBusyLock(value, IsDigital) is { } v) BusyLock = v; }
    }

    public string PttIdSelection
    {
        get => ChannelCodec.PttIdToString(PttId);
        set { if (ChannelCodec.ParsePttId(value) is { } v) PttId = v; }
    }

    public string OffsetDirectionSelection
    {
        get => ChannelCodec.OffsetDirectionToString(OffsetDirection);
        set { if (ChannelCodec.ParseOffsetDirection(value) is { } v) OffsetDirection = v; }
    }

    /// <summary>Real vendor CPS input range for a channel's RX/TX frequency -
    /// corrected 2026-08-02 (was 100-999.99999,
    /// the wire format's raw BCD encoding capacity, not the real allowed
    /// range), then corrected AGAIN 2026-08-07: 140-480 as a single
    /// continuous range was still wrong - it silently accepted the
    /// 174-400 MHz dead zone between this radio's real VHF (136-174) and
    /// UHF (400-480) coverage, which a Roaming Channel live capture proved
    /// the vendor CPS itself rejects. See
    /// CodeplugLimits.IsValidVhfOrUhfFrequencyMhz's own doc comment.
    /// ChannelCodec/BcdDecimalCodec.EncodeAsDecimal would still throw
    /// on anything outside 0-999.99999, but this narrower range is what
    /// actually blocks a write attempt via MainViewModel.ValidateChannels.
    /// See <see cref="CodeplugLimits.IsValidVhfOrUhfFrequencyMhz"/> for the
    /// actual real (two-band) check - there is no longer a single min/max
    /// pair here since the valid range is disjoint.</summary>

    // --- Numeric text-entry wrappers ---
    // Deliberately NOT reject-and-revert (see OptionalSettingsEntry.
    // VfoScanStartFreqUhfText's doc comment for the exact bug shape: a
    // 3-digit floor like 140 is unreachable by typing if every keystroke
    // that produces a below-floor prefix gets silently reverted - true here
    // even before this range tightened, since the old 100 floor had the
    // same problem). The raw text is always accepted; ValidateProperty
    // attaches an error via the CustomValidation attribute, and
    // MainViewModel.ValidateChannels still independently blocks Save/Write
    // on an out-of-range RxFrequencyMHz regardless of this property's own
    // error state.
    [CustomValidation(typeof(ChannelEntry), nameof(ValidateFrequencyText))]
    public string RxFrequencyMHzText
    {
        get => RxFrequencyMHz.ToString("000.00000", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(RxFrequencyMHzText));
            if (double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v)
                && CodeplugLimits.IsValidVhfOrUhfFrequencyMhz(v))
            {
                RxFrequencyMHz = v;
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    /// <summary>Editing this recomputes <see cref="OffsetMHz"/>/<see cref="OffsetDirection"/>
    /// from the difference against <see cref="RxFrequencyMHz"/> - the radio only stores
    /// RX + offset + direction, there's no independent "TX frequency" on the wire.</summary>
    [CustomValidation(typeof(ChannelEntry), nameof(ValidateFrequencyText))]
    public string TransmitFrequencyMHzText
    {
        get => ComputeTransmitFrequencyMHz().ToString("000.00000", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(TransmitFrequencyMHzText));
            if (double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var tx)
                && CodeplugLimits.IsValidVhfOrUhfFrequencyMhz(tx))
            {
                OffsetMHz = Math.Abs(tx - RxFrequencyMHz);
                OffsetDirection = tx > RxFrequencyMHz ? (byte)1 : tx < RxFrequencyMHz ? (byte)2 : (byte)0;
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    public static ValidationResult? ValidateFrequencyText(string? value, ValidationContext context)
    {
        if (!double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var mhz))
        {
            return new ValidationResult("Enter a decimal frequency in MHz.", [context.MemberName!]);
        }

        return CodeplugLimits.IsValidVhfOrUhfFrequencyMhz(mhz)
            ? ValidationResult.Success
            : new ValidationResult($"Must be {CodeplugLimits.VhfFrequencyMinMhz:0.00000}-{CodeplugLimits.VhfFrequencyMaxMhz:0.00000} or {CodeplugLimits.UhfFrequencyMinMhz:0.00000}-{CodeplugLimits.UhfFrequencyMaxMhz:0.00000} MHz (the radio's VHF/UHF band limits).", [context.MemberName!]);
    }

    public double ComputeTransmitFrequencyMHz() => OffsetDirection switch
    {
        1 => RxFrequencyMHz + OffsetMHz,
        2 => RxFrequencyMHz - OffsetMHz,
        _ => RxFrequencyMHz
    };

    public string RepeaterSlotText
    {
        get => RepeaterSlot2 ? "2" : "1";
        set => RepeaterSlot2 = value.Trim() == "2";
    }

    public string ColorCodeText
    {
        get => ColorCode.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (byte.TryParse(value, out var v) && v <= 15)
            {
                ColorCode = v;
            }
            else
            {
                OnPropertyChanged(nameof(ColorCodeText));
            }
        }
    }

    public string TxColorCodeText
    {
        get => TxColorCode.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (byte.TryParse(value, out var v) && v <= 15)
            {
                TxColorCode = v;
            }
            else
            {
                OnPropertyChanged(nameof(TxColorCodeText));
            }
        }
    }

    // Confirmed 2026-07-19 by a live differential test (real vendor CPS,
    // two distinct values: entering 1000 produced raw 100, entering 10
    // produced raw 1) - the wire byte is in units of 10 Hz, not 1 Hz.
    // Range corrected 2026-07-31 to the real vendor CPS limit (0-1250 Hz),
    // not the field's full byte*10 capacity (0-2550) - confirmed via
    // direct vendor CPS comparison. Converted 2026-08-09 from reject-and-
    // revert to real validation (same bug shape as CustomCtcssText below,
    // flagged by the app-wide numeric-field audit) - floor is 0 here so it
    // never actually hit the "impossible to type a high floor" failure, but
    // it was still the wrong pattern for a write-enabled field.
    [CustomValidation(typeof(ChannelEntry), nameof(ValidateCorrectFrequencyHzText))]
    public string CorrectFrequencyHzText
    {
        get => (CorrectFrequencyHz * 10).ToString(CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(CorrectFrequencyHzText));
            if (int.TryParse(value, out var hz) && hz >= 0 && hz <= 1250 && hz % 10 == 0)
            {
                CorrectFrequencyHz = (byte)(hz / 10);
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    public static ValidationResult? ValidateCorrectFrequencyHzText(string? value, ValidationContext context)
    {
        if (!int.TryParse(value, out var hz))
        {
            return new ValidationResult("Enter a whole number of Hz.", [context.MemberName!]);
        }

        if (hz % 10 != 0)
        {
            return new ValidationResult("Must be a multiple of 10 Hz.", [context.MemberName!]);
        }

        return hz is >= 0 and <= 1250
            ? ValidationResult.Success
            : new ValidationResult("Must be 0-1250 Hz.", [context.MemberName!]);
    }

    // Confirmed 2026-07-19 by a live differential test (real vendor CPS,
    // "Custom CTCSS" tone entry of 74.6 Hz produced raw 746) - the wire
    // ushort is in tenths of a Hz, matching standard CTCSS tone precision.
    // Range corrected 2026-08-02 to the real vendor CPS limit (50-260 Hz),
    // not the field's full ushort/10 capacity - confirmed directly.
    // Deliberately NOT reject-and-revert - same "multi-digit floor is
    // unreachable by typing" bug shape as RxFrequencyMHzText above.
    private const double MinCustomCtcssHz = 50.0;
    private const double MaxCustomCtcssHz = 260.0;

    [CustomValidation(typeof(ChannelEntry), nameof(ValidateCustomCtcssText))]
    public string CustomCtcssText
    {
        get => (CustomCtcss / 10.0).ToString("0.0", CultureInfo.InvariantCulture);
        set
        {
            ValidateProperty(value, nameof(CustomCtcssText));
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var hz)
                && hz is >= MinCustomCtcssHz and <= MaxCustomCtcssHz)
            {
                CustomCtcss = (ushort)Math.Round(hz * 10);
            }

            OnPropertyChanged(nameof(HasErrors));
        }
    }

    public static ValidationResult? ValidateCustomCtcssText(string? value, ValidationContext context)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var hz))
        {
            return new ValidationResult("Enter a decimal frequency in Hz.", [context.MemberName!]);
        }

        return hz is >= MinCustomCtcssHz and <= MaxCustomCtcssHz
            ? ValidationResult.Success
            : new ValidationResult($"Must be {MinCustomCtcssHz:0.0}-{MaxCustomCtcssHz:0.0} Hz.", [context.MemberName!]);
    }

    public string DtmfIdSelection
    {
        get => $"M{DtmfIdIndex + 1}";
        set
        {
            if (value.Length > 1 && value[0] == 'M' && byte.TryParse(value.AsSpan(1), out var n) && n is >= 1 and <= 16)
            {
                DtmfIdIndex = (byte)(n - 1);
            }
        }
    }

    public string Tone2IdSelection
    {
        get => (Tone2IdIndex + 1).ToString(CultureInfo.InvariantCulture);
        set
        {
            if (byte.TryParse(value, out var n) && n is >= 1 and <= 16)
            {
                Tone2IdIndex = (byte)(n - 1);
            }
        }
    }

    public string Tone5IdSelection
    {
        get => (Tone5IdIndex + 1).ToString(CultureInfo.InvariantCulture);
        set
        {
            if (byte.TryParse(value, out var n) && n is >= 1 and <= 16)
            {
                Tone5IdIndex = (byte)(n - 1);
            }
        }
    }

    public string Tone2DecodeSelection
    {
        get => (Tone2Decode + 1).ToString(CultureInfo.InvariantCulture);
        set
        {
            if (byte.TryParse(value, out var n) && n is >= 1 and <= 16)
            {
                Tone2Decode = (byte)(n - 1);
            }
        }
    }

    // The "1"/"2" presets are raw 0/1. "Customize" is confirmed write-safe
    // via a live differential test 2026-08-02 (real vendor CPS write
    // against channel AV00, 5Tone Bot set to "Customize" - byte 0x40 went
    // to 0x64 (100 decimal), a sentinel value rather than the next
    // sequential index, uniquely isolated against every other populated
    // channel in the capture). No extra custom-value field is exposed for
    // it in the real vendor CPS UI either - it's a single sentinel byte,
    // not a value+parameter pair like Scramble Set/Custom Scrambler.
    private const byte R5ToneCustomizeRaw = 100;

    public string R5ToneBotSelection
    {
        get => R5ToneBot == R5ToneCustomizeRaw ? "Customize" : (R5ToneBot + 1).ToString(CultureInfo.InvariantCulture);
        set
        {
            if (value == "Customize")
            {
                R5ToneBot = R5ToneCustomizeRaw;
            }
            else if (byte.TryParse(value, out var n) && n is >= 1 and <= 2)
            {
                R5ToneBot = (byte)(n - 1);
            }
        }
    }

    public string R5ToneEotSelection
    {
        get => R5ToneEot == R5ToneCustomizeRaw ? "Customize" : (R5ToneEot + 1).ToString(CultureInfo.InvariantCulture);
        set
        {
            if (value == "Customize")
            {
                R5ToneEot = R5ToneCustomizeRaw;
            }
            else if (byte.TryParse(value, out var n) && n is >= 1 and <= 2)
            {
                R5ToneEot = (byte)(n - 1);
            }
        }
    }

    public string QdcIdSelection
    {
        get => (QdcIdIndex + 1).ToString(CultureInfo.InvariantCulture);
        set
        {
            if (byte.TryParse(value, out var n) && n is >= 1 and <= 16)
            {
                QdcIdIndex = (byte)(n - 1);
            }
        }
    }

    public string ExtendEncryptionSelection
    {
        get => ExtendEncryption ? "ARC4" : "AES";
        set => ExtendEncryption = value == "ARC4";
    }

    // "High priority" is a real vendor CPS item but its encoding is
    // unconfirmed (the write attempt failed) - it's listed in
    // MainViewModel.TxInterruptOptions for visibility but blocked from
    // being selected in the view, so this setter should never actually
    // see "High priority" in practice. Falls through to Off if it ever
    // does, rather than guessing at a raw value.
    public string TxInterruptSelection
    {
        get => TxInterrupt ? "Low priority" : "Off";
        set => TxInterrupt = value == "Low priority";
    }

    public string DmrModeSelection
    {
        get => ChannelCodec.DmrModeSelectionToString(DmrModeDcdm, DmrMode);
        set
        {
            if (ChannelCodec.ParseDmrModeSelection(value) is not { } parsed)
            {
                return;
            }

            DmrModeDcdm = parsed.DmrModeDcdm;
            if (parsed.DmrMode is { } dmrMode)
            {
                DmrMode = dmrMode;
            }
        }
    }

    public string AesDigitalEncryptionText
    {
        get => AesEncryptionIndex == 0 ? "Off" : AesEncryptionIndex.ToString(CultureInfo.InvariantCulture);
        set => AesEncryptionIndex = ParseKeyIndexOrOff(value);
    }

    public string Arc4EncryptionText
    {
        get => Arc4EncryptionKeyIndex == 0 ? "Off" : Arc4EncryptionKeyIndex.ToString(CultureInfo.InvariantCulture);
        set => Arc4EncryptionKeyIndex = ParseKeyIndexOrOff(value);
    }

    public string DigitalEncryptionText
    {
        get => DigitalEncryptionIndex == 0 ? "Off" : DigitalEncryptionIndex.ToString(CultureInfo.InvariantCulture);
        set => DigitalEncryptionIndex = ParseKeyIndexOrOff(value);
    }

    private static byte ParseKeyIndexOrOff(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Equals("Off", StringComparison.OrdinalIgnoreCase) || !byte.TryParse(value, out var v)
            ? (byte)0
            : v;

    public string ScrambleModeSelection
    {
        get => ScrambleModeLabels[Math.Clamp(ScrambleMode, 0, 15)];
        set
        {
            var mode = ScrambleModeLabels.ToList().IndexOf(value);
            if (mode is >= 0 and <= 15)
            {
                ScrambleMode = mode;
            }
        }
    }

    public string CustomScramblerSelection
    {
        get => CustomScramblerLabels[Math.Clamp(CustomScrambleFrequencyIndex, 0, 28)];
        set
        {
            var index = CustomScramblerLabels.ToList().IndexOf(value);
            if (index is >= 0 and <= 28)
            {
                CustomScrambleFrequencyIndex = index;
            }
        }
    }

    // Confirmed 2026-07-17 via a live differential test: Busy-Lock/TX-Permit
    // shares ONE raw value space for both digital and analog channels
    // (Channel Free/Different Color Code/Same Color Code, plus raw 0 for
    // "no restriction"), not two separate lists. 2026-07-18: raw 0 is
    // labelled "Off" for analog and "Always" for digital in the real
    // vendor CPS.
    public string DefaultBusyLockTxPermit => IsDigital ? "Always" : "Off";
    public string DisplayLabel => $"{Number:000}  {Name}";
    public string FrequencyLabel => $"{RxFrequencyMHzText} / {TransmitFrequencyMHzText}";
    public string TypeBadge => IsDigital ? "DMR" : "FM";
    public string InfoBadge => GetInfoBadge();
    public bool HasInfoBadge => !string.IsNullOrWhiteSpace(InfoBadge);
    public string InfoBadgeToolTip => GetInfoBadgeToolTip();
    public bool IsDirty => _cleanSnapshot is null || CreateSnapshot() != _cleanSnapshot;
    public bool IsNumberDirty => _cleanSnapshot is null || Number != _cleanSnapshot.Number;
    public bool IsNameDirty => _cleanSnapshot is null || Name != _cleanSnapshot.Name;
    public bool IsReceiveFrequencyDirty => _cleanSnapshot is null || RxFrequencyMHz != _cleanSnapshot.RxFrequencyMHz;
    public bool IsTransmitFrequencyDirty => _cleanSnapshot is null || OffsetMHz != _cleanSnapshot.OffsetMHz || OffsetDirection != _cleanSnapshot.OffsetDirection;
    public bool IsChannelTypeDirty => _cleanSnapshot is null || ChannelType != _cleanSnapshot.ChannelType;
    public bool IsTransmitPowerDirty => _cleanSnapshot is null || TransmitPower != _cleanSnapshot.TransmitPower;
    public bool IsBandwidthDirty => _cleanSnapshot is null || Bandwidth != _cleanSnapshot.Bandwidth;
    public bool IsCtcssDecodeDirty => _cleanSnapshot is null || CtcssDcsDecode != _cleanSnapshot.CtcssDcsDecode;
    public bool IsCtcssEncodeDirty => _cleanSnapshot is null || CtcssDcsEncode != _cleanSnapshot.CtcssDcsEncode;
    public bool IsColorCodeDirty => _cleanSnapshot is null || ColorCode != _cleanSnapshot.ColorCode;
    public bool IsTxColorCodeDirty => _cleanSnapshot is null || TxColorCode != _cleanSnapshot.TxColorCode;
    public bool IsRepeaterSlotDirty => _cleanSnapshot is null || RepeaterSlot2 != _cleanSnapshot.RepeaterSlot2;
    public bool IsContactDirty => _cleanSnapshot is null || ContactIndex != _cleanSnapshot.ContactIndex;
    public bool IsDigitalEncryptionDirty => _cleanSnapshot is null || DigitalEncryptionIndex != _cleanSnapshot.DigitalEncryptionIndex;
    public bool IsArc4EncryptionDirty => _cleanSnapshot is null || Arc4EncryptionKeyIndex != _cleanSnapshot.Arc4EncryptionKeyIndex;
    public bool IsRadioIdDirty => _cleanSnapshot is null || RadioIdIndex != _cleanSnapshot.RadioIdIndex;
    public bool IsBusyLockTxPermitDirty => _cleanSnapshot is null || BusyLock != _cleanSnapshot.BusyLock;
    public bool IsSquelchModeDirty => _cleanSnapshot is null || SquelchMode != _cleanSnapshot.SquelchMode;
    public bool IsOptionalSignalDirty => _cleanSnapshot is null || OptionalSignal != _cleanSnapshot.OptionalSignal;
    public bool IsPttIdDirty => _cleanSnapshot is null || PttId != _cleanSnapshot.PttId;
    public bool IsReceiveGroupListDirty => _cleanSnapshot is null || ReceiveGroupListIndex != _cleanSnapshot.ReceiveGroupListIndex;
    public bool IsPttProhibitDirty => _cleanSnapshot is null || PttProhibit != _cleanSnapshot.PttProhibit;
    public bool IsReverseDirty => _cleanSnapshot is null || Reverse != _cleanSnapshot.Reverse;
    public bool IsSlotSuitDirty => _cleanSnapshot is null || SlotSuit != _cleanSnapshot.SlotSuit;
    public bool IsAesDigitalEncryptionDirty => _cleanSnapshot is null || AesEncryptionIndex != _cleanSnapshot.AesEncryptionIndex;
    public bool IsCallConfirmationDirty => _cleanSnapshot is null || CallConfirmation != _cleanSnapshot.CallConfirmation;
    public bool IsTalkAroundDirty => _cleanSnapshot is null || TalkAround != _cleanSnapshot.TalkAround;
    public bool IsWorkAloneDirty => _cleanSnapshot is null || WorkAlone != _cleanSnapshot.WorkAlone;
    public bool IsCustomCtcssDirty => _cleanSnapshot is null || CustomCtcss != _cleanSnapshot.CustomCtcss;
    public bool IsCtcssEncodeToneDirty => _cleanSnapshot is null || CtcssEncodeTone != _cleanSnapshot.CtcssEncodeTone;
    public bool IsCtcssDecodeToneDirty => _cleanSnapshot is null || CtcssDecodeTone != _cleanSnapshot.CtcssDecodeTone;
    public bool IsDcsEncodeToneDirty => _cleanSnapshot is null || DcsEncodeTone != _cleanSnapshot.DcsEncodeTone;
    public bool IsDcsDecodeToneDirty => _cleanSnapshot is null || DcsDecodeTone != _cleanSnapshot.DcsDecodeTone;
    public bool IsAutoScanDirty => _cleanSnapshot is null || AutoScan != _cleanSnapshot.AutoScan;
    public bool IsSmsConfirmationDirty => _cleanSnapshot is null || SmsConfirmation != _cleanSnapshot.SmsConfirmation;
    public bool IsCorrectFrequencyHzDirty => _cleanSnapshot is null || CorrectFrequencyHz != _cleanSnapshot.CorrectFrequencyHz;
    public bool IsDmrModeDcdmDirty => _cleanSnapshot is null || DmrModeDcdm != _cleanSnapshot.DmrModeDcdm;
    public bool IsDmrModeDirty => _cleanSnapshot is null || DmrMode != _cleanSnapshot.DmrMode;
    public bool IsScrambleDirty => _cleanSnapshot is null || ScrambleMode != _cleanSnapshot.ScrambleMode;
    public bool IsScrambleFrequencyDirty => _cleanSnapshot is null || CustomScrambleFrequencyIndex != _cleanSnapshot.CustomScrambleFrequencyIndex;
    public bool IsDmrCrcIgnoreDirty => _cleanSnapshot is null || DmrCrcIgnore != _cleanSnapshot.DmrCrcIgnore;
    public bool IsSendTalkerAliasDirty => _cleanSnapshot is null || SendTalkerAlias != _cleanSnapshot.SendTalkerAlias;
    public bool IsSmsForbidDirty => _cleanSnapshot is null || SmsForbid != _cleanSnapshot.SmsForbid;
    public bool IsDataAckDisableDirty => _cleanSnapshot is null || DataAckDisable != _cleanSnapshot.DataAckDisable;
    public bool IsExcludeChannelRoamingDirty => _cleanSnapshot is null || ExcludeChannelRoaming != _cleanSnapshot.ExcludeChannelRoaming;
    public bool IsAesRandomKeyDirty => _cleanSnapshot is null || AesRandomKey != _cleanSnapshot.AesRandomKey;
    public bool IsAesMultipleKeyDirty => _cleanSnapshot is null || AesMultipleKey != _cleanSnapshot.AesMultipleKey;
    public bool IsAprsRxDirty => _cleanSnapshot is null || AprsRx != _cleanSnapshot.AprsRx;
    public bool IsDtmfIdIndexDirty => _cleanSnapshot is null || DtmfIdIndex != _cleanSnapshot.DtmfIdIndex;
    public bool IsTone2IdIndexDirty => _cleanSnapshot is null || Tone2IdIndex != _cleanSnapshot.Tone2IdIndex;
    public bool IsTone5IdIndexDirty => _cleanSnapshot is null || Tone5IdIndex != _cleanSnapshot.Tone5IdIndex;
    public bool IsTone2DecodeDirty => _cleanSnapshot is null || Tone2Decode != _cleanSnapshot.Tone2Decode;
    public bool IsR5ToneBotDirty => _cleanSnapshot is null || R5ToneBot != _cleanSnapshot.R5ToneBot;
    public bool IsR5ToneEotDirty => _cleanSnapshot is null || R5ToneEot != _cleanSnapshot.R5ToneEot;
    public bool IsQdcIdIndexDirty => _cleanSnapshot is null || QdcIdIndex != _cleanSnapshot.QdcIdIndex;
    public bool IsExtendEncryptionDirty => _cleanSnapshot is null || ExtendEncryption != _cleanSnapshot.ExtendEncryption;
    public bool IsTxInterruptDirty => _cleanSnapshot is null || TxInterrupt != _cleanSnapshot.TxInterrupt;
    public bool IsIdleTxDirty => _cleanSnapshot is null || IdleTx != _cleanSnapshot.IdleTx;
    public bool IsRangingDirty => _cleanSnapshot is null || Ranging != _cleanSnapshot.Ranging;

    // --- Radio-write dirty tracking (independent of the file-save tracking
    // above) - only the 7 fields ChannelCodec.Encode currently exposes as
    // write-safe. See _radioSyncSnapshot's doc comment.
    public bool IsNamePendingRadioWrite => _radioSyncSnapshot is null || Name != _radioSyncSnapshot.Name;
    public bool IsReceiveFrequencyPendingRadioWrite => _radioSyncSnapshot is null || RxFrequencyMHz != _radioSyncSnapshot.RxFrequencyMHz;
    public bool IsTransmitFrequencyPendingRadioWrite => _radioSyncSnapshot is null || OffsetMHz != _radioSyncSnapshot.OffsetMHz || OffsetDirection != _radioSyncSnapshot.OffsetDirection;
    public bool IsCtcssDecodePendingRadioWrite => _radioSyncSnapshot is null || CtcssDcsDecode != _radioSyncSnapshot.CtcssDcsDecode;
    public bool IsCtcssEncodePendingRadioWrite => _radioSyncSnapshot is null || CtcssDcsEncode != _radioSyncSnapshot.CtcssDcsEncode;
    public bool IsSquelchModePendingRadioWrite => _radioSyncSnapshot is null || SquelchMode != _radioSyncSnapshot.SquelchMode;
    public bool IsOptionalSignalPendingRadioWrite => _radioSyncSnapshot is null || OptionalSignal != _radioSyncSnapshot.OptionalSignal;
    public bool IsBusyLockTxPermitPendingRadioWrite => _radioSyncSnapshot is null || BusyLock != _radioSyncSnapshot.BusyLock;

    // Confirmed write-safe via a live differential test 2026-07-19.
    public bool IsContactPendingRadioWrite => _radioSyncSnapshot is null || ContactIndex != _radioSyncSnapshot.ContactIndex;
    public bool IsRadioIdPendingRadioWrite => _radioSyncSnapshot is null || RadioIdIndex != _radioSyncSnapshot.RadioIdIndex;
    public bool IsReceiveGroupListPendingRadioWrite => _radioSyncSnapshot is null || ReceiveGroupListIndex != _radioSyncSnapshot.ReceiveGroupListIndex;
    public bool IsPttIdPendingRadioWrite => _radioSyncSnapshot is null || PttId != _radioSyncSnapshot.PttId;

    // Round 1, confirmed write-safe via a live differential test 2026-07-19.
    public bool IsChannelTypePendingRadioWrite => _radioSyncSnapshot is null || ChannelType != _radioSyncSnapshot.ChannelType;
    public bool IsTransmitPowerPendingRadioWrite => _radioSyncSnapshot is null || TransmitPower != _radioSyncSnapshot.TransmitPower;
    public bool IsBandwidthPendingRadioWrite => _radioSyncSnapshot is null || Bandwidth != _radioSyncSnapshot.Bandwidth;

    // Round 2, confirmed write-safe via a live differential test 2026-07-19.
    public bool IsTalkAroundPendingRadioWrite => _radioSyncSnapshot is null || TalkAround != _radioSyncSnapshot.TalkAround;
    public bool IsCallConfirmationPendingRadioWrite => _radioSyncSnapshot is null || CallConfirmation != _radioSyncSnapshot.CallConfirmation;
    public bool IsPttProhibitPendingRadioWrite => _radioSyncSnapshot is null || PttProhibit != _radioSyncSnapshot.PttProhibit;
    public bool IsReversePendingRadioWrite => _radioSyncSnapshot is null || Reverse != _radioSyncSnapshot.Reverse;

    // Round 5, confirmed write-safe via a live differential test 2026-07-19.
    public bool IsColorCodePendingRadioWrite => _radioSyncSnapshot is null || ColorCode != _radioSyncSnapshot.ColorCode;
    public bool IsTxColorCodePendingRadioWrite => _radioSyncSnapshot is null || TxColorCode != _radioSyncSnapshot.TxColorCode;

    // Round 6/7/8/9, confirmed write-safe via a combined live differential
    // test 2026-07-19.
    public bool IsWorkAlonePendingRadioWrite => _radioSyncSnapshot is null || WorkAlone != _radioSyncSnapshot.WorkAlone;
    public bool IsSlotSuitPendingRadioWrite => _radioSyncSnapshot is null || SlotSuit != _radioSyncSnapshot.SlotSuit;
    public bool IsRepeaterSlotPendingRadioWrite => _radioSyncSnapshot is null || RepeaterSlot2 != _radioSyncSnapshot.RepeaterSlot2;
    public bool IsSmsConfirmationPendingRadioWrite => _radioSyncSnapshot is null || SmsConfirmation != _radioSyncSnapshot.SmsConfirmation;
    public bool IsAesEncryptionPendingRadioWrite => _radioSyncSnapshot is null || AesEncryptionIndex != _radioSyncSnapshot.AesEncryptionIndex;
    public bool IsArc4EncryptionPendingRadioWrite => _radioSyncSnapshot is null || Arc4EncryptionKeyIndex != _radioSyncSnapshot.Arc4EncryptionKeyIndex;
    public bool IsAutoScanPendingRadioWrite => _radioSyncSnapshot is null || AutoScan != _radioSyncSnapshot.AutoScan;
    public bool IsScramblePendingRadioWrite => _radioSyncSnapshot is null || ScrambleMode != _radioSyncSnapshot.ScrambleMode;
    public bool IsScrambleFrequencyPendingRadioWrite => _radioSyncSnapshot is null || CustomScrambleFrequencyIndex != _radioSyncSnapshot.CustomScrambleFrequencyIndex;
    public bool IsDmrCrcIgnorePendingRadioWrite => _radioSyncSnapshot is null || DmrCrcIgnore != _radioSyncSnapshot.DmrCrcIgnore;
    public bool IsSendTalkerAliasPendingRadioWrite => _radioSyncSnapshot is null || SendTalkerAlias != _radioSyncSnapshot.SendTalkerAlias;
    public bool IsSmsForbidPendingRadioWrite => _radioSyncSnapshot is null || SmsForbid != _radioSyncSnapshot.SmsForbid;
    public bool IsDataAckDisablePendingRadioWrite => _radioSyncSnapshot is null || DataAckDisable != _radioSyncSnapshot.DataAckDisable;
    public bool IsExcludeChannelRoamingPendingRadioWrite => _radioSyncSnapshot is null || ExcludeChannelRoaming != _radioSyncSnapshot.ExcludeChannelRoaming;
    public bool IsAesRandomKeyPendingRadioWrite => _radioSyncSnapshot is null || AesRandomKey != _radioSyncSnapshot.AesRandomKey;
    public bool IsAesMultipleKeyPendingRadioWrite => _radioSyncSnapshot is null || AesMultipleKey != _radioSyncSnapshot.AesMultipleKey;
    public bool IsAprsRxPendingRadioWrite => _radioSyncSnapshot is null || AprsRx != _radioSyncSnapshot.AprsRx;
    public bool IsDtmfIdIndexPendingRadioWrite => _radioSyncSnapshot is null || DtmfIdIndex != _radioSyncSnapshot.DtmfIdIndex;
    public bool IsTone2IdIndexPendingRadioWrite => _radioSyncSnapshot is null || Tone2IdIndex != _radioSyncSnapshot.Tone2IdIndex;
    public bool IsTone5IdIndexPendingRadioWrite => _radioSyncSnapshot is null || Tone5IdIndex != _radioSyncSnapshot.Tone5IdIndex;
    public bool IsTone2DecodePendingRadioWrite => _radioSyncSnapshot is null || Tone2Decode != _radioSyncSnapshot.Tone2Decode;
    public bool IsR5ToneBotPendingRadioWrite => _radioSyncSnapshot is null || R5ToneBot != _radioSyncSnapshot.R5ToneBot;
    public bool IsR5ToneEotPendingRadioWrite => _radioSyncSnapshot is null || R5ToneEot != _radioSyncSnapshot.R5ToneEot;
    public bool IsQdcIdIndexPendingRadioWrite => _radioSyncSnapshot is null || QdcIdIndex != _radioSyncSnapshot.QdcIdIndex;
    public bool IsExtendEncryptionPendingRadioWrite => _radioSyncSnapshot is null || ExtendEncryption != _radioSyncSnapshot.ExtendEncryption;
    public bool IsTxInterruptPendingRadioWrite => _radioSyncSnapshot is null || TxInterrupt != _radioSyncSnapshot.TxInterrupt;
    public bool IsIdleTxPendingRadioWrite => _radioSyncSnapshot is null || IdleTx != _radioSyncSnapshot.IdleTx;
    public bool IsRangingPendingRadioWrite => _radioSyncSnapshot is null || Ranging != _radioSyncSnapshot.Ranging;

    // Round 7/8/9 follow-ups, confirmed write-safe via a live differential
    // test 2026-07-19 (re-tested after clearing a vendor CPS interlock for
    // Digital Encryption, and after finding the real scale factors for
    // CorrectFrequencyHz - 10 Hz per raw count - and CustomCtcss - tenths
    // of a Hz per raw count).
    public bool IsDigitalEncryptionPendingRadioWrite => _radioSyncSnapshot is null || DigitalEncryptionIndex != _radioSyncSnapshot.DigitalEncryptionIndex;
    public bool IsCorrectFrequencyHzPendingRadioWrite => _radioSyncSnapshot is null || CorrectFrequencyHz != _radioSyncSnapshot.CorrectFrequencyHz;
    public bool IsCustomCtcssPendingRadioWrite => _radioSyncSnapshot is null || CustomCtcss != _radioSyncSnapshot.CustomCtcss;
    public bool IsCtcssEncodeTonePendingRadioWrite => _radioSyncSnapshot is null || CtcssEncodeTone != _radioSyncSnapshot.CtcssEncodeTone;
    public bool IsCtcssDecodeTonePendingRadioWrite => _radioSyncSnapshot is null || CtcssDecodeTone != _radioSyncSnapshot.CtcssDecodeTone;
    public bool IsDcsEncodeTonePendingRadioWrite => _radioSyncSnapshot is null || DcsEncodeTone != _radioSyncSnapshot.DcsEncodeTone;
    public bool IsDcsDecodeTonePendingRadioWrite => _radioSyncSnapshot is null || DcsDecodeTone != _radioSyncSnapshot.DcsDecodeTone;
    // Corrected 2026-07-19: DCDM was previously wrongly marked "confirmed
    // dead" - re-tested after locating the real vendor CPS control
    // ("DMR Mode"), which confirmed a 3-value DCDM-submode mapping (see
    // ChannelCodec.Encode's doc comment).
    public bool IsDmrModeDcdmPendingRadioWrite => _radioSyncSnapshot is null || DmrModeDcdm != _radioSyncSnapshot.DmrModeDcdm;
    public bool IsDmrModePendingRadioWrite => _radioSyncSnapshot is null || DmrMode != _radioSyncSnapshot.DmrMode;

    public bool HasAnyPendingRadioWrite =>
        IsNamePendingRadioWrite
        || IsReceiveFrequencyPendingRadioWrite
        || IsTransmitFrequencyPendingRadioWrite
        || IsCtcssDecodePendingRadioWrite
        || IsCtcssEncodePendingRadioWrite
        || IsSquelchModePendingRadioWrite
        || IsOptionalSignalPendingRadioWrite
        || IsBusyLockTxPermitPendingRadioWrite
        || IsContactPendingRadioWrite
        || IsRadioIdPendingRadioWrite
        || IsReceiveGroupListPendingRadioWrite
        || IsPttIdPendingRadioWrite
        || IsChannelTypePendingRadioWrite
        || IsTransmitPowerPendingRadioWrite
        || IsBandwidthPendingRadioWrite
        || IsTalkAroundPendingRadioWrite
        || IsCallConfirmationPendingRadioWrite
        || IsPttProhibitPendingRadioWrite
        || IsReversePendingRadioWrite
        || IsColorCodePendingRadioWrite
        || IsTxColorCodePendingRadioWrite
        || IsWorkAlonePendingRadioWrite
        || IsSlotSuitPendingRadioWrite
        || IsRepeaterSlotPendingRadioWrite
        || IsSmsConfirmationPendingRadioWrite
        || IsAesEncryptionPendingRadioWrite
        || IsArc4EncryptionPendingRadioWrite
        || IsAutoScanPendingRadioWrite
        || IsScramblePendingRadioWrite
        || IsScrambleFrequencyPendingRadioWrite
        || IsDigitalEncryptionPendingRadioWrite
        || IsCorrectFrequencyHzPendingRadioWrite
        || IsCustomCtcssPendingRadioWrite
        || IsDmrCrcIgnorePendingRadioWrite
        || IsSendTalkerAliasPendingRadioWrite
        || IsSmsForbidPendingRadioWrite
        || IsDataAckDisablePendingRadioWrite
        || IsExcludeChannelRoamingPendingRadioWrite
        || IsAesRandomKeyPendingRadioWrite
        || IsAesMultipleKeyPendingRadioWrite
        || IsDmrModeDcdmPendingRadioWrite
        || IsDmrModePendingRadioWrite
        || IsAprsRxPendingRadioWrite
        || IsDtmfIdIndexPendingRadioWrite
        || IsTone2IdIndexPendingRadioWrite
        || IsTone5IdIndexPendingRadioWrite
        || IsTone2DecodePendingRadioWrite
        || IsR5ToneBotPendingRadioWrite
        || IsR5ToneEotPendingRadioWrite
        || IsQdcIdIndexPendingRadioWrite
        || IsExtendEncryptionPendingRadioWrite
        || IsIdleTxPendingRadioWrite
        || IsRangingPendingRadioWrite
        || IsCtcssEncodeTonePendingRadioWrite
        || IsCtcssDecodeTonePendingRadioWrite
        || IsDcsEncodeTonePendingRadioWrite
        || IsDcsDecodeTonePendingRadioWrite
        || IsTxInterruptPendingRadioWrite;

    public string NumberFontWeight => IsNumberDirty ? "Bold" : "Normal";
    public string NameFontWeight => IsNameDirty ? "Bold" : "Normal";
    public string ReceiveFrequencyFontWeight => IsReceiveFrequencyDirty ? "Bold" : "Normal";
    public string TransmitFrequencyFontWeight => IsTransmitFrequencyDirty ? "Bold" : "Normal";
    public string ChannelTypeFontWeight => IsChannelTypeDirty ? "Bold" : "Normal";
    public string TransmitPowerFontWeight => IsTransmitPowerDirty ? "Bold" : "Normal";
    public string BandwidthFontWeight => IsBandwidthDirty ? "Bold" : "Normal";
    public string CtcssDecodeFontWeight => IsCtcssDecodeDirty ? "Bold" : "Normal";
    public string CtcssEncodeFontWeight => IsCtcssEncodeDirty ? "Bold" : "Normal";
    public string ColorCodeFontWeight => IsColorCodeDirty ? "Bold" : "Normal";
    public string TxColorCodeFontWeight => IsTxColorCodeDirty ? "Bold" : "Normal";
    public string RepeaterSlotFontWeight => IsRepeaterSlotDirty ? "Bold" : "Normal";
    public string ContactFontWeight => IsContactDirty ? "Bold" : "Normal";
    public string DigitalEncryptionFontWeight => IsDigitalEncryptionDirty ? "Bold" : "Normal";
    public string Arc4EncryptionFontWeight => IsArc4EncryptionDirty ? "Bold" : "Normal";
    public string RadioIdFontWeight => IsRadioIdDirty ? "Bold" : "Normal";
    public string BusyLockTxPermitFontWeight => IsBusyLockTxPermitDirty ? "Bold" : "Normal";
    public string SquelchModeFontWeight => IsSquelchModeDirty ? "Bold" : "Normal";
    public string OptionalSignalFontWeight => IsOptionalSignalDirty ? "Bold" : "Normal";
    public string PttIdFontWeight => IsPttIdDirty ? "Bold" : "Normal";
    public string ReceiveGroupListFontWeight => IsReceiveGroupListDirty ? "Bold" : "Normal";
    public string PttProhibitFontWeight => IsPttProhibitDirty ? "Bold" : "Normal";
    public string ReverseFontWeight => IsReverseDirty ? "Bold" : "Normal";
    public string SlotSuitFontWeight => IsSlotSuitDirty ? "Bold" : "Normal";
    public string AesDigitalEncryptionFontWeight => IsAesDigitalEncryptionDirty ? "Bold" : "Normal";
    public string CallConfirmationFontWeight => IsCallConfirmationDirty ? "Bold" : "Normal";
    public string TalkAroundFontWeight => IsTalkAroundDirty ? "Bold" : "Normal";
    public string WorkAloneFontWeight => IsWorkAloneDirty ? "Bold" : "Normal";
    public string CustomCtcssFontWeight => IsCustomCtcssDirty ? "Bold" : "Normal";
    public string EncodeToneFontWeight => (IsCtcssEncodeToneDirty || IsDcsEncodeToneDirty) ? "Bold" : "Normal";
    public string DecodeToneFontWeight => (IsCtcssDecodeToneDirty || IsDcsDecodeToneDirty) ? "Bold" : "Normal";
    public string AutoScanFontWeight => IsAutoScanDirty ? "Bold" : "Normal";
    public string SmsConfirmationFontWeight => IsSmsConfirmationDirty ? "Bold" : "Normal";
    public string CorrectFrequencyHzFontWeight => IsCorrectFrequencyHzDirty ? "Bold" : "Normal";
    // Bound to the combined DmrModeSelection combobox - bold if either
    // underlying raw field (DmrModeDcdm or DmrMode) is dirty.
    public string DmrModeFontWeight => (IsDmrModeDcdmDirty || IsDmrModeDirty) ? "Bold" : "Normal";
    public string ScrambleFontWeight => IsScrambleDirty ? "Bold" : "Normal";
    public string ScrambleFrequencyFontWeight => IsScrambleFrequencyDirty ? "Bold" : "Normal";
    public string DmrCrcIgnoreFontWeight => IsDmrCrcIgnoreDirty ? "Bold" : "Normal";
    public string SendTalkerAliasFontWeight => IsSendTalkerAliasDirty ? "Bold" : "Normal";
    public string SmsForbidFontWeight => IsSmsForbidDirty ? "Bold" : "Normal";
    public string DataAckDisableFontWeight => IsDataAckDisableDirty ? "Bold" : "Normal";
    public string ExcludeChannelRoamingFontWeight => IsExcludeChannelRoamingDirty ? "Bold" : "Normal";
    public string AesRandomKeyFontWeight => IsAesRandomKeyDirty ? "Bold" : "Normal";
    public string AesMultipleKeyFontWeight => IsAesMultipleKeyDirty ? "Bold" : "Normal";
    public string AprsRxFontWeight => IsAprsRxDirty ? "Bold" : "Normal";
    public string DtmfIdIndexFontWeight => IsDtmfIdIndexDirty ? "Bold" : "Normal";
    public string Tone2IdIndexFontWeight => IsTone2IdIndexDirty ? "Bold" : "Normal";
    public string Tone5IdIndexFontWeight => IsTone5IdIndexDirty ? "Bold" : "Normal";
    public string Tone2DecodeFontWeight => IsTone2DecodeDirty ? "Bold" : "Normal";
    public string R5ToneBotFontWeight => IsR5ToneBotDirty ? "Bold" : "Normal";
    public string R5ToneEotFontWeight => IsR5ToneEotDirty ? "Bold" : "Normal";
    public string QdcIdIndexFontWeight => IsQdcIdIndexDirty ? "Bold" : "Normal";
    public string ExtendEncryptionFontWeight => IsExtendEncryptionDirty ? "Bold" : "Normal";
    public string TxInterruptFontWeight => IsTxInterruptDirty ? "Bold" : "Normal";
    public string IdleTxFontWeight => IsIdleTxDirty ? "Bold" : "Normal";
    public string RangingFontWeight => IsRangingDirty ? "Bold" : "Normal";

    public void MarkClean()
    {
        _cleanSnapshot = CreateSnapshot();
        NotifyDirtyProperties();
    }

    /// <summary>Establishes the radio-write baseline - call after a
    /// successful Read From Radio (baseline = what the radio has now) or a
    /// successful Write (baseline = what was just confirmed written).
    /// Deliberately never called by Save - see _radioSyncSnapshot's doc
    /// comment.</summary>
    public void MarkRadioSynced()
    {
        _radioSyncSnapshot = CreateSnapshot();
        NotifyPendingRadioWriteProperties();
    }

    /// <summary>Copies every canonical radio-facing field (the same list
    /// <see cref="CreateSnapshot"/> uses for dirty-checking, so this can't
    /// silently drift out of sync with a newly added field the way
    /// <c>MainViewModel.DuplicateChannel</c>'s old hand-written property
    /// list did - Number/TxColorCode/BusyLock/SquelchMode/PttId and about
    /// 30 other fields were missing from a duplicated channel). Caller is
    /// expected to overwrite Number/Name on the result.</summary>
    public ChannelEntry Clone()
    {
        var snapshot = CreateSnapshot();
        return new ChannelEntry
        {
            Number = snapshot.Number,
            Name = snapshot.Name,
            RxFrequencyMHz = snapshot.RxFrequencyMHz,
            OffsetMHz = snapshot.OffsetMHz,
            OffsetDirection = snapshot.OffsetDirection,
            ChannelType = snapshot.ChannelType,
            TransmitPower = snapshot.TransmitPower,
            Bandwidth = snapshot.Bandwidth,
            CtcssDcsDecode = snapshot.CtcssDcsDecode,
            CtcssDcsEncode = snapshot.CtcssDcsEncode,
            ColorCode = snapshot.ColorCode,
            TxColorCode = snapshot.TxColorCode,
            RepeaterSlot2 = snapshot.RepeaterSlot2,
            ContactIndex = snapshot.ContactIndex,
            RadioIdIndex = snapshot.RadioIdIndex,
            BusyLock = snapshot.BusyLock,
            SquelchMode = snapshot.SquelchMode,
            OptionalSignal = snapshot.OptionalSignal,
            PttId = snapshot.PttId,
            ScanListIndex = snapshot.ScanListIndex,
            ReceiveGroupListIndex = snapshot.ReceiveGroupListIndex,
            PttProhibit = snapshot.PttProhibit,
            Reverse = snapshot.Reverse,
            SlotSuit = snapshot.SlotSuit,
            AesEncryptionIndex = snapshot.AesEncryptionIndex,
            CallConfirmation = snapshot.CallConfirmation,
            TalkAround = snapshot.TalkAround,
            WorkAlone = snapshot.WorkAlone,
            CustomCtcss = snapshot.CustomCtcss,
            CtcssEncodeTone = snapshot.CtcssEncodeTone,
            CtcssDecodeTone = snapshot.CtcssDecodeTone,
            DcsEncodeTone = snapshot.DcsEncodeTone,
            DcsDecodeTone = snapshot.DcsDecodeTone,
            AutoScan = snapshot.AutoScan,
            SmsConfirmation = snapshot.SmsConfirmation,
            CorrectFrequencyHz = snapshot.CorrectFrequencyHz,
            DmrModeDcdm = snapshot.DmrModeDcdm,
            ScrambleMode = snapshot.ScrambleMode,
            CustomScrambleFrequencyIndex = snapshot.CustomScrambleFrequencyIndex,
            Arc4EncryptionKeyIndex = snapshot.Arc4EncryptionKeyIndex,
            DigitalEncryptionIndex = snapshot.DigitalEncryptionIndex,
            DmrCrcIgnore = snapshot.DmrCrcIgnore,
            SendTalkerAlias = snapshot.SendTalkerAlias,
            SmsForbid = snapshot.SmsForbid,
            DataAckDisable = snapshot.DataAckDisable,
            ExcludeChannelRoaming = snapshot.ExcludeChannelRoaming,
            AesRandomKey = snapshot.AesRandomKey,
            AesMultipleKey = snapshot.AesMultipleKey,
            DmrMode = snapshot.DmrMode,
            AprsRx = snapshot.AprsRx,
            DtmfIdIndex = snapshot.DtmfIdIndex,
            Tone2IdIndex = snapshot.Tone2IdIndex,
            Tone5IdIndex = snapshot.Tone5IdIndex,
            Tone2Decode = snapshot.Tone2Decode,
            R5ToneBot = snapshot.R5ToneBot,
            R5ToneEot = snapshot.R5ToneEot,
            QdcIdIndex = snapshot.QdcIdIndex,
            ExtendEncryption = snapshot.ExtendEncryption,
            IdleTx = snapshot.IdleTx,
            Ranging = snapshot.Ranging,
            TxInterrupt = snapshot.TxInterrupt,
            ContactDisplayName = ContactDisplayName,
            RadioIdDisplayName = RadioIdDisplayName,
            ReceiveGroupListDisplayName = ReceiveGroupListDisplayName
        };
    }

    private void NotifyPendingRadioWriteProperties()
    {
        OnPropertyChanged(nameof(IsNamePendingRadioWrite));
        OnPropertyChanged(nameof(IsReceiveFrequencyPendingRadioWrite));
        OnPropertyChanged(nameof(IsTransmitFrequencyPendingRadioWrite));
        OnPropertyChanged(nameof(IsCtcssDecodePendingRadioWrite));
        OnPropertyChanged(nameof(IsCtcssEncodePendingRadioWrite));
        OnPropertyChanged(nameof(IsSquelchModePendingRadioWrite));
        OnPropertyChanged(nameof(IsOptionalSignalPendingRadioWrite));
        OnPropertyChanged(nameof(IsBusyLockTxPermitPendingRadioWrite));
        OnPropertyChanged(nameof(IsContactPendingRadioWrite));
        OnPropertyChanged(nameof(IsRadioIdPendingRadioWrite));
        OnPropertyChanged(nameof(IsReceiveGroupListPendingRadioWrite));
        OnPropertyChanged(nameof(IsPttIdPendingRadioWrite));
        OnPropertyChanged(nameof(IsChannelTypePendingRadioWrite));
        OnPropertyChanged(nameof(IsTransmitPowerPendingRadioWrite));
        OnPropertyChanged(nameof(IsBandwidthPendingRadioWrite));
        OnPropertyChanged(nameof(IsTalkAroundPendingRadioWrite));
        OnPropertyChanged(nameof(IsCallConfirmationPendingRadioWrite));
        OnPropertyChanged(nameof(IsPttProhibitPendingRadioWrite));
        OnPropertyChanged(nameof(IsReversePendingRadioWrite));
        OnPropertyChanged(nameof(IsColorCodePendingRadioWrite));
        OnPropertyChanged(nameof(IsTxColorCodePendingRadioWrite));
        OnPropertyChanged(nameof(IsWorkAlonePendingRadioWrite));
        OnPropertyChanged(nameof(IsSlotSuitPendingRadioWrite));
        OnPropertyChanged(nameof(IsRepeaterSlotPendingRadioWrite));
        OnPropertyChanged(nameof(IsSmsConfirmationPendingRadioWrite));
        OnPropertyChanged(nameof(IsAesEncryptionPendingRadioWrite));
        OnPropertyChanged(nameof(IsArc4EncryptionPendingRadioWrite));
        OnPropertyChanged(nameof(IsAutoScanPendingRadioWrite));
        OnPropertyChanged(nameof(IsScramblePendingRadioWrite));
        OnPropertyChanged(nameof(IsScrambleFrequencyPendingRadioWrite));
        OnPropertyChanged(nameof(IsDigitalEncryptionPendingRadioWrite));
        OnPropertyChanged(nameof(IsCorrectFrequencyHzPendingRadioWrite));
        OnPropertyChanged(nameof(IsCustomCtcssPendingRadioWrite));
        OnPropertyChanged(nameof(IsCtcssEncodeTonePendingRadioWrite));
        OnPropertyChanged(nameof(IsCtcssDecodeTonePendingRadioWrite));
        OnPropertyChanged(nameof(IsDcsEncodeTonePendingRadioWrite));
        OnPropertyChanged(nameof(IsDcsDecodeTonePendingRadioWrite));
        OnPropertyChanged(nameof(IsDmrModeDcdmPendingRadioWrite));
        OnPropertyChanged(nameof(IsDmrModePendingRadioWrite));
        OnPropertyChanged(nameof(IsDmrCrcIgnorePendingRadioWrite));
        OnPropertyChanged(nameof(IsSendTalkerAliasPendingRadioWrite));
        OnPropertyChanged(nameof(IsSmsForbidPendingRadioWrite));
        OnPropertyChanged(nameof(IsDataAckDisablePendingRadioWrite));
        OnPropertyChanged(nameof(IsExcludeChannelRoamingPendingRadioWrite));
        OnPropertyChanged(nameof(IsAesRandomKeyPendingRadioWrite));
        OnPropertyChanged(nameof(IsAesMultipleKeyPendingRadioWrite));
        OnPropertyChanged(nameof(IsAprsRxPendingRadioWrite));
        OnPropertyChanged(nameof(IsDtmfIdIndexPendingRadioWrite));
        OnPropertyChanged(nameof(IsTone2IdIndexPendingRadioWrite));
        OnPropertyChanged(nameof(IsTone5IdIndexPendingRadioWrite));
        OnPropertyChanged(nameof(IsTone2DecodePendingRadioWrite));
        OnPropertyChanged(nameof(IsR5ToneBotPendingRadioWrite));
        OnPropertyChanged(nameof(IsR5ToneEotPendingRadioWrite));
        OnPropertyChanged(nameof(IsQdcIdIndexPendingRadioWrite));
        OnPropertyChanged(nameof(IsExtendEncryptionPendingRadioWrite));
        OnPropertyChanged(nameof(IsTxInterruptPendingRadioWrite));
        OnPropertyChanged(nameof(IsIdleTxPendingRadioWrite));
        OnPropertyChanged(nameof(IsRangingPendingRadioWrite));
        OnPropertyChanged(nameof(HasAnyPendingRadioWrite));
    }

    partial void OnNumberChanged(int value)
    {
        OnPropertyChanged(nameof(DisplayLabel));
        NotifyDirtyProperties();
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayLabel));
        NotifyInfoBadgeChanged();
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnRxFrequencyMHzChanged(double value)
    {
        OnPropertyChanged(nameof(RxFrequencyMHzText));
        OnPropertyChanged(nameof(TransmitFrequencyMHzText));
        OnPropertyChanged(nameof(FrequencyLabel));
        NotifyInfoBadgeChanged();
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();

        // Simplex (OffsetDirection == 0) ignores OffsetMHz when computing TX
        // (see ComputeTransmitFrequencyMHz), which let this go stale and
        // invisible in the UI whenever only RX was edited after a channel's
        // creation - confirmed 2026-08-23 against a real saved project file
        // (11 channels carrying a pre-edit OffsetMHz that no longer matched
        // RX). Keep it mirrored to RX so a future reader that takes
        // OffsetMHz literally instead of checking OffsetDirection first (a
        // write path, a CSV export, a different app reading this same
        // file) doesn't compute a wrong TX frequency from silently stale
        // data.
        if (OffsetDirection == 0)
        {
            OffsetMHz = value;
        }
    }

    partial void OnOffsetMHzChanged(double value)
    {
        OnPropertyChanged(nameof(TransmitFrequencyMHzText));
        OnPropertyChanged(nameof(FrequencyLabel));
        NotifyInfoBadgeChanged();
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnOffsetDirectionChanged(byte value)
    {
        OnPropertyChanged(nameof(OffsetDirectionSelection));
        OnPropertyChanged(nameof(TransmitFrequencyMHzText));
        OnPropertyChanged(nameof(FrequencyLabel));
        NotifyInfoBadgeChanged();
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();

        // Same staleness class as OnRxFrequencyMHzChanged above - switching
        // an existing duplex/split channel to simplex should not leave its
        // old (now-ignored-by-the-UI) OffsetMHz behind.
        if (value == 0)
        {
            OffsetMHz = RxFrequencyMHz;
        }
    }

    partial void OnChannelTypeChanged(byte value)
    {
        OnPropertyChanged(nameof(ChannelTypeSelection));
        OnPropertyChanged(nameof(IsDigital));
        OnPropertyChanged(nameof(IsAnalog));
        OnPropertyChanged(nameof(CanUseEncryption));
        OnPropertyChanged(nameof(UsesDigitalEncryption));
        OnPropertyChanged(nameof(UsesAesEncryption));
        OnPropertyChanged(nameof(UsesArc4Encryption));
        OnPropertyChanged(nameof(EncryptionMode));
        OnPropertyChanged(nameof(TypeBadge));
        OnPropertyChanged(nameof(BusyLockTxPermitSelection));
        OnPropertyChanged(nameof(HasAnalogCapability));
        OnPropertyChanged(nameof(CanUseReverse));
        OnPropertyChanged(nameof(CanUseBandwidth));
        NotifyInfoBadgeChanged();
        if (!IsDigital)
        {
            DigitalEncryptionIndex = 0;
            AesEncryptionIndex = 0;
            Arc4EncryptionKeyIndex = 0;

            // SlotSuit, SmsConfirmation, CallConfirmation and DmrModeDcdm
            // are all confirmed digital-only in the vendor CPS (all four
            // live inside its digitalGroupBox, disabled entirely for
            // A-Analog) - clearing here for the same reason as Bandwidth/
            // Reverse above: a stale value from a prior digital channel
            // type must not silently survive a switch to pure Analog and
            // get written.
            SlotSuit = false;
            SmsConfirmation = false;
            CallConfirmation = false;
            DmrModeDcdm = 0;
        }

        // Matches the vendor CPS's own behavior (channel_edit_dialog.cpp's
        // setModeFormVisibility: ui->bandWidthCmbx->setCurrentIndex(0) for
        // every non-Analog channel type) - Bandwidth is analog-only.
        if (!CanUseBandwidth)
        {
            Bandwidth = 0;
        }

        // Reverse is only meaningful for A-Analog/A+D TX A - matches the
        // vendor CPS disabling (not clearing) the checkbox for the other
        // types, but since our write-safe patch always sends whatever the
        // field currently holds, clearing here prevents a stale True from
        // a prior channel type silently surviving into an unsupported one.
        if (!CanUseReverse)
        {
            Reverse = false;
        }

        NormalizeBusyLockTxPermit();
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnTransmitPowerChanged(byte value)
    {
        OnPropertyChanged(nameof(TransmitPowerSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnBandwidthChanged(byte value)
    {
        OnPropertyChanged(nameof(BandwidthSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnCtcssDcsDecodeChanged(byte value)
    {
        OnPropertyChanged(nameof(CtcssDecodeSelection));
        OnPropertyChanged(nameof(IsDecodeToneVisible));
        OnPropertyChanged(nameof(DecodeToneOptions));
        OnPropertyChanged(nameof(DecodeToneSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnCtcssDcsEncodeChanged(byte value)
    {
        OnPropertyChanged(nameof(CtcssEncodeSelection));
        OnPropertyChanged(nameof(IsEncodeToneVisible));
        OnPropertyChanged(nameof(EncodeToneOptions));
        OnPropertyChanged(nameof(EncodeToneSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnColorCodeChanged(byte value)
    {
        OnPropertyChanged(nameof(ColorCodeText));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnTxColorCodeChanged(byte value)
    {
        OnPropertyChanged(nameof(TxColorCodeText));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnRepeaterSlot2Changed(bool value)
    {
        OnPropertyChanged(nameof(RepeaterSlotText));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnContactIndexChanged(ushort value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnAesEncryptionIndexChanged(byte value)
    {
        OnPropertyChanged(nameof(AesDigitalEncryptionText));
        OnPropertyChanged(nameof(UsesAesEncryption));
        OnPropertyChanged(nameof(EncryptionMode));
        NotifyInfoBadgeChanged();
        // Confirmed against the reference vendor CPS source: a channel only
        // ever has ONE active encryption type (Digital/AES/ARC4 are
        // mutually exclusive, gated by a radio-wide Basic/Extended switch
        // this app doesn't model yet) - selecting one clears the others so
        // this app can't produce an invalid multi-selected state.
        if (value != 0)
        {
            if (Arc4EncryptionKeyIndex != 0)
            {
                Arc4EncryptionKeyIndex = 0;
            }

            if (DigitalEncryptionIndex != 0)
            {
                DigitalEncryptionIndex = 0;
            }
        }

        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnArc4EncryptionKeyIndexChanged(byte value)
    {
        OnPropertyChanged(nameof(Arc4EncryptionText));
        OnPropertyChanged(nameof(UsesArc4Encryption));
        OnPropertyChanged(nameof(EncryptionMode));
        NotifyInfoBadgeChanged();
        if (value != 0)
        {
            if (AesEncryptionIndex != 0)
            {
                AesEncryptionIndex = 0;
            }

            if (DigitalEncryptionIndex != 0)
            {
                DigitalEncryptionIndex = 0;
            }
        }

        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnDigitalEncryptionIndexChanged(byte value)
    {
        OnPropertyChanged(nameof(DigitalEncryptionText));
        OnPropertyChanged(nameof(UsesDigitalEncryption));
        OnPropertyChanged(nameof(EncryptionMode));
        NotifyInfoBadgeChanged();
        if (value != 0)
        {
            if (AesEncryptionIndex != 0)
            {
                AesEncryptionIndex = 0;
            }

            if (Arc4EncryptionKeyIndex != 0)
            {
                Arc4EncryptionKeyIndex = 0;
            }
        }

        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnRadioIdIndexChanged(ushort value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnBusyLockChanged(byte value)
    {
        OnPropertyChanged(nameof(BusyLockTxPermitSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnSquelchModeChanged(byte value)
    {
        OnPropertyChanged(nameof(SquelchModeSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnOptionalSignalChanged(byte value)
    {
        OnPropertyChanged(nameof(OptionalSignalSelection));
        OnPropertyChanged(nameof(IsOptionalSignalDtmf));
        OnPropertyChanged(nameof(IsOptionalSignalTwoTone));
        OnPropertyChanged(nameof(IsOptionalSignalFiveTone));
        OnPropertyChanged(nameof(IsOptionalSignalQdc1200));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnPttIdChanged(byte value)
    {
        OnPropertyChanged(nameof(PttIdSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnScanListIndexChanged(ushort value) => NotifyDirtyProperties();

    partial void OnReceiveGroupListIndexChanged(ushort value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnPttProhibitChanged(bool value)
    {
        NotifyInfoBadgeChanged();
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnReverseChanged(bool value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnSlotSuitChanged(bool value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnCallConfirmationChanged(bool value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnTalkAroundChanged(bool value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnWorkAloneChanged(bool value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnDmrCrcIgnoreChanged(bool value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnSendTalkerAliasChanged(bool value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnSmsForbidChanged(bool value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnDataAckDisableChanged(bool value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnExcludeChannelRoamingChanged(bool value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAesRandomKeyChanged(bool value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAesMultipleKeyChanged(bool value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAprsRxChanged(bool value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnDtmfIdIndexChanged(byte value)
    {
        OnPropertyChanged(nameof(DtmfIdSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnTone2IdIndexChanged(byte value)
    {
        OnPropertyChanged(nameof(Tone2IdSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnTone5IdIndexChanged(byte value)
    {
        OnPropertyChanged(nameof(Tone5IdSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnTone2DecodeChanged(byte value)
    {
        OnPropertyChanged(nameof(Tone2DecodeSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnR5ToneBotChanged(byte value)
    {
        OnPropertyChanged(nameof(R5ToneBotSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnR5ToneEotChanged(byte value)
    {
        OnPropertyChanged(nameof(R5ToneEotSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnQdcIdIndexChanged(byte value)
    {
        OnPropertyChanged(nameof(QdcIdSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnExtendEncryptionChanged(bool value)
    {
        OnPropertyChanged(nameof(ExtendEncryptionSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnIdleTxChanged(bool value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnRangingChanged(bool value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnTxInterruptChanged(bool value)
    {
        OnPropertyChanged(nameof(TxInterruptSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnCustomCtcssChanged(ushort value)
    {
        OnPropertyChanged(nameof(CustomCtcssText));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnCtcssEncodeToneChanged(byte value)
    {
        OnPropertyChanged(nameof(EncodeToneSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnCtcssDecodeToneChanged(byte value)
    {
        OnPropertyChanged(nameof(DecodeToneSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnDcsEncodeToneChanged(ushort value)
    {
        OnPropertyChanged(nameof(EncodeToneSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnDcsDecodeToneChanged(ushort value)
    {
        OnPropertyChanged(nameof(DecodeToneSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnAutoScanChanged(bool value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }
    partial void OnSmsConfirmationChanged(bool value)
    {
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnCorrectFrequencyHzChanged(byte value)
    {
        OnPropertyChanged(nameof(CorrectFrequencyHzText));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnDmrModeDcdmChanged(byte value)
    {
        OnPropertyChanged(nameof(DmrModeSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnDmrModeChanged(bool value)
    {
        OnPropertyChanged(nameof(DmrModeSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnScrambleModeChanged(int value)
    {
        OnPropertyChanged(nameof(IsCustomScramble));
        OnPropertyChanged(nameof(ScrambleModeSelection));
        NotifyInfoBadgeChanged();
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    partial void OnCustomScrambleFrequencyIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CustomScramblerSelection));
        NotifyDirtyProperties();
        NotifyPendingRadioWriteProperties();
    }

    private void NotifyDirtyProperties()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsNumberDirty));
        OnPropertyChanged(nameof(IsNameDirty));
        OnPropertyChanged(nameof(IsReceiveFrequencyDirty));
        OnPropertyChanged(nameof(IsTransmitFrequencyDirty));
        OnPropertyChanged(nameof(IsChannelTypeDirty));
        OnPropertyChanged(nameof(IsTransmitPowerDirty));
        OnPropertyChanged(nameof(IsBandwidthDirty));
        OnPropertyChanged(nameof(IsCtcssDecodeDirty));
        OnPropertyChanged(nameof(IsCtcssEncodeDirty));
        OnPropertyChanged(nameof(IsColorCodeDirty));
        OnPropertyChanged(nameof(IsTxColorCodeDirty));
        OnPropertyChanged(nameof(IsRepeaterSlotDirty));
        OnPropertyChanged(nameof(IsContactDirty));
        OnPropertyChanged(nameof(IsDigitalEncryptionDirty));
        OnPropertyChanged(nameof(IsArc4EncryptionDirty));
        OnPropertyChanged(nameof(IsReceiveGroupListDirty));
        OnPropertyChanged(nameof(IsTalkAroundDirty));
        OnPropertyChanged(nameof(NumberFontWeight));
        OnPropertyChanged(nameof(NameFontWeight));
        OnPropertyChanged(nameof(ReceiveFrequencyFontWeight));
        OnPropertyChanged(nameof(TransmitFrequencyFontWeight));
        OnPropertyChanged(nameof(ChannelTypeFontWeight));
        OnPropertyChanged(nameof(TransmitPowerFontWeight));
        OnPropertyChanged(nameof(BandwidthFontWeight));
        OnPropertyChanged(nameof(CtcssDecodeFontWeight));
        OnPropertyChanged(nameof(CtcssEncodeFontWeight));
        OnPropertyChanged(nameof(ColorCodeFontWeight));
        OnPropertyChanged(nameof(TxColorCodeFontWeight));
        OnPropertyChanged(nameof(RepeaterSlotFontWeight));
        OnPropertyChanged(nameof(ContactFontWeight));
        OnPropertyChanged(nameof(DigitalEncryptionFontWeight));
        OnPropertyChanged(nameof(Arc4EncryptionFontWeight));
        OnPropertyChanged(nameof(RadioIdFontWeight));
        OnPropertyChanged(nameof(BusyLockTxPermitFontWeight));
        OnPropertyChanged(nameof(SquelchModeFontWeight));
        OnPropertyChanged(nameof(OptionalSignalFontWeight));
        OnPropertyChanged(nameof(PttIdFontWeight));
        OnPropertyChanged(nameof(ReceiveGroupListFontWeight));
        OnPropertyChanged(nameof(PttProhibitFontWeight));
        OnPropertyChanged(nameof(ReverseFontWeight));
        OnPropertyChanged(nameof(SlotSuitFontWeight));
        OnPropertyChanged(nameof(AesDigitalEncryptionFontWeight));
        OnPropertyChanged(nameof(CallConfirmationFontWeight));
        OnPropertyChanged(nameof(TalkAroundFontWeight));
        OnPropertyChanged(nameof(WorkAloneFontWeight));
        OnPropertyChanged(nameof(CustomCtcssFontWeight));
        OnPropertyChanged(nameof(EncodeToneFontWeight));
        OnPropertyChanged(nameof(DecodeToneFontWeight));
        OnPropertyChanged(nameof(AutoScanFontWeight));
        OnPropertyChanged(nameof(SmsConfirmationFontWeight));
        OnPropertyChanged(nameof(CorrectFrequencyHzFontWeight));
        OnPropertyChanged(nameof(DmrModeFontWeight));
        OnPropertyChanged(nameof(ScrambleFontWeight));
        OnPropertyChanged(nameof(ScrambleFrequencyFontWeight));
        OnPropertyChanged(nameof(IsCustomScramble));
        OnPropertyChanged(nameof(DmrCrcIgnoreFontWeight));
        OnPropertyChanged(nameof(SendTalkerAliasFontWeight));
        OnPropertyChanged(nameof(SmsForbidFontWeight));
        OnPropertyChanged(nameof(DataAckDisableFontWeight));
        OnPropertyChanged(nameof(ExcludeChannelRoamingFontWeight));
        OnPropertyChanged(nameof(AesRandomKeyFontWeight));
        OnPropertyChanged(nameof(AesMultipleKeyFontWeight));
        OnPropertyChanged(nameof(AprsRxFontWeight));
        OnPropertyChanged(nameof(DtmfIdIndexFontWeight));
        OnPropertyChanged(nameof(Tone2IdIndexFontWeight));
        OnPropertyChanged(nameof(Tone5IdIndexFontWeight));
        OnPropertyChanged(nameof(Tone2DecodeFontWeight));
        OnPropertyChanged(nameof(R5ToneBotFontWeight));
        OnPropertyChanged(nameof(R5ToneEotFontWeight));
        OnPropertyChanged(nameof(QdcIdIndexFontWeight));
        OnPropertyChanged(nameof(ExtendEncryptionFontWeight));
        OnPropertyChanged(nameof(TxInterruptFontWeight));
        OnPropertyChanged(nameof(IdleTxFontWeight));
        OnPropertyChanged(nameof(RangingFontWeight));
    }

    private void NotifyInfoBadgeChanged()
    {
        OnPropertyChanged(nameof(InfoBadge));
        OnPropertyChanged(nameof(HasInfoBadge));
        OnPropertyChanged(nameof(InfoBadgeToolTip));
    }

    // Analog only has 3 valid raw values (0-2); digital has 4 (0-3) - see
    // ChannelCodec.BusyLockToString's 2026-07-31 doc comment for why these
    // are genuinely different mappings, not a shared value space. Reset to
    // the type's own default (raw 0) if a channel type switch leaves the
    // current raw value out of range for the new type - e.g. a digital
    // channel with "Same Color Code" (raw 3) switched to analog, where
    // raw 3 has no meaning at all.
    private void NormalizeBusyLockTxPermit()
    {
        if (!IsDigital && BusyLock > 2)
        {
            BusyLock = 0;
        }
    }

    private ChannelSnapshot CreateSnapshot()
    {
        return new ChannelSnapshot(
            Number,
            Name,
            RxFrequencyMHz,
            OffsetMHz,
            OffsetDirection,
            ChannelType,
            TransmitPower,
            Bandwidth,
            CtcssDcsDecode,
            CtcssDcsEncode,
            ColorCode,
            TxColorCode,
            RepeaterSlot2,
            ContactIndex,
            RadioIdIndex,
            BusyLock,
            SquelchMode,
            OptionalSignal,
            PttId,
            ScanListIndex,
            ReceiveGroupListIndex,
            PttProhibit,
            Reverse,
            SlotSuit,
            AesEncryptionIndex,
            CallConfirmation,
            TalkAround,
            WorkAlone,
            CustomCtcss,
            CtcssEncodeTone,
            CtcssDecodeTone,
            DcsEncodeTone,
            DcsDecodeTone,
            AutoScan,
            SmsConfirmation,
            CorrectFrequencyHz,
            DmrModeDcdm,
            ScrambleMode,
            CustomScrambleFrequencyIndex,
            Arc4EncryptionKeyIndex,
            DigitalEncryptionIndex,
            DmrCrcIgnore,
            SendTalkerAlias,
            SmsForbid,
            DataAckDisable,
            ExcludeChannelRoaming,
            AesRandomKey,
            AesMultipleKey,
            DmrMode,
            AprsRx,
            DtmfIdIndex,
            Tone2IdIndex,
            Tone5IdIndex,
            Tone2Decode,
            R5ToneBot,
            R5ToneEot,
            QdcIdIndex,
            ExtendEncryption,
            IdleTx,
            Ranging,
            TxInterrupt);
    }

    private sealed record ChannelSnapshot(
        int Number,
        string Name,
        double RxFrequencyMHz,
        double OffsetMHz,
        byte OffsetDirection,
        byte ChannelType,
        byte TransmitPower,
        byte Bandwidth,
        byte CtcssDcsDecode,
        byte CtcssDcsEncode,
        byte ColorCode,
        byte TxColorCode,
        bool RepeaterSlot2,
        ushort ContactIndex,
        ushort RadioIdIndex,
        byte BusyLock,
        byte SquelchMode,
        byte OptionalSignal,
        byte PttId,
        ushort ScanListIndex,
        ushort ReceiveGroupListIndex,
        bool PttProhibit,
        bool Reverse,
        bool SlotSuit,
        byte AesEncryptionIndex,
        bool CallConfirmation,
        bool TalkAround,
        bool WorkAlone,
        ushort CustomCtcss,
        byte CtcssEncodeTone,
        byte CtcssDecodeTone,
        ushort DcsEncodeTone,
        ushort DcsDecodeTone,
        bool AutoScan,
        bool SmsConfirmation,
        byte CorrectFrequencyHz,
        byte DmrModeDcdm,
        int ScrambleMode,
        int CustomScrambleFrequencyIndex,
        byte Arc4EncryptionKeyIndex,
        byte DigitalEncryptionIndex,
        bool DmrCrcIgnore,
        bool SendTalkerAlias,
        bool SmsForbid,
        bool DataAckDisable,
        bool ExcludeChannelRoaming,
        bool AesRandomKey,
        bool AesMultipleKey,
        bool DmrMode,
        bool AprsRx,
        byte DtmfIdIndex,
        byte Tone2IdIndex,
        byte Tone5IdIndex,
        byte Tone2Decode,
        byte R5ToneBot,
        byte R5ToneEot,
        byte QdcIdIndex,
        bool ExtendEncryption,
        bool IdleTx,
        bool Ranging,
        bool TxInterrupt);

    private string GetInfoBadge()
    {
        var labels = GetDerivedInfoLabels();
        return labels.Count == 0 ? "" : labels[0];
    }

    private string GetInfoBadgeToolTip()
    {
        var labels = GetDerivedInfoDescriptions();
        return labels.Count == 0
            ? ""
            : string.Join(Environment.NewLine, labels);
    }

    private List<string> GetDerivedInfoLabels()
    {
        var labels = new List<string>();
        if (IsRepeaterChannel())
        {
            labels.Add("RPTR");
        }

        var band = GetFrequencyBand();
        if (!string.IsNullOrWhiteSpace(band))
        {
            labels.Add(band);
        }

        if (Name.Contains("jakt", StringComparison.OrdinalIgnoreCase))
        {
            labels.Add("JAKT");
        }

        if (UsesDigitalEncryption || UsesAesEncryption)
        {
            labels.Add("ENC");
        }
        else if (UsesArc4Encryption)
        {
            labels.Add("ARC4");
        }

        if (ScrambleMode != 0)
        {
            labels.Add("SCRA");
        }

        if (PttProhibit)
        {
            labels.Add("RX");
        }

        return labels;
    }

    private List<string> GetDerivedInfoDescriptions()
    {
        var labels = new List<string>();
        if (IsRepeaterChannel())
        {
            labels.Add("Repeaterkanal");
        }

        var band = GetFrequencyBand();
        if (band == "VHF")
        {
            labels.Add("VHF-band");
        }
        else if (band == "UHF")
        {
            labels.Add("UHF-band");
        }

        if (Name.Contains("jakt", StringComparison.OrdinalIgnoreCase))
        {
            labels.Add("Jaktkanal");
        }

        if (UsesDigitalEncryption || UsesAesEncryption)
        {
            labels.Add("Kryptering aktiv");
        }
        else if (UsesArc4Encryption)
        {
            labels.Add("ARC4 aktivt");
        }

        if (ScrambleMode != 0)
        {
            labels.Add("Analog scrambler aktiv");
        }

        if (PttProhibit)
        {
            labels.Add("Endast mottagning");
        }

        return labels;
    }

    private bool IsRepeaterChannel() => OffsetDirection != 0 && OffsetMHz != 0;

    private string GetFrequencyBand() => RxFrequencyMHz switch
    {
        >= 136 and <= 174 => "VHF",
        >= 400 and <= 520 => "UHF",
        _ => ""
    };

    public static IReadOnlyList<string> ScrambleModeLabels { get; } =
    [
        "Off",
        "3.5K",
        "3.39K",
        "3.3K",
        "3.29K",
        "3.2K",
        "3.1K",
        "3.0K",
        "2.9K",
        "2.8K",
        "2.7K",
        "2.6K",
        "2.5K",
        "4.095K",
        "3.458K",
        "Customize"
    ];

    // Confirmed 2026-07-19 directly against the real vendor CPS UI: 29
    // distinct values, 1.3k-4.1k in 0.1k steps with no gaps - the reference
    // project's own CUSTOM_SCRAMBLER constants list is missing "2.0k"
    // (jumps straight from 1.9k to 2.1k, only 28 entries), which would have
    // silently mislabeled every index from 7 onward had it been trusted.
    public static IReadOnlyList<string> CustomScramblerLabels { get; } =
    [
        "1.3k",
        "1.4k",
        "1.5k",
        "1.6k",
        "1.7k",
        "1.8k",
        "1.9k",
        "2.0k",
        "2.1k",
        "2.2k",
        "2.3k",
        "2.4k",
        "2.5k",
        "2.6k",
        "2.7k",
        "2.8k",
        "2.9k",
        "3.0k",
        "3.1k",
        "3.2k",
        "3.3k",
        "3.4k",
        "3.5k",
        "3.6k",
        "3.7k",
        "3.8k",
        "3.9k",
        "4.0k",
        "4.1k"
    ];

    // Transcribed 2026-08-02 directly from the real vendor CPS's own CTCSS
    // dropdown - 51 tones matching the well-known 50-tone EIA standard
    // list exactly, plus one addition at the front (62.5 Hz, a known real
    // extra tone on some Chinese-made radios including AnyTone, not part
    // of the official EIA table). Byte offsets 0x0a/0x0b, confirmed
    // write-safe via a live differential test - see
    // ChannelCodec.Decode's doc comment. "Custom CTCSS" is a real vendor
    // CPS item but its raw encoding is unconfirmed, so it's listed for
    // parity but blocked from selection in the view (same pattern as TX
    // Interrupt's "High priority").
    public static IReadOnlyList<string> CtcssToneLabels { get; } =
    [
        "62.5", "67.0", "69.3", "71.9", "74.4", "77.0", "79.7", "82.5", "85.4", "88.5",
        "91.5", "94.8", "97.4", "100.0", "103.5", "107.2", "110.9", "114.8", "118.8", "123.0",
        "127.3", "131.8", "136.5", "141.3", "146.2", "151.4", "156.7", "159.8", "162.2", "165.5",
        "167.9", "171.3", "173.8", "177.3", "179.9", "183.5", "186.2", "189.9", "192.8", "196.6",
        "199.5", "203.5", "206.5", "210.7", "218.1", "225.7", "229.1", "233.6", "241.8", "250.3",
        "254.1",
        "Custom CTCSS"
    ];

    // The confirmed 0-50 range of CtcssToneLabels - "Custom CTCSS" (index
    // 51) is excluded since its raw encoding isn't confirmed.
    public const int CtcssToneCount = 51;

    // Generated 2026-08-02 from a direct transcription of the real vendor
    // CPS's own DCS dropdown: every 3-digit octal code 000-777
    // (512 codes - not the well-known 104-code "valid" DCS subset used by
    // most other radios; this vendor CPS lists the full raw code space),
    // each as both Normal and Inverted polarity. Bytes 0x0c-0x0d/0x0e-0x0f,
    // confirmed write-safe via a live differential test - see
    // ChannelCodec.Decode's doc comment: raw value is a plain 0-based
    // index into this exact 1024-entry list (N block first, then I block),
    // e.g. "D023N" -> 19, "D023I" -> 531 (19 + 512).
    public static IReadOnlyList<string> DcsCodeLabels { get; } =
        Enumerable.Range(0, 512).Select(n => $"D{Convert.ToString(n, 8).PadLeft(3, '0')}N")
            .Concat(Enumerable.Range(0, 512).Select(n => $"D{Convert.ToString(n, 8).PadLeft(3, '0')}I"))
            .ToList();
}

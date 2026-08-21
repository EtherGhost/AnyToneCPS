using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Pure decoder for the D890UV's APRS Settings - a single instance, not a
/// list, like Master ID/Talk Alias Settings/Alarm Settings. Byte layout
/// transcribed field-for-field from the MIT-licensed reference project
/// github.com/xbenkozx/anytone-cps (aprs_settings.cpp, decode_D890UV -
/// every field in this class IS populated for D890UV, unlike Alarm
/// Settings' 3 skipped work_mode_* fields, so this is a genuinely complete
/// port, just a large one).
///
/// Text fields are a genuine MIX of two encodings, confirmed via a live
/// hardware read 2026-07-15 (not assumed): callsigns/digipeater path/symbol/
/// icon are narrow ASCII via <see cref="AsciiTextCodec"/> (confirmed by
/// clean decodes matching known-good strings already found in raw capture
/// analysis - e.g. `to_call` decoded to `"APAT51"` and `digipeater_path` to
/// `"WIDE1-1"`, both exact matches to strings independently noted in
/// `Capture_Findings.md` months earlier). <c>SendingText</c> is the one
/// exception - it decodes via the UTF-16LE <see cref="TextFieldCodec"/>
/// instead, confirmed by the live read: decoding it as narrow ASCII
/// produced a telltale space between every character (the signature of
/// UTF-16LE byte pairs split apart), whereas the correct UTF-16LE decode
/// read cleanly as `"APRSCN WIFI 4.30V"`.
///
/// <c>Dcs</c> and <c>Altitude</c> are plain little-endian integers
/// (`Int::fromBytes` in the reference, default little-endian) - NOT the
/// BCD-hex-as-decimal trick used for frequencies and the digital-report
/// talkgroup IDs (`.toHex().toInt()`) elsewhere in this same record. Two
/// different multi-byte encodings coexist in this one struct - ported
/// exactly as the reference does each field, not assumed uniform.
///
/// <c>DigitalReportCallType</c>/<c>DigitalReportSlot</c>: the reference
/// assigns these single bytes directly to a signed `int` without an
/// unsigned cast (`data_1000.at(0x70)` - Qt's `QByteArray::at()` returns a
/// signed `char`), unlike every other single-byte field in this class and
/// codebase, which explicitly cast to `uint8_t` first. Ported here as plain
/// unsigned bytes for consistency with the rest of this codebase - real
/// call-type/slot values are small (0-3ish), so the signed/unsigned
/// distinction is very unlikely to matter, but flagged in case a real
/// high-bit-set byte ever turns up.
/// </summary>
public static class AprsSettingsCodec
{
    public const int MainDataLength = 0x260;
    public const int FixLocationCount = 7; // fix_2..fix_8
    public const int DigitalReportCount = 8;

    /// <summary><paramref name="fixedLocationBeacon"/> comes from OUTSIDE
    /// <paramref name="data"/> entirely - see
    /// <see cref="D890UvMemoryMap.AprsFixedLocationBeaconAddress"/>'s doc
    /// comment. The caller (<c>RadioCodeplugReader</c>) already reads the
    /// shared 0x3500000 block for Optional Settings, so this is a slice of
    /// that same buffer, not an extra USB read.</summary>
    public static DecodedAprsSettings Decode(ReadOnlySpan<byte> data, byte fixedLocationBeacon)
    {
        // Both slices and the split point CONFIRMED correct 2026-08-15 by a
        // live differential write of a 25-character path (21 chars, exactly
        // filling the first slice, + 4-char "ABCD" overflow) - the overflow
        // landed exactly at +0x83 as already assumed here. See
        // Capture_Findings.md.
        var digipeaterPath = AsciiTextCodec.Decode(data.Slice(0x24, 0x15)) + AsciiTextCodec.Decode(data.Slice(0x83, 0x23));

        // Column-array layout (all 7 fixes' lat-degree bytes contiguous, then
        // all 7 lat-minute bytes, etc. - NOT "each fix's 8 bytes together").
        // Lat degrees/minutes/hundredths, Lng degrees/minutes/hundredths, and
        // Ns CONFIRMED correct for the full i=0..6 range (Fix2-Fix8)
        // 2026-08-15: Fix2 (i=0) and Fix3 (i=1) fully confirmed by live
        // differential writes of all 4 fields at once; Fix8 (i=6) initially
        // looked broken when testing all 4 fields together (values appeared
        // in the wrong columns), but a follow-up isolated single-field test
        // (Lat only) matched the i=6 formula exactly - the original result
        // was the vendor CPS's own UI getting confused editing 4
        // simultaneously-blank fields on the Fix8 tab, not an addressing
        // bug. See Capture_Findings.md for both results.
        //
        // Ew is the one CONFIRMED WRONG piece here: its formula
        // (data[0xfe + i]) reads from 0x03501100 onward for i>=2 (Fix4-8),
        // but that address range is never written in ANY capture taken so
        // far (including the very first one) - a genuine gap in this
        // radio's memory between 0x035010f0 and 0x03501200. Only Ew for
        // Fix2/Fix3 (i=0,1, still inside the written region) is confirmed
        // correct.
        //
        // Three independent 2026-08-15 tests all converge on the same
        // result for Fix4-8's E/W: (1) an isolated single-field edit
        // (Fix4 E/W only) - zero bytes changed anywhere in the write;
        // (2) a 4-field edit from a blank state (Fix8) - Lat/Lng/Ns changed,
        // Ew did not; (3) a 4-field edit from an ALREADY-POPULATED state
        // (Fix4) - all 7 other sub-fields changed exactly as predicted
        // (proving a genuine full-record rewrite happened), Ew still did
        // not appear anywhere. This is no longer "address not found yet" -
        // across 3 different experimental designs, the vendor CPS never
        // once transmits an E/W byte for Fix4-8, even during a proven
        // complete rewrite of everything else in the same record. Most
        // likely explanation: the vendor CPS itself doesn't support setting
        // E/W independently for Fix4-8 (perhaps hardcoded, perhaps derived
        // from something else not yet identified). Do not build write
        // support for this field for Fix4-8 - there is nothing to write.
        var fixLocations = new List<DecodedFixLocation>(FixLocationCount);
        for (var i = 0; i < FixLocationCount; i++)
        {
            var lat = DegMinToDecimal(data[0xcd + i], data[0xd4 + i] + data[0xdb + i] / 100.0);
            var lng = DegMinToDecimal(data[0xe9 + i], data[0xf0 + i] + data[0xf7 + i] / 100.0);
            // Ew only has real backing data for i=0,1 (Fix2/Fix3) - data[0xfe+i]
            // for i>=2 falls in the confirmed-unwritten 0x100-0x1ff gap (see
            // this method's own doc comment above). Reading it would just
            // surface whatever stale/undefined byte happens to be there, not
            // a real E/W value, so default to "E" (0) instead - matches what
            // the vendor CPS itself always shows for these slots.
            var ew = i < 2 ? data[0xfe + i] : (byte)0;
            fixLocations.Add(new DecodedFixLocation(i + 2)
            {
                Lat = lat,
                Ns = data[0xe2 + i],
                Lng = lng,
                Ew = ew
            });
        }

        // Channel/CallType/Slot for slot 1 (i=0) CONFIRMED 2026-08-15 by live
        // differential write - see Capture_Findings.md. Channel is a uint16
        // LE sentinel scheme, not a plain 1-based channel index: VFO A =
        // 4000 (0x0fa0), VFO B = 4001 (0x0fa1) confirmed; real channel
        // numbers presumably occupy the range below 4000, "Current Channel"
        // not yet confirmed (likely 0 or another sentinel). CallType/Slot
        // are plain sequential indexes into CallTypeOptions/SlotOptions as
        // already assumed. TalkgroupId already confirmed separately (see the
        // 4-byte-BCD "5057" finding above, from the full backup capture).
        var digitalReports = new List<DecodedDigitalReport>(DigitalReportCount);
        for (var i = 0; i < DigitalReportCount; i++)
        {
            digitalReports.Add(new DecodedDigitalReport(i + 1)
            {
                Channel = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x40 + i * 2, 2)),
                TalkgroupId = BcdDecimalCodec.DecodeAsDecimal(data.Slice(0x50 + i * 4, 4)),
                CallType = data[0x70 + i],
                Slot = data[0x79 + i]
            });
        }

        // CONFIRMED 2026-08-15: the real vendor CPS has no Filters UI at all
        // on this radio (user checked directly) - the 9-checkbox interpretation
        // below is ported from the reference project's likely-different-model
        // layout, not something this app invented. The bytes themselves are
        // real and non-zero on this radio (0x3f/0x00 observed in a live
        // capture - a clean "low 6 bits set" pattern, not blank/erased flash),
        // so this is genuinely populated data, just with an unconfirmed real
        // meaning - could be a hidden/firmware-only feature, or this byte
        // means something else entirely on the D890UV. Disabled in the UI
        // (see AprsSettingsDetailView.axaml/MobileMainView.axaml) per this
        // project's "disable, don't remove" rule - do not trust this bit
        // mapping for any write path without further investigation.
        var filters1 = data[0xa8];
        var filters2 = data[0xa9];

        return new DecodedAprsSettings
        {
            // See the TxFreqNMHz block below - TxFreq1MHz now reads from the
            // real, confirmed address (+0xac), not the dead +0x1 offset.
            TxFreq1MHz = BcdDecimalCodec.DecodeAsDecimal(data.Slice(0xac, 4)) / 100000.0,
            // TxDelay/SendSubtone/Ctcss/AutoTxInterval/TxTone/ManualTxInterval
            // all CONFIRMED correct at these exact offsets 2026-08-15 (live
            // differential write, see Capture_Findings.md).
            //
            // Dcs: 2 bytes at data[0x8..0xa] little-endian - CONFIRMED
            // 2026-08-15 (was wrongly read as a 4-byte int before, which
            // also silently consumed ManualTxInterval's byte). The raw
            // uint16 value IS directly usable as an index into
            // ChannelEntry.DcsCodeLabels/AprsSettingsEntry.DcsOptions with
            // no further transformation - DcsCodeLabels was built with
            // index N (0-511) = Normal code N in octal, index 512+N =
            // Inverted code N, which is EXACTLY the wire's own
            // "octal-as-decimal, +0x200 if Inverted" scheme. Confirmed by
            // the worked example: D023N -> wire 19 -> DcsCodeLabels[19] ==
            // "D023N"; D754I -> wire 1004 -> DcsCodeLabels[1004] == "D754I".
            TxDelay = data[0x5],
            SendSubtone = data[0x6],
            Ctcss = data[0x7],
            Dcs = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0x8, 2)),
            ManualTxInterval = data[0xa],
            AutoTxInterval = data[0xb],
            TxTone = data[0xc],
            // FixedLocationBeacon is NOT part of this struct's data at all -
            // see D890UvMemoryMap.AprsFixedLocationBeaconAddress's doc
            // comment and this method's own signature.
            FixedLocationBeacon = fixedLocationBeacon,

            // All 4 CONFIRMED correct at these exact offsets 2026-08-15 (live
            // differential write, see Capture_Findings.md), including the
            // degree/minute/hundredths-of-minute sub-byte split - matched a
            // real position (34.21217/108.83317) to 5 decimal places and a
            // second position (45.3/120.45) with only the truncation
            // rounding the vendor CPS itself introduces, not a decode error.
            Fix1Lat = DegMinToDecimal(data[0xe], data[0xf] + data[0x10] / 100.0),
            Fix1Ns = data[0x11],
            Fix1Lng = DegMinToDecimal(data[0x12], data[0x13] + data[0x14] / 100.0),
            Fix1Ew = data[0x15],

            // ToCall/ToCallSsid/YourCall/YourCallSsid/DigipeaterPath's first
            // slice/AprsSymbol/MapIcon all CONFIRMED correct at these exact
            // offsets 2026-08-15 by a live differential write (stronger than
            // the earlier 2026-07-15 read-only confirmation) - SSID fields
            // are plain literal bytes 0-15, not list indexes with any
            // transformation. DigipeaterPath's second slice (0x83, 0x23) not
            // independently exercised (the test path fit entirely in the
            // first slice) - see Capture_Findings.md.
            ToCall = AsciiTextCodec.Decode(data.Slice(0x16, 6)),
            ToCallSsid = data[0x1c],
            YourCall = AsciiTextCodec.Decode(data.Slice(0x1d, 6)),
            YourCallSsid = data[0x23],
            DigipeaterPath = digipeaterPath,

            AprsSymbol = AsciiTextCodec.Decode(data.Slice(0x39, 1)),
            MapIcon = AsciiTextCodec.Decode(data.Slice(0x3a, 1)),
            TxPower = data[0x3b],
            PrewaveTime = data[0x3c],

            // RoamingSupport/RepeaterActivationDelay/DisTime/Altitude (below)/
            // AnalogTxMode/PassAll: all CONFIRMED correct at these exact
            // offsets 2026-08-15 (live differential write, see
            // Capture_Findings.md) - unlike Dcs/FixedLocationBeacon/TxFreq2..8
            // above, no fix needed here.
            RoamingSupport = data[0x78],
            RepeaterActivationDelay = data[0x81],
            DisTime = data[0x82],
            Altitude = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(0xa6, 2)),
            AnalogTxMode = data[0xaa],
            PassAll = data[0xab],

            // Full TxFreq mapping CONFIRMED 2026-08-15 (3 separate isolated
            // live differential writes - TX Frequency 1, TX Frequency 8, and
            // the original slot-shift discovery): UI freq N -> offset
            // +0xac+(N-1)*4 for N=1..8. 8 real BCD slots for 8 UI fields -
            // the old +0x1 ("TxFreq1MHz") slot is genuinely dead, see above.
            TxFreq2MHz = BcdDecimalCodec.DecodeAsDecimal(data.Slice(0xb0, 4)) / 100000.0,
            TxFreq3MHz = BcdDecimalCodec.DecodeAsDecimal(data.Slice(0xb4, 4)) / 100000.0,
            TxFreq4MHz = BcdDecimalCodec.DecodeAsDecimal(data.Slice(0xb8, 4)) / 100000.0,
            TxFreq5MHz = BcdDecimalCodec.DecodeAsDecimal(data.Slice(0xbc, 4)) / 100000.0,
            TxFreq6MHz = BcdDecimalCodec.DecodeAsDecimal(data.Slice(0xc0, 4)) / 100000.0,
            TxFreq7MHz = BcdDecimalCodec.DecodeAsDecimal(data.Slice(0xc4, 4)) / 100000.0,
            TxFreq8MHz = BcdDecimalCodec.DecodeAsDecimal(data.Slice(0xc8, 4)) / 100000.0,

            // Confirmed via live hardware read 2026-07-15: unlike the
            // callsign/path/symbol fields above (genuinely narrow ASCII,
            // matching AX.25/APRS wire convention - confirmed by clean
            // decodes with no interleaved garbage), this free-text status
            // field decoded with a telltale space between every character
            // when treated as narrow ASCII - the signature of UTF-16LE bytes
            // (real-char + 0x00 pair) being misread one byte at a time. This
            // field follows the same UTF-16LE convention as every other
            // free-text field on this radio (channel/zone names etc), not
            // the narrow-ASCII convention used by the callsign fields in
            // this same struct - not a reference-project quirk, just this
            // one field genuinely uses UTF-16LE.
            SendingText = TextFieldCodec.DecodeName(data.Slice(0x200, 0x60)),

            FilterPosition = (filters1 & 0x01) != 0,
            FilterMicE = (filters1 & 0x02) != 0,
            FilterObject = (filters1 & 0x04) != 0,
            FilterItem = (filters1 & 0x08) != 0,
            FilterMessage = (filters1 & 0x10) != 0,
            FilterWxReport = (filters1 & 0x20) != 0,
            FilterNmeaReport = (filters1 & 0x40) != 0,
            FilterStatusReport = (filters1 & 0x80) != 0,
            FilterOther = (filters2 & 0x01) != 0,

            FixLocations = fixLocations,
            DigitalReports = digitalReports
        };
    }

    private static double DegMinToDecimal(byte deg, double minutes) => deg + minutes / 60.0;

    /// <summary>Inverse of the deg/minutes split <see cref="DegMinToDecimal"/>
    /// reads - deliberately ROUNDS the final hundredths-of-minute digit
    /// (not truncates). The vendor CPS itself truncates when it does this
    /// same conversion (see the Fix1/Fix2/Fix3 live-test write-ups in
    /// Capture_Findings.md, where a typed 45.30000 read back as 45.29983) -
    /// that's the vendor CPS's own quirk, not something this app's own
    /// encode path needs to reproduce when going straight from a typed
    /// decimal-degrees value to bytes.</summary>
    private static (byte Deg, byte Min, byte Hundredths) EncodeDegMin(double decimalDegrees)
    {
        var deg = (byte)decimalDegrees;
        var minutesFloat = (decimalDegrees - deg) * 60.0;
        var min = (byte)minutesFloat;
        var hundredths = (byte)Math.Round((minutesFloat - min) * 100.0, MidpointRounding.AwayFromZero);
        return (deg, min, hundredths);
    }

    /// <summary>RMW encode for the main 0x260-byte APRS Settings record -
    /// only writes offsets confirmed live-tested 2026-08-15 (see this
    /// file's Decode doc comments and Capture_Findings.md); every other
    /// byte in <paramref name="current"/> (Filters' 2 bytes, the unmapped
    /// 0x100-0x1ff gap, Fix4-8's Ew columns, and anything else not
    /// explicitly written below) is left exactly as read.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> current, DecodedAprsSettings values)
    {
        if (current.Length != MainDataLength)
        {
            throw new ArgumentException($"APRS Settings main data record must be exactly {MainDataLength} bytes.", nameof(current));
        }

        var result = current.ToArray();

        result[0x5] = values.TxDelay;
        result[0x6] = values.SendSubtone;
        result[0x7] = values.Ctcss;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x8, 2), (ushort)values.Dcs);
        result[0xa] = values.ManualTxInterval;
        result[0xb] = values.AutoTxInterval;
        result[0xc] = values.TxTone;
        // FixedLocationBeacon is NOT part of this record - see
        // EncodeFixedLocationBeacon below and D890UvMemoryMap's doc comment.

        var (fix1LatDeg, fix1LatMin, fix1LatHundredths) = EncodeDegMin(values.Fix1Lat);
        result[0xe] = fix1LatDeg;
        result[0xf] = fix1LatMin;
        result[0x10] = fix1LatHundredths;
        result[0x11] = values.Fix1Ns;
        var (fix1LngDeg, fix1LngMin, fix1LngHundredths) = EncodeDegMin(values.Fix1Lng);
        result[0x12] = fix1LngDeg;
        result[0x13] = fix1LngMin;
        result[0x14] = fix1LngHundredths;
        result[0x15] = values.Fix1Ew;

        AsciiTextCodec.Encode(values.ToCall, 6).CopyTo(result.AsSpan(0x16, 6));
        result[0x1c] = values.ToCallSsid;
        AsciiTextCodec.Encode(values.YourCall, 6).CopyTo(result.AsSpan(0x1d, 6));
        result[0x23] = values.YourCallSsid;

        // DigipeaterPath's overflow-slice split CONFIRMED 2026-08-15 (see
        // Capture_Findings.md) - first 21 chars in the primary slice, the
        // remainder (if any) in the second slice at +0x83.
        var digipeaterPath = values.DigipeaterPath;
        var digipeaterPathOverflow = digipeaterPath.Length > 0x15 ? digipeaterPath[0x15..] : "";
        AsciiTextCodec.Encode(digipeaterPath, 0x15).CopyTo(result.AsSpan(0x24, 0x15));
        AsciiTextCodec.Encode(digipeaterPathOverflow, 0x23).CopyTo(result.AsSpan(0x83, 0x23));

        AsciiTextCodec.Encode(values.AprsSymbol, 1).CopyTo(result.AsSpan(0x39, 1));
        AsciiTextCodec.Encode(values.MapIcon, 1).CopyTo(result.AsSpan(0x3a, 1));
        result[0x3b] = values.TxPower;
        result[0x3c] = values.PrewaveTime;

        result[0x78] = values.RoamingSupport;
        result[0x81] = values.RepeaterActivationDelay;
        result[0x82] = values.DisTime;
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0xa6, 2), (ushort)values.Altitude);
        result[0xaa] = values.AnalogTxMode;
        result[0xab] = values.PassAll;

        WriteFreqMHz(result, 0xac, values.TxFreq1MHz);
        WriteFreqMHz(result, 0xb0, values.TxFreq2MHz);
        WriteFreqMHz(result, 0xb4, values.TxFreq3MHz);
        WriteFreqMHz(result, 0xb8, values.TxFreq4MHz);
        WriteFreqMHz(result, 0xbc, values.TxFreq5MHz);
        WriteFreqMHz(result, 0xc0, values.TxFreq6MHz);
        WriteFreqMHz(result, 0xc4, values.TxFreq7MHz);
        WriteFreqMHz(result, 0xc8, values.TxFreq8MHz);

        TextFieldCodec.EncodeName(values.SendingText, 0x60).CopyTo(result.AsSpan(0x200, 0x60));

        // Filters (0xa8/0xa9) deliberately NOT written - meaning unconfirmed,
        // UI disabled, left exactly as read (see Decode's own doc comment).

        // Fix2-8 column-array writes - Lat/Lng/Ns CONFIRMED for the full
        // i=0..6 range (Fix2-Fix8); Ew only has real backing data for i=0,1
        // (Fix2/Fix3, Number 2/3) - see Decode's own doc comment on the Ew
        // gap. Silently skipping Ew for Number > 3 is intentional, not an
        // oversight - there is nothing to write there.
        foreach (var fix in values.FixLocations)
        {
            var i = fix.Number - 2;
            if (i is < 0 or >= FixLocationCount)
            {
                continue;
            }

            var (latDeg, latMin, latHundredths) = EncodeDegMin(fix.Lat);
            result[0xcd + i] = latDeg;
            result[0xd4 + i] = latMin;
            result[0xdb + i] = latHundredths;
            result[0xe2 + i] = fix.Ns;

            var (lngDeg, lngMin, lngHundredths) = EncodeDegMin(fix.Lng);
            result[0xe9 + i] = lngDeg;
            result[0xf0 + i] = lngMin;
            result[0xf7 + i] = lngHundredths;

            if (fix.Number <= 3)
            {
                result[0xfe + i] = fix.Ew;
            }
        }

        // Digital Report Channel/TalkgroupId/CallType/Slot for slot 1
        // CONFIRMED 2026-08-15; slots 2-8 use the same parallel-array stride,
        // independently corroborated for TalkgroupId specifically (the same
        // "00 00 50 57" BCD value repeated at all 8 slots in the full backup
        // capture) - same confidence level as the Fix4-8 Lat/Lng extension
        // above, not independently write-tested for Channel/CallType/Slot on
        // slots 2-8.
        foreach (var report in values.DigitalReports)
        {
            var i = report.Number - 1;
            if (i is < 0 or >= DigitalReportCount)
            {
                continue;
            }

            BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(0x40 + i * 2, 2), (ushort)report.Channel);
            BcdDecimalCodec.EncodeAsDecimal(report.TalkgroupId, 4).CopyTo(result.AsSpan(0x50 + i * 4, 4));
            result[0x70 + i] = report.CallType;
            result[0x79 + i] = report.Slot;
        }

        return result;
    }

    private static void WriteFreqMHz(byte[] result, int offset, double freqMHz) =>
        BcdDecimalCodec.EncodeAsDecimal((long)Math.Round(freqMHz * 100000), 4).CopyTo(result.AsSpan(offset, 4));

    /// <summary>Trivial RMW-independent encode for the single
    /// FixedLocationBeacon byte outside this struct's own region - see
    /// D890UvMemoryMap.AprsFixedLocationBeaconAddress's doc comment. Matches
    /// TalkAliasSettingsCodec.Encode's shape (ignores <c>current</c> since
    /// it's a single fixed-purpose byte, not a record with unmapped
    /// neighbors to preserve).</summary>
    public static byte[] EncodeFixedLocationBeacon(byte value) => [value];

    public sealed record DecodedAprsSettings
    {
        public double TxFreq1MHz { get; init; }
        public byte TxDelay { get; init; }
        public byte SendSubtone { get; init; }
        public byte Ctcss { get; init; }
        public int Dcs { get; init; }
        public byte ManualTxInterval { get; init; }
        public byte AutoTxInterval { get; init; }
        public byte TxTone { get; init; }
        public byte FixedLocationBeacon { get; init; }

        public double Fix1Lat { get; init; }
        public byte Fix1Ns { get; init; }
        public double Fix1Lng { get; init; }
        public byte Fix1Ew { get; init; }

        public string ToCall { get; init; } = "";
        public byte ToCallSsid { get; init; }
        public string YourCall { get; init; } = "";
        public byte YourCallSsid { get; init; }
        public string DigipeaterPath { get; init; } = "";

        public string AprsSymbol { get; init; } = "";
        public string MapIcon { get; init; } = "";
        public byte TxPower { get; init; }
        public byte PrewaveTime { get; init; }

        public byte RoamingSupport { get; init; }
        public byte RepeaterActivationDelay { get; init; }
        public byte DisTime { get; init; }
        public int Altitude { get; init; }
        public byte AnalogTxMode { get; init; }
        public byte PassAll { get; init; }

        public double TxFreq2MHz { get; init; }
        public double TxFreq3MHz { get; init; }
        public double TxFreq4MHz { get; init; }
        public double TxFreq5MHz { get; init; }
        public double TxFreq6MHz { get; init; }
        public double TxFreq7MHz { get; init; }
        public double TxFreq8MHz { get; init; }

        public string SendingText { get; init; } = "";

        public bool FilterPosition { get; init; }
        public bool FilterMicE { get; init; }
        public bool FilterObject { get; init; }
        public bool FilterItem { get; init; }
        public bool FilterMessage { get; init; }
        public bool FilterWxReport { get; init; }
        public bool FilterNmeaReport { get; init; }
        public bool FilterStatusReport { get; init; }
        public bool FilterOther { get; init; }

        public IReadOnlyList<DecodedFixLocation> FixLocations { get; init; } = [];
        public IReadOnlyList<DecodedDigitalReport> DigitalReports { get; init; } = [];
    }

    public sealed record DecodedFixLocation(int Number)
    {
        public double Lat { get; init; }
        public byte Ns { get; init; }
        public double Lng { get; init; }
        public byte Ew { get; init; }
    }

    public sealed record DecodedDigitalReport(int Number)
    {
        public int Channel { get; init; }
        public long TalkgroupId { get; init; }
        public byte CallType { get; init; }
        public byte Slot { get; init; }
    }
}

using System;
using System.Buffers.Binary;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Hot Key &gt; Hot Key - a flat 18-record array, 0x30 (48) bytes each,
/// starting at D890UvMemoryMap.HotKeyData. No bitmap - all 18 records
/// always physically exist, one per HotKeyEntry.KeyNames entry, in order.
/// Byte layout within a record - Mode(0), Menu(1), CallType(2),
/// DigiCallType(3), CallObject(4-7), Content(8) - matches the
/// xbenkozx/anytone-cps reference project's own hotkey.cpp/device.cpp
/// exactly (its per-record SHAPE held up even though its base ADDRESS was
/// wrong for this radio - see D890UvMemoryMap's doc comment).
///
/// Confirmed 2026-08-04 via TWO live differential captures - a plain READ
/// (Mode/Menu, and every field's state on 18 untouched keys) and a WRITE
/// (everything else, changing Hot Key 2/3/4 to real Analog/Digital
/// configurations):
///
/// - Mode/Menu: direct byte-equals-model-index, e.g. Hot Key 1 read back
///   as Mode=1 ("Menu") on the test radio.
/// - CallType: wire 0=Analog, 1=Digital - confirmed directly (Hot Key 2 set
///   to "Analog" in vendor CPS wrote raw 0x00, Hot Key 3 set to "Digital"
///   wrote raw 0x01). There is NO distinct "Off" byte - untouched keys
///   ALSO read raw 0x00 (this codec's first draft wrongly assumed 0xFF was
///   the reference project's Off sentinel and 0/1/2 a direct model-index
///   mapping; neither held up). Since CallObject has its own unambiguous
///   0xFFFFFFFF "unset" sentinel and CallType's raw 0 is indistinguishable
///   between "genuinely Analog" and "never touched", this decode infers
///   "Off" from CallObject being unset rather than from any CallType byte
///   value - matching every untouched key's real observed state (raw
///   CallType 0x00 + raw CallObject 0xFFFFFFFF, displayed as "Off" in
///   vendor CPS) without contradicting Hot Key 2's real Analog+CallObject
///   pairing. Not 100% certain (a genuine dedicated Off byte for CallType
///   was never directly observed either way, since no key was ever written
///   FROM a real value BACK to Off during the capture), but the only
///   reading consistent with all captured data so far.
/// - DigiCallType: wire 0xFF=Off, 3=Hot Text, 5=State Information - matches
///   the reference project's own raw scheme (its 0=Group Call and 4=Call
///   Tip values are its own, not independently confirmed here - Call Tip
///   was never set during the write test). Unlike CallType, DigiCallType
///   is only ever meaningful when CallType=Digital, so its byte value on
///   an Analog/untouched key is inert either way - no cross-field
///   inference needed here.
/// - CallObject: a 0-based index into Analog Quick Call (when CallType=
///   Analog) or Talkgroups (when CallType=Digital), NOT a "Number" -
///   confirmed by Hot Key 2's Call Object (Analog Quick Call No. 1
///   selected) writing raw 0, and Hot Key 3's Call Object (a real
///   Talkgroup selected) also writing raw 0. Translated to this codec's
///   own 1-based "Number" convention (+1) so DecodedHotKey.CallObject
///   lines up with AnalogQuickCallEntry.Number/TalkgroupEntry.Number the
///   same way every other reference field in this app already does.
///   Endianness NOT independently confirmed (every captured value happens
///   to be endian-symmetric: 0 or 0xFFFFFFFF) - kept little-endian,
///   matching AutoRepeaterOffsetCodec's own plain-integer convention.
/// - Content: a 0-based index, NOT a "Number" - into Prefabricated SMS
///   (Hot Text) or, notably, into State Information itself (State
///   Information) - confirmed by Hot Key 3's Content (the real SMS
///   "Welcome!", the 2nd of 5 configured messages, 0-based id 1) writing
///   raw 0x01, and Hot Key 4's Content (State Information's own real "1"
///   entry, which resolves to text "Status Message 1") writing raw 0x00 -
///   i.e. DMR State Information's Content field is a genuine reference
///   into the State Information list, not the literal "1"/"16" values
///   this codec's first draft guessed before finding the
///   dropdown was actually resolving to real State Information text.
///   Same +1 translation as CallObject. Content DOES have an unambiguous
///   0xFF "Off" sentinel (distinct from any real 0-based index, since a
///   byte only reaches 0xFF at 255 slots deep - far past either list's
///   real size), so no cross-field inference is needed here.
/// </summary>
public static class HotKeyCodec
{
    public const int RecordLength = 0x30;
    public const int KeyCount = 18;

    private const byte WireCallTypeAnalog = 0;
    private const byte WireCallTypeDigital = 1;

    private const byte WireDigiCallTypeHotText = 3;
    private const byte WireDigiCallTypeCallTip = 4;
    private const byte WireDigiCallTypeStateInformation = 5;

    public static DecodedHotKey Decode(ReadOnlySpan<byte> data, int index)
    {
        var mode = data[0];
        var menu = data[1];
        var rawCallType = data[2];
        var rawDigiCallType = data[3];
        var rawCallObject = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(4, 4));
        var rawContent = data[8];
        var callObjectSet = rawCallObject != 0xFFFFFFFF;

        return new DecodedHotKey(index)
        {
            Mode = mode,
            Menu = menu,
            CallType = DecodeCallType(rawCallType, callObjectSet),
            DigiCallType = DecodeDigiCallType(rawDigiCallType),
            CallObject = callObjectSet ? (int)rawCallObject + 1 : -1,
            Content = rawContent == 0xFF ? -1 : rawContent + 1
        };
    }

    /// <summary>See this class's own doc comment - "Off" is inferred from
    /// <paramref name="callObjectSet"/> rather than from <paramref name="raw"/>
    /// itself, since raw 0 ("Analog") is indistinguishable from an
    /// untouched/never-configured key at the byte level.</summary>
    private static byte DecodeCallType(byte raw, bool callObjectSet)
    {
        if (!callObjectSet)
        {
            return 0; // Off
        }

        return raw switch
        {
            WireCallTypeDigital => 2,
            _ => 1 // WireCallTypeAnalog (0) and anything unrecognized both fall back to Analog
        };
    }

    private static byte DecodeDigiCallType(byte raw) => raw switch
    {
        WireDigiCallTypeHotText => 1,
        WireDigiCallTypeCallTip => 2,
        WireDigiCallTypeStateInformation => 3,
        _ => 0 // Off (0xFF), the reference's own "Group Call" (0), and anything unrecognized all fall back to "Off"
    };

    /// <summary>Encodes every confirmed field into a copy of <paramref name="currentRecord"/>,
    /// preserving bytes 9 through the record's end (never observed as
    /// anything but 0 in either capture, but not attributed to any known
    /// field - see this class's own doc comment). "Off" CallType has no
    /// dedicated wire value of its own (see this class's doc comment) - it
    /// writes the same raw 0 as "Analog" and relies on CallObject's own
    /// 0xFFFFFFFF sentinel to round-trip back to Off on the next
    /// Decode.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> currentRecord, DecodedHotKey values)
    {
        if (currentRecord.Length != RecordLength)
        {
            throw new ArgumentException($"Hot Key record must be exactly {RecordLength} bytes.", nameof(currentRecord));
        }

        var result = currentRecord.ToArray();
        result[0] = values.Mode;
        result[1] = values.Menu;
        result[2] = EncodeCallType(values.CallType);
        result[3] = EncodeDigiCallType(values.DigiCallType);

        var rawCallObject = values.CallObject < 0 ? 0xFFFFFFFFu : (uint)(values.CallObject - 1);
        BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(4, 4), rawCallObject);

        result[8] = values.Content < 0 ? (byte)0xFF : (byte)(values.Content - 1);

        return result;
    }

    private static byte EncodeCallType(byte callType) => callType switch
    {
        2 => WireCallTypeDigital,
        _ => WireCallTypeAnalog // Off (0) and Analog (1) both write the same raw 0 - see this method's own doc comment
    };

    private static byte EncodeDigiCallType(byte digiCallType) => digiCallType switch
    {
        1 => WireDigiCallTypeHotText,
        2 => WireDigiCallTypeCallTip,
        3 => WireDigiCallTypeStateInformation,
        _ => 0xFF // Off
    };

    public sealed record DecodedHotKey(int Index)
    {
        public byte Mode { get; init; }
        public byte Menu { get; init; }
        public byte CallType { get; init; }
        public byte DigiCallType { get; init; }
        public int CallObject { get; init; } = -1;
        public int Content { get; init; } = -1;
    }
}

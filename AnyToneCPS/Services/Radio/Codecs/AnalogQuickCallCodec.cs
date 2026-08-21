using System;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// Hot Key &gt; Analog Quick Call - a flat 4-slot array, 2 bytes each
/// (Operation Type, Call ID), starting at D890UvMemoryMap.AnalogQuickCallData.
/// No bitmap - all 4 slots always physically exist, matching
/// AutoRepeaterOffsetCodec's own "flat contiguous array, no presence
/// tracking" shape. Real address and byte shape confirmed 2026-08-04 via a
/// live differential READ capture (see D890UvMemoryMap's doc comment) -
/// the reference project's own guessed address was wrong for this radio,
/// but its per-slot byte shape held up.
/// </summary>
public static class AnalogQuickCallCodec
{
    public const int RecordLength = 2;
    public const int SlotCount = 4;

    /// <summary>Confirmed 2026-08-04: all 4 slots on the test radio read
    /// as OperationType=0x00, CallId=0xFF - matching "unconfigured", which
    /// was independently confirmed against the vendor CPS ("no items ...
    /// I don't have any filled"). CallId's real byte-to-model mapping for
    /// a configured 2Tone/5Tone/QDC1200 slot is NOT yet confirmed - only
    /// the 0xFF "Off" sentinel is (see D890UvMemoryMap's doc comment).</summary>
    public static DecodedAnalogQuickCall Decode(ReadOnlySpan<byte> data, int index)
    {
        var operationType = data[0];
        var rawCallId = data[1];

        return new DecodedAnalogQuickCall(index)
        {
            OperationType = operationType,
            CallId = rawCallId == 0xFF ? -1 : rawCallId
        };
    }

    /// <summary>Mirrors <see cref="Decode"/> exactly - CallId's own byte
    /// value for a configured 2Tone/5Tone/QDC1200 slot was never directly
    /// confirmed against a real configured record (see this class's own
    /// doc comment), so Encode keeps the same "raw byte equals the model
    /// value directly" assumption Decode already ships with, rather than
    /// introducing a different unconfirmed guess of its own.</summary>
    public static byte[] Encode(DecodedAnalogQuickCall values)
    {
        return [values.OperationType, values.CallId < 0 ? (byte)0xFF : (byte)values.CallId];
    }

    public sealed record DecodedAnalogQuickCall(int Index)
    {
        public byte OperationType { get; init; }
        public int CallId { get; init; } = -1;
    }
}

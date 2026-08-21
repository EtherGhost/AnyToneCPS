using System;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// QDC 1200 Setting &gt; Decode/Encode tabs' global (singleton) fields - a
/// compact 32-byte record at D890UvMemoryMap.Qdc1200SettingsData. Unlike
/// the ID table (Qdc1200IdCodec), this isn't part of a big array - only
/// this one 32-byte region was ever written in either capture, confirming
/// it's a genuine standalone record (there's only ever one QDC 1200
/// Setting on the radio, same reasoning as AlarmSettingsEntry/
/// AprsSettingsEntry).
///
/// Confirmed 2026-08-04 via TWO live differential WRITE captures (searched
/// blind - no reference project data exists at all for this entity, see
/// Qdc1200SettingsEntry's own class doc comment):
///
/// - Side Tone (byte 0): a plain bool.
/// - Remotely Kill Allow (byte 16) / Remotely Monitor Allow (byte 17):
///   plain bools, confirmed independently (On/Off respectively in the
///   test capture).
/// - Pretime (byte 18): a 0-based index into the confirmed 10-2500ms,
///   10ms-step option list (value = (index+1)*10) - 730ms round-tripped
///   as raw byte 72 exactly.
/// - Auto Reset Time (byte 19): the raw value directly, no offset - 77
///   round-tripped as raw byte 77 exactly.
/// - Self ID Private Call (bytes 20-21) / Self ID Group Call (bytes
///   22-23): same ReverseHexCodec trick as Qdc1200IdCodec's own Private/
///   Group Call ID fields - Private Call keeps its full 4 hex chars,
///   Group Call is padded to 4 with a leading 0 before reversing, then
///   only the last 3 chars are kept on decode (same as
///   AlarmSettingsCodec.QdcGroupId).
/// - Max ACK Wait Time (byte 25): a 0-based index into the confirmed
///   0.5-60.0s, 0.5-step option list (value = (index+1)*0.5) - 12.5s
///   round-tripped as raw byte 24 exactly.
/// - Resend Code (byte 26): a 0-based index into the confirmed 1/2/3
///   option list (value = index+1) - confirmed by a second capture
///   changing it from an unset default to 3, which read back as raw byte
///   2 exactly.
/// - Remote Listening Duration (byte 29): the raw value MINUS 5 (the
///   option list's own floor) - 199s round-tripped as raw byte 194
///   exactly.
///
/// Byte 27 was observed as 0x01 in BOTH captures despite Resend Code
/// changing on the second one - genuinely unconfirmed, not attributed to
/// any known field. Bytes 1-15, 24, 28, 30-31 were always
/// zero in both captures - unconfirmed, assumed unused/padding.
/// </summary>
public static class Qdc1200SettingsCodec
{
    public const int RecordLength = 0x20;

    public static DecodedQdc1200Settings Decode(ReadOnlySpan<byte> data)
    {
        var sideTone = data[0] != 0;
        var remotelyKillAllow = data[16] != 0;
        var remotelyMonitorAllow = data[17] != 0;
        var pretime = (data[18] + 1) * 10;
        var autoResetTime = data[19];

        var selfIdPrivateCall = ReverseHexCodec.Decode(data.Slice(20, 2));
        var selfIdGroupCallHex = ReverseHexCodec.Decode(data.Slice(22, 2));
        var selfIdGroupCall = selfIdGroupCallHex.Length >= 4 ? selfIdGroupCallHex.Substring(1, 3) : selfIdGroupCallHex;

        var maxAckWaitTime = (data[25] + 1) * 0.5;
        var resendCode = (byte)(data[26] + 1);
        var remoteListeningDuration = data[29] + 5;

        return new DecodedQdc1200Settings
        {
            SideTone = sideTone,
            RemotelyKillAllow = remotelyKillAllow,
            RemotelyMonitorAllow = remotelyMonitorAllow,
            Pretime = pretime,
            AutoResetTime = autoResetTime,
            SelfIdPrivateCall = selfIdPrivateCall,
            SelfIdGroupCall = selfIdGroupCall,
            MaxAckWaitTime = maxAckWaitTime,
            ResendCode = resendCode,
            RemoteListeningDuration = remoteListeningDuration
        };
    }

    /// <summary>Encodes every confirmed field into a copy of <paramref name="currentRecord"/>,
    /// leaving byte 27 and the unconfirmed padding bytes untouched - same
    /// "preserve the unknown bytes" discipline as AmAirCodec.Encode/
    /// Qdc1200IdCodec.Encode. An empty Self ID field encodes the same
    /// "0000"/"000" default as Qdc1200IdCodec's own Private/Group Call ID
    /// fields.</summary>
    public static byte[] Encode(ReadOnlySpan<byte> currentRecord, DecodedQdc1200Settings values)
    {
        if (currentRecord.Length != RecordLength)
        {
            throw new ArgumentException($"QDC 1200 Setting record must be exactly {RecordLength} bytes.", nameof(currentRecord));
        }

        var result = currentRecord.ToArray();
        result[0] = (byte)(values.SideTone ? 1 : 0);
        result[16] = (byte)(values.RemotelyKillAllow ? 1 : 0);
        result[17] = (byte)(values.RemotelyMonitorAllow ? 1 : 0);
        result[18] = (byte)(values.Pretime / 10 - 1);
        result[19] = values.AutoResetTime;

        var privateDigits = string.IsNullOrEmpty(values.SelfIdPrivateCall) ? "0000" : values.SelfIdPrivateCall.PadLeft(4, '0');
        ReverseHexCodec.Encode(privateDigits).CopyTo(result, 20);

        var groupDigits = string.IsNullOrEmpty(values.SelfIdGroupCall) ? "000" : values.SelfIdGroupCall.PadLeft(3, '0');
        ReverseHexCodec.Encode("0" + groupDigits).CopyTo(result, 22);

        result[25] = (byte)Math.Round(values.MaxAckWaitTime * 2 - 1);
        result[26] = (byte)(values.ResendCode - 1);
        result[29] = (byte)(values.RemoteListeningDuration - 5);

        return result;
    }

    public sealed record DecodedQdc1200Settings
    {
        public bool SideTone { get; init; }
        public bool RemotelyKillAllow { get; init; }
        public bool RemotelyMonitorAllow { get; init; }
        public int Pretime { get; init; }
        public byte AutoResetTime { get; init; }
        public string SelfIdPrivateCall { get; init; } = "";
        public string SelfIdGroupCall { get; init; } = "";
        public double MaxAckWaitTime { get; init; }
        public byte ResendCode { get; init; }
        public int RemoteListeningDuration { get; init; }
    }
}

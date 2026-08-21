using System;
using System.Text;

namespace AnyToneCPS.Services.Radio.Codecs;

/// <summary>
/// The 5-tone paging "repeat-tone compression" used by Send Message and
/// PTTID's own Encode ID wire format (row-level, BOT, EOT alike) - cracked
/// 2026-08-06 across 3 live differential WRITE captures, using 2
/// byte-confirmed data points ("12345" and "99999" as Other Side ID, both
/// under Send Message) plus 4 older hand-transcribed vendor CPS examples
/// that all fit the SAME rule once re-derived against real bytes.
///
/// Every Calling Type that uses this format starts with a fixed 2-character
/// marker whose own trailing digit becomes the initial reference for
/// compression: "E1" for Send Message, "E6" for PTTID (BOT/EOT only - the
/// row-level table's own PTTID rule is "Encode ID empty", it never reaches
/// this format at all). PTTID's own OtherSideId="12345" looked like plain
/// "E6"+digits with zero compression in the very first capture purely
/// because none of 1/2/3/4/5 happened to equal the reference 6 at the right
/// moment - it's the SAME algorithm as Send Message, just seeded
/// differently, confirmed once a capture used an OtherSideId containing a
/// literal '6' digit and the SAME compression kicked in.
///
/// Algorithm, one output character per input digit: maintain a reference
/// character R, seeded from the marker's own trailing digit. For each
/// digit, if it equals R, emit 'E' and force the NEXT digit to be emitted
/// literally (no comparison) with R updated to it; otherwise emit the digit
/// literally and update R to it. Always finish with exactly one trailing
/// 'E'. If the resulting length (marker + compressed digits + trailing E)
/// is odd, one more 'E' pads it to an even count for nibble-packing (2 hex
/// characters per byte) - confirmed for 5- and 7-digit Other Side IDs
/// (2+N+1 is already even for odd N). The 6-digit case (2+N+1=9, odd, would
/// need this extra pad) is NOT independently byte-confirmed - the only data
/// point is one old hand-transcribed example that's ambiguous about whether
/// the pad is really there, so this is implemented on the same principle as
/// the confirmed cases but flagged as the one genuinely unverified corner.
/// </summary>
internal static class FiveToneToneSequenceCodec
{
    /// <summary>Marker for Send Message's own Encode ID (row-level and
    /// BOT/EOT alike).</summary>
    public const string SendMessageMarker = "E1";

    /// <summary>Marker for PTTID's own Encode ID - BOT/EOT only, the
    /// row-level table's own PTTID rule never produces this (Encode ID
    /// stays empty there).</summary>
    public const string PttIdMarker = "E6";

    public static string Compose(string digits, string marker)
    {
        var sb = new StringBuilder();
        sb.Append(marker);

        var reference = marker[^1];
        var skipNext = false;
        foreach (var d in digits)
        {
            if (skipNext)
            {
                sb.Append(d);
                reference = d;
                skipNext = false;
                continue;
            }

            if (d == reference)
            {
                sb.Append('E');
                skipNext = true;
            }
            else
            {
                sb.Append(d);
                reference = d;
            }
        }

        sb.Append('E');
        if (sb.Length % 2 != 0)
        {
            sb.Append('E');
        }

        return sb.ToString();
    }

    /// <summary>Reverses <see cref="Compose"/> - needs <paramref name="digitCount"/>
    /// up front (always the current Self ID's own length, since Other Side
    /// ID is confirmed to always match it exactly) because the packed text
    /// has no self-describing length of its own to decode against. Returns
    /// null if <paramref name="text"/> doesn't start with <paramref name="marker"/>
    /// or isn't long enough to hold <paramref name="digitCount"/> digits -
    /// the caller falls back to treating the value as raw/manual hex.</summary>
    public static string? TryReverse(string text, string marker, int digitCount)
    {
        if (!text.StartsWith(marker, StringComparison.OrdinalIgnoreCase) || text.Length < marker.Length + digitCount)
        {
            return null;
        }

        var sb = new StringBuilder();
        var reference = marker[^1];
        var skipNext = false;
        for (var i = 0; i < digitCount; i++)
        {
            var c = char.ToUpperInvariant(text[marker.Length + i]);
            if (skipNext)
            {
                sb.Append(c);
                reference = c;
                skipNext = false;
            }
            else if (c == 'E')
            {
                sb.Append(reference);
                skipNext = true;
            }
            else
            {
                sb.Append(c);
                reference = c;
            }
        }

        return sb.ToString();
    }
}

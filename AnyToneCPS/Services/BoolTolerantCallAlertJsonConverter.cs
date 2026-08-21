using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AnyToneCPS.Services;

/// <summary>
/// Some project files store a CallAlert field as a JSON bool from before it
/// became the real 3-state string (None/Ring/Online Alert) - Talkgroup's own
/// CallAlert was corrected 2026-08-07 (see TalkgroupCodec's own doc
/// comment), Digital Contact's followed 2026-08-09 (see DigitalContactCodec's
/// own doc comment). Without this converter, loading an old project throws
/// JsonException and permanently locks the user out of their own saved data
/// instead of just re-reading an old value. There's no reliable old value to
/// recover (the old bit was decoded from the wrong byte position entirely,
/// or a coarser interpretation), so this maps true/false to the closest
/// reasonable guess (Online Alert/None) rather than failing - the user can
/// re-check each entry's Call Alert after loading. Shared by both
/// TalkgroupData.CallAlert and DigitalContactData.CallAlert.
/// </summary>
public sealed class BoolTolerantCallAlertJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.True or JsonTokenType.False)
        {
            return reader.GetBoolean() ? "Online Alert" : "None";
        }

        return reader.GetString() ?? "None";
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

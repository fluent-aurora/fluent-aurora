using System.Text.Json;
using System.Text.Json.Serialization;
using NLog;

namespace FluentAurora.Core.Converters;

/// <summary>
/// JSON converter for NLog LogLevel to handle serialization/deserialization using integer values
/// </summary>
public class LogLevelJsonConverter : JsonConverter<LogLevel>
{
    /// <summary>
    /// Reads and converts a JSON number to an NLog LogLevel instance.
    /// </summary>
    /// <param name="reader">The UTF-8 JSON reader to read from.</param>
    /// <param name="typeToConvert">The type of object to convert to, which is LogLevel in this case.</param>
    /// <param name="options">The options to use for reading and converting JSON.</param>
    /// <returns>The deserialized LogLevel value corresponding to the integer provided in the JSON.</returns>
    public override LogLevel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Try int first
        if (reader.TokenType == JsonTokenType.Number)
        {
            int value = reader.GetInt32();
            return value switch
            {
                0 => LogLevel.Trace,
                1 => LogLevel.Debug,
                2 => LogLevel.Info,
                3 => LogLevel.Warn,
                4 => LogLevel.Error,
                5 => LogLevel.Fatal,
                6 => LogLevel.Off,
                _ => LogLevel.Info
            };
        }

        // String as a fallback
        if (reader.TokenType == JsonTokenType.String)
        {
            // Maybe it's int saved as a string ("2")
            if (reader.TryGetInt32(out int intValue))
            {
                return intValue switch
                {
                    0 => LogLevel.Trace,
                    1 => LogLevel.Debug,
                    2 => LogLevel.Info,
                    3 => LogLevel.Warn,
                    4 => LogLevel.Error,
                    5 => LogLevel.Fatal,
                    6 => LogLevel.Off,
                    _ => LogLevel.Info
                };
            }

            // Try to get
            string? value = reader.GetString();
            if (!string.IsNullOrEmpty(value))
            {
                try
                {
                    return LogLevel.FromString(value);
                }
                catch
                {
                    // Failed to parse LogLevel by string
                }
            }
        }

        // Default to Info level
        return LogLevel.Info;
    }

    /// <summary>
    /// Writes an NLog LogLevel instance as an integer value in JSON format.
    /// </summary>
    /// <param name="writer">The UTF-8 JSON writer to write to.</param>
    /// <param name="value">The LogLevel instance to be written as an integer.</param>
    /// <param name="options">The options to use for writing and converting JSON.</param>
    public override void Write(Utf8JsonWriter writer, LogLevel value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value.Ordinal);
    }
}
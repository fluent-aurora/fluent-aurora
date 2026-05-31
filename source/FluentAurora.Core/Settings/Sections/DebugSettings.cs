using System.Text.Json.Serialization;
using FluentAurora.Core.Converters;
using NLog;

namespace FluentAurora.Core.Settings.Sections;

/// <summary>
/// Settings related to debugging and logging
/// </summary>
public class DebugSettings
{
    /// <summary>
    /// The minimum logging level for log output
    /// </summary>
    [JsonPropertyName("log_level")]
    [JsonConverter(typeof(LogLevelJsonConverter))]
    public LogLevel LogLevel { get; set; } = LogLevel.Info;
}
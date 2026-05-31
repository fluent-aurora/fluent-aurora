using System.Text.Json.Serialization;
using FluentAurora.Core.Settings.Sections;

namespace FluentAurora.Core.Settings;

/// <summary>
/// Manages application-specific settings with loading, saving, and access functionality.
/// </summary>
public class Settings
{
    /// <summary>
    /// Debugging and logging settings
    /// </summary>
    [JsonPropertyName("debug")]
    public DebugSettings Debug { get; set; } = new DebugSettings();

    /// <summary>
    /// Settings related to the user interface
    /// </summary>
    [JsonPropertyName("ui")]
    public UiSettings Ui { get; set; } = new UiSettings();
}
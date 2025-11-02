using System.Text.Json.Serialization;
using NLog;

namespace FluentAurora.Core.Settings;

public class ApplicationSettingsStore
{
    [JsonPropertyName("ui")]
    public UiSettings UiSettings { get; set; } = new UiSettings();

    [JsonPropertyName("playback")]
    public PlaybackSettings Playback { get; set; } = new PlaybackSettings();

    [JsonPropertyName("debug")]
    public DebuggingSettings Debug { get; set; } = new DebuggingSettings();
}

public class UiSettings
{
    [JsonPropertyName("theme")]
    public AppTheme Theme { get; set; } = AppTheme.Dark;
}

public enum AppTheme
{
    Light,
    Dark,
    Black
}

public class PlaybackSettings
{
    [JsonPropertyName("reactive_artwork")]
    public ReactiveArtwork ReactiveArtwork { get; set; } = new ReactiveArtwork();

    [JsonPropertyName("volume")]
    public float Volume { get; set; } = 1.0f;
}

public class ReactiveArtwork
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = false;

    [JsonPropertyName("scale")]
    public ArtworkScale Scale { get; set; } = new();

    public class ArtworkScale
    {
        [JsonPropertyName("base")]
        public double Base { get; set; } = 0.8;

        [JsonPropertyName("max")]
        public double Max { get; set; } = 1.3;
    }
}

public class DebuggingSettings
{
    [JsonPropertyName("logging")]
    public Logging Logger { get; set; } = new Logging();

    public class Logging
    {
        [JsonPropertyName("level")]
        public string Level { get; set; } = "Info";
    }
}
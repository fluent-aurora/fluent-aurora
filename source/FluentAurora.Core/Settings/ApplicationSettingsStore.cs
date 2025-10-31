using System.Text.Json.Serialization;

namespace FluentAurora.Core.Settings;

public class ApplicationSettingsStore
{
    [JsonPropertyName("playback")]
    public PlaybackSettings Playback { get; set; } = new PlaybackSettings();
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
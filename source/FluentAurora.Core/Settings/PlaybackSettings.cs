using System.Text.Json.Serialization;

namespace FluentAurora.Core.Settings;

public class PlaybackSettings
{
    [JsonPropertyName("reactive_artwork")]
    public ReactiveArtwork ReactiveArtwork { get; set; } = new ReactiveArtwork();
    
    [JsonPropertyName("volume")]
    public float Volume { get; set; }
}

public class ReactiveArtwork
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("scale")]
    public ArtworkScale Scale { get; set; } = new ArtworkScale();

    public class ArtworkScale
    {
        [JsonPropertyName("base")]
        public double Base { get; set; }

        [JsonPropertyName("max")]
        public double Max { get; set; }
    }
}
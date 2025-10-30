using System.Text.Json.Serialization;

namespace FluentAurora.Core.Settings;

public interface IApplicationSettings : ISettingsService<ApplicationSettingsStore>
{
}

public class ApplicationSettings : AbstractSettings<ApplicationSettingsStore>, IApplicationSettings
{
    public ApplicationSettings() : base("config.json")
    {
    }

    protected override ApplicationSettingsStore DefaultSettings => new ApplicationSettingsStore
    {
        Playback = new PlaybackSettings
        {
            ReactiveArtwork = new ReactiveArtwork
            {
                Enabled = false,
                Scale = new ReactiveArtwork.ArtworkScale
                {
                    Base = 0.8,
                    Max = 1.3
                }
            },
            Volume = 1.0f
        }
    };
}

public class ApplicationSettingsStore
{
    [JsonPropertyName("playback")]
    public PlaybackSettings Playback { get; set; } = new PlaybackSettings();
}
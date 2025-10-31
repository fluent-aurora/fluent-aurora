using FluentAurora.Core.Paths;

namespace FluentAurora.Core.Settings;

public interface IApplicationSettings : ISettingsService<ApplicationSettingsStore>
{
}

public class ApplicationSettings() : JsonSettingsService<ApplicationSettingsStore>(Path.Combine(PathResolver.Config, "config.json")), IApplicationSettings
{
    protected override ApplicationSettingsStore Default => new ApplicationSettingsStore
    {
        Playback = new PlaybackSettings
        {
            Volume = 1.0f,
            ReactiveArtwork = new ReactiveArtwork
            {
                Enabled = false,
                Scale = new ReactiveArtwork.ArtworkScale
                {
                    Base = 0.8,
                    Max = 1.3
                }
            }
        }
    };
}
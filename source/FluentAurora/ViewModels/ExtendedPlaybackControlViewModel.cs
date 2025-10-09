using FluentAurora.Core.Playback;
using FluentAurora.Services;

namespace FluentAurora.ViewModels;

public class ExtendedPlaybackControlViewModel : PlaybackControlViewModel
{
    public ExtendedPlaybackControlViewModel(AudioPlayerService audioPlayerService, PlaybackControlService playbackControlService) : base(audioPlayerService, playbackControlService)
    {
    }
}
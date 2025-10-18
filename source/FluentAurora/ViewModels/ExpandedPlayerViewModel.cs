using FluentAurora.Core.Playback;
using FluentAurora.Services;

namespace FluentAurora.ViewModels;

public class ExpandedPlayerViewModel : CompactPlayerViewModel
{
    public ExpandedPlayerViewModel(AudioPlayerService audioPlayerService, PlaybackControlService playbackControlService, StoragePickerService storagePickerService) : base(audioPlayerService, playbackControlService, storagePickerService)
    {
    }
}
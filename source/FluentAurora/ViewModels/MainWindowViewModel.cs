using FluentAurora.Services;

namespace FluentAurora.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public PlaybackControlService PlaybackControlService { get; }

    public MainWindowViewModel(PlaybackControlService playbackControlService)
    {
        PlaybackControlService = playbackControlService;
    }
}
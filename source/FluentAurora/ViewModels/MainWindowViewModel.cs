using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentAurora.Services;

namespace FluentAurora.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public PlaybackControlService PlaybackControlService { get; }
    public double MinWindowHeight => PlaybackControlService.IsExpanded ? 885 : 110;

    public MainWindowViewModel(PlaybackControlService playbackControlService)
    {
        PlaybackControlService = playbackControlService;
        PlaybackControlService.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PlaybackControlService.IsExpanded))
            {
                OnPropertyChanged(nameof(MinWindowHeight));
            }
        };
    }
}
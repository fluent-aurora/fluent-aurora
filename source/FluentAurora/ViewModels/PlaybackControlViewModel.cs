using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FluentAurora.ViewModels;

public partial class PlaybackControlViewModel : ViewModelBase
{
    // Variables
    [ObservableProperty] private string songTitle = "Title";
    [ObservableProperty] private string songArtist = "Artists";
    [ObservableProperty] private int songDuration = 314;
    [ObservableProperty] private int currentPosition = 128;
    [ObservableProperty] private int currentVolume;
    [ObservableProperty] private bool isPlaying;

    public string PlayPauseIcon => IsPlaying ? "PauseFilled" : "PlayFilled";

    public string VolumeIcon => CurrentVolume switch
    {
        >= 1 and < 10 => "Speaker0",
        >= 10 and < 50 => "Speaker1",
        >= 50 and < 100 => "Speaker2",
        100 => "Volume",
        _ => "SpeakerMute"
    };


    // Events
    partial void OnIsPlayingChanged(bool value)
    {
        OnPropertyChanged(nameof(PlayPauseIcon));
    }
    partial void OnCurrentVolumeChanged(int value)
    {
        OnPropertyChanged(nameof(VolumeIcon));
    }

    [RelayCommand]
    private void TogglePlay()
    {
        IsPlaying = !IsPlaying;
    }
}
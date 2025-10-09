using CommunityToolkit.Mvvm.ComponentModel;

namespace FluentAurora.Services;

public partial class PlaybackControlService : ObservableObject
{
    [ObservableProperty] private bool isExpanded;

    public void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }
}
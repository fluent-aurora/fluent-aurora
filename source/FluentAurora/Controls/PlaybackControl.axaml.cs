using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentAurora.ViewModels;

namespace FluentAurora.Controls;

public partial class PlaybackControl : UserControl
{
    public PlaybackControl()
    {
        InitializeComponent();
        DataContext = new PlaybackControlViewModel();
    }
}
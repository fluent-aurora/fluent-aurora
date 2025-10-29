using Avalonia.Controls;
using Avalonia.Input;
using FluentAurora.Core.Playback;
using FluentAurora.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FluentAurora.Views;

public partial class LibraryView : UserControl
{
    public LibraryView()
    {
        InitializeComponent();
        DataContext = App.Services?.GetRequiredService<LibraryViewModel>();
    }

    private void SongElement_OnTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border border || DataContext is not LibraryViewModel vm)
        {
            return;
        }

        vm.PlaySongCommand.Execute(border.DataContext);
    }

    private void CloseOverlay_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is LibraryViewModel vm)
        {
            vm.ClosePlaylistDetailsCommand.Execute(null);
        }
    }
}
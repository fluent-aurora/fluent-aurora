using Avalonia.Controls;
using FluentAurora.ViewModels;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using FluentAurora.Core.Logging;
using FluentAvalonia.UI.Media.Animation;

namespace FluentAurora.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Services?.GetRequiredService<MainWindowViewModel>();
    }

    private void NavigationView_OnItemInvoked(object? sender, NavigationViewItemInvokedEventArgs e)
    {
        if (e.InvokedItemContainer is not NavigationViewItem item)
        {
            return;
        }
        string tag = item.Tag?.ToString() ?? string.Empty;
        switch (tag)
        {
            case "Library":
                Logger.Info("Navigating to library");
                ContentFrame.Navigate(typeof(LibraryView), null, new EntranceNavigationTransitionInfo());
                break;
            case "Settings":
                Logger.Info("Navigating to settings");
                ContentFrame.Navigate(typeof(SettingsView), null, new EntranceNavigationTransitionInfo());
                break;
            default:
                Logger.Warning($"Unknown navigation view tag: {tag}");
                ContentFrame.Content = null;
                break;
        }
    }
}
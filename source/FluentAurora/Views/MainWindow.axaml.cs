using Avalonia.Controls;
using FluentAurora.ViewModels;
using FluentAvalonia.UI.Controls;
using Microsoft.Extensions.DependencyInjection;
using FluentAurora.Core.Logging;

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
                ContentFrame.Content = App.Services?.GetRequiredService<LibraryView>();
                break;
            default:
                Logger.Warning($"Unknown navigation view tag: {tag}");
                ContentFrame.Content = null;
                break;
        }
    }
}
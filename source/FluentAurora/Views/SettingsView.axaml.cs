using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentAurora.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FluentAurora.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        DataContext = App.Services?.GetRequiredService<SettingsViewModel>();
    }
}
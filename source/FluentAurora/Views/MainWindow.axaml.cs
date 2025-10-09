using Avalonia.Controls;
using FluentAurora.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FluentAurora.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Services?.GetRequiredService<MainWindowViewModel>();
    }
}
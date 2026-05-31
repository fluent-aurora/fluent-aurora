using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using FluentAurora.ViewModels;

namespace FluentAurora.Views;

public partial class MainView : UserControl
{
    // Properties
    private MainViewViewModel _viewModel { get; set; }

    // Constructor
    public MainView()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<MainViewViewModel>();
        DataContext = _viewModel;
    }
}
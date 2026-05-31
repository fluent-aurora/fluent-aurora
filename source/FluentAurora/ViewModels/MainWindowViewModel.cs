using CommunityToolkit.Mvvm.ComponentModel;
using FluentAurora.Services;

namespace FluentAurora.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public string WindowTitle { get; } = LocalizationService.GetText("MainWindow.Title");
}
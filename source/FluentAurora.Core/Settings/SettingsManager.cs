namespace FluentAurora.Core.Settings;

public interface ISettingsManager : IDisposable
{
    ApplicationSettingsStore Application { get; }
    void SaveAll();
    event EventHandler<ApplicationSettingsStore>? ApplicationSettingsChanged;
}

public class SettingsManager : ISettingsManager
{
    private readonly IApplicationSettings _applicationSettings;

    public event EventHandler<ApplicationSettingsStore>? ApplicationSettingsChanged;

    public SettingsManager(IApplicationSettings applicationSettings)
    {
        _applicationSettings = applicationSettings;

        _applicationSettings.SettingsChanged += OnApplicationSettingsChanged;
    }

    public ApplicationSettingsStore Application => _applicationSettings.Settings;

    public void SaveAll()
    {
        _applicationSettings.SaveSettings();
    }

    private void OnApplicationSettingsChanged(object? sender, ApplicationSettingsStore settings)
    {
        ApplicationSettingsChanged?.Invoke(this, settings);
    }

    public void Dispose()
    {
        _applicationSettings.SettingsChanged -= OnApplicationSettingsChanged;
    }
}
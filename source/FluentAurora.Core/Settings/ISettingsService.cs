namespace FluentAurora.Core.Settings;

public interface ISettingsService<T> where T : class, new()
{
    event EventHandler<T>? SettingsChanged;
    T Settings { get; }
    bool SaveSettings();
    bool SaveSettings(T settings);
    void ReloadSettings();
}
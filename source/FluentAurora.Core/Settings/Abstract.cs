using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAurora.Core.Logging;
using FluentAurora.Core.Paths;

namespace FluentAurora.Core.Settings;

public abstract class AbstractSettings<T> : ISettingsService<T> where T : class, new()
{
    // Properties
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    private readonly string _settingsPath;
    private readonly object _lock = new();
    private T? _settings;

    protected virtual T DefaultSettings => new();

    public T Settings
    {
        get
        {
            if (_settings != null)
            {
                return _settings;
            }

            lock (_lock)
            {
                if (_settings != null)
                {
                    return _settings;
                }

                _settings = LoadSettings();
                return _settings;
            }
        }
    }

    // Events
    public event EventHandler<T>? SettingsChanged;

    // Constructor
    protected AbstractSettings(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        Directory.CreateDirectory(PathResolver.Config);
        _settingsPath = Path.Combine(PathResolver.Config, fileName);

        // Load settings on initialization
        _ = Settings;
    }

    // Functions
    protected virtual void OnSettingsChanged(T settings)
    {
        SettingsChanged?.Invoke(this, settings);
    }

    private T LoadSettings()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                T defaults = DefaultSettings;
                SaveSettings(defaults);
                return defaults;
            }

            string settingsSerialized = File.ReadAllText(_settingsPath);
            T? settings = JsonSerializer.Deserialize<T>(settingsSerialized, JsonOptions);
            return settings ?? DefaultSettings;
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to load settings from {_settingsPath}");
            Logger.LogExceptionDetails(ex);
            return DefaultSettings;
        }
    }

    public bool SaveSettings()
    {
        lock (_lock)
        {
            if (_settings == null)
            {
                _settings = LoadSettings();
            }
            return SaveSettings(_settings);
        }
    }

    public bool SaveSettings(T settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            lock (_lock)
            {
                string settingsSerialized = JsonSerializer.Serialize(settings, JsonOptions);

                // Atomic write: write to temp file then replace
                string tempPath = _settingsPath + ".tmp";
                File.WriteAllText(tempPath, settingsSerialized);
                File.Move(tempPath, _settingsPath, overwrite: true);

                _settings = settings;
                OnSettingsChanged(settings);
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to save settings to {_settingsPath}");
            Logger.LogExceptionDetails(ex);
            return false;
        }
    }

    public void ReloadSettings()
    {
        lock (_lock)
        {
            _settings = null;
            _ = Settings;
            OnSettingsChanged(Settings);
        }
    }
}
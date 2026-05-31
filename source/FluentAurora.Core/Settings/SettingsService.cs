using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAurora.Core.Converters;
using FluentAurora.Core.Logging;
using FluentAurora.Core.Utilities;

namespace FluentAurora.Core.Settings;

/// <summary>
/// Service for managing application settings with JSON file persistence.
/// Loads and saves settings with support for backup recovery and thread safety.
/// </summary>
public sealed class SettingsService : ISettingsService<Settings>
{
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _settingsPath;
    private readonly string _settingsBackupPath;
    private readonly Lock _lock = new Lock();
    private Settings _settings = null!;
    private bool _settingsLoaded;

    /// <summary>
    /// Gets the currently loaded settings instance. Loads the settings from persistent storage if not yet initialized.
    /// </summary>
    public Settings Settings
    {
        get
        {
            if (!_settingsLoaded)
            {
                LoadSettings();
            }
            return _settings;
        }
    }

    /// <summary>
    /// Occurs when settings have been changed.
    /// </summary>
    public event EventHandler? SettingsChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService"/> class with default JSON serialization options.
    /// </summary>
    public SettingsService()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Converters =
            {
                new JsonStringEnumConverter(),
                new LogLevelJsonConverter()
            }
        };

        _settingsPath = PathResolver.GetFullPath("Config", "config.json");
        _settingsBackupPath = PathResolver.GetFullPath("Config", "config.json.backup");

        // Ensure config directory exists
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
    }

    /// <summary>
    /// Loads settings from the persistent storage file. If the file does not exist or fails to load, the default settings are returned.
    /// Invalid property values are automatically replaced with their defaults.
    /// </summary>
    /// <returns>The loaded settings instance, or default settings if loading fails.</returns>
    public Settings LoadSettings()
    {
        lock (_lock)
        {
            try
            {
                if (!File.Exists(_settingsPath))
                {
                    AppLogger.Info("Settings file does not exist, creating default settings");
                    _settings = new Settings();
                    SaveSettingsInternal();
                    _settingsLoaded = true;
                    return _settings;
                }

                string json = File.ReadAllText(_settingsPath);

                // Load defaults first, then overlay valid JSON values on top
                _settings = LenientJsonDeserializer.Deserialize<Settings>(json, _jsonOptions);
                _settingsLoaded = true;
                SaveSettingsInternal();
                return _settings;
            }
            catch (Exception ex)
            {
                AppLogger.Warning($"Failed to load settings, attempting backup: {ex.Message}");

                // Try loading from backup
                try
                {
                    if (File.Exists(_settingsBackupPath))
                    {
                        string backupJson = File.ReadAllText(_settingsBackupPath);
                        _settings = LenientJsonDeserializer.Deserialize<Settings>(backupJson, _jsonOptions);
                        _settingsLoaded = true;
                        AppLogger.Info("Settings loaded from backup");
                        return _settings;
                    }
                }
                catch (Exception backupEx)
                {
                    AppLogger.Warning($"Backup load failed: {backupEx.Message}");
                }

                // Fall back to defaults
                AppLogger.Info("Using default settings");
                _settings = new Settings();
                _settingsLoaded = true;
                return _settings;
            }
        }
    }

    /// <summary>
    /// Asynchronously loads settings from the persistent storage.
    /// Invalid property values are automatically replaced with their defaults.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains the loaded settings instance, or default settings if loading fails.</returns>
    public async Task<Settings> LoadSettingsAsync()
    {
        return await Task.Run(LoadSettings);
    }

    /// <summary>
    /// Saves the current settings instance to persistent storage.
    /// </summary>
    public void SaveSettings()
    {
        SaveSettingsInternal();
    }

    /// <summary>
    /// Asynchronously saves the current settings instance to persistent storage.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SaveSettingsAsync()
    {
        await Task.Run(SaveSettingsInternal);
    }

    /// <summary>
    /// Saves the provided settings instance to persistent storage.
    /// </summary>
    /// <param name="settings">The settings instance to save.</param>
    public void SaveSettings(Settings settings)
    {
        lock (_lock)
        {
            _settings = settings;
            SaveSettingsInternal();
        }
    }

    /// <summary>
    /// Asynchronously saves the provided settings instance to persistent storage.
    /// </summary>
    /// <param name="settings">The settings instance to save.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task SaveSettingsAsync(Settings settings)
    {
        await Task.Run(() => SaveSettings(settings));
    }

    /// <summary>
    /// Creates a backup of the settings file and writes the current settings to persistent storage.
    /// Raises the <see cref="SettingsChanged"/> event after a successful save.
    /// </summary>
    private void SaveSettingsInternal()
    {
        lock (_lock)
        {
            try
            {
                CreateBackup();

                string json = JsonSerializer.Serialize(_settings, _jsonOptions);
                File.WriteAllText(_settingsPath, json);

                OnSettingsChanged();
            }
            catch (Exception ex)
            {
                AppLogger.Error($"There was an unexpected error saving settings to {_settingsPath}");
                AppLogger.LogExceptionDetails(ex);
            }
        }
    }

    /// <summary>
    /// Raises the SettingsChanged event.
    /// </summary>
    private void OnSettingsChanged()
    {
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Creates a backup of the settings file if it exists.
    /// </summary>
    private void CreateBackup()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                File.Copy(_settingsPath, _settingsBackupPath, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Warning($"Failed to create settings backup: {ex.Message}");
        }
    }
}
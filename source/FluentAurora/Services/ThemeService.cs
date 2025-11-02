using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using FluentAurora.Core.Logging;
using FluentAurora.Core.Settings;

namespace FluentAurora.Services;

public class ThemeService
{
    private AppTheme _currentTheme = AppTheme.Dark;
    private FluentAvaloniaTheme? _faTheme;
    private readonly Dictionary<AppTheme, ResourceInclude?> _themeResources = new();
    private readonly Dictionary<AppTheme, ThemeConfiguration> _themeConfigs;

    // Theme configuration class
    private class ThemeConfiguration
    {
        public ThemeVariant? BaseTheme { get; set; }
        public string? ResourcePath { get; set; }
        public AppTheme? FallbackTheme { get; set; }
    }

    public ThemeService()
    {
        // Configure all themes
        _themeConfigs = new Dictionary<AppTheme, ThemeConfiguration>
        {
            [AppTheme.Light] = new ThemeConfiguration
            {
                BaseTheme = ThemeVariant.Light,
                ResourcePath = null,
                FallbackTheme = null
            },
            [AppTheme.Dark] = new ThemeConfiguration
            {
                BaseTheme = ThemeVariant.Dark,
                ResourcePath = null,
                FallbackTheme = null
            },
            [AppTheme.Black] = new ThemeConfiguration
            {
                BaseTheme = ThemeVariant.Dark, // Based on dark theme
                ResourcePath = "avares://FluentAurora/Resources/Themes/Black.axaml",
                FallbackTheme = AppTheme.Dark
            }
            // New themes need to be added here
        };

        // Find FluentAvalonia Theme in Application Styles
        if (Application.Current == null)
        {
            return;
        }

        foreach (IStyle style in Application.Current.Styles)
        {
            if (style is not FluentAvaloniaTheme faTheme)
            {
                continue;
            }
            _faTheme = faTheme;
            break;
        }
    }

    public void SetTheme(AppTheme theme)
    {
        if (!_themeConfigs.ContainsKey(theme))
        {
            Logger.Error($"Theme {theme} is not configured");
            return;
        }

        Logger.Info($"Switching to {theme} theme");

        // Remove previously loaded theme and apply the new theme
        RemoveCurrentThemeResources();
        ApplyTheme(theme);

        _currentTheme = theme;
    }

    private void ApplyTheme(AppTheme theme)
    {
        if (!_themeConfigs.TryGetValue(theme, out var config))
        {
            Logger.Error($"No configuration found for theme {theme}");
            return;
        }

        // First apply base theme and then the actual theme (if specified)
        if (config.BaseTheme != null && Application.Current != null)
        {
            Application.Current.RequestedThemeVariant = config.BaseTheme;
        }

        if (!string.IsNullOrEmpty(config.ResourcePath))
        {
            LoadThemeResources(theme, config.ResourcePath, config.FallbackTheme);
        }
    }

    private void LoadThemeResources(AppTheme theme, string resourcePath, AppTheme? fallbackTheme)
    {
        if (Application.Current == null)
        {
            return;
        }

        try
        {
            Uri uri = new Uri(resourcePath);
            ResourceInclude resourceInclude = new ResourceInclude(uri)
            {
                Source = uri
            };

            // Store resource for later removal and add it to the Application Resources
            _themeResources[theme] = resourceInclude;
            Application.Current.Resources.MergedDictionaries.Add(resourceInclude);
            Logger.Info($"Theme resources loaded for {theme}");
        }
        catch (Exception ex)
        {
            // Apply fallback theme if it was specified
            Logger.Error($"Failed to load theme resources for {theme}: {ex.Message}");
            if (fallbackTheme != null)
            {
                Logger.Warning($"Falling back to {fallbackTheme.Value} theme");
                ApplyTheme(fallbackTheme.Value);
            }
        }
    }

    private void RemoveCurrentThemeResources()
    {
        if (Application.Current == null)
        {
            return;
        }

        // Remove all loaded theme resources
        foreach (KeyValuePair<AppTheme, ResourceInclude?> kvp in _themeResources)
        {
            if (kvp.Value == null)
            {
                continue;
            }
            Application.Current.Resources.MergedDictionaries.Remove(kvp.Value);
            Logger.Debug($"Removed theme resources for {kvp.Key}");
        }

        _themeResources.Clear();
    }

    public AppTheme GetCurrentTheme() => _currentTheme;

    public IEnumerable<AppTheme> GetAvailableThemes()
    {
        return _themeConfigs.Keys;
    }
}
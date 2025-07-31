using System.Windows;
using Microsoft.Win32;

namespace TicketTracker.Services;

public class ThemeService
{
    public enum Theme
    {
        System,
        Light,
        Dark
    }

    private const string LIGHT_THEME_URI = "pack://application:,,,/Themes/LightTheme.xaml";
    private const string DARK_THEME_URI = "pack://application:,,,/Themes/DarkTheme.xaml";

    private Theme _currentTheme = Theme.System;

    public Theme CurrentTheme
    {
        get => _currentTheme;
        set
        {
            if (_currentTheme != value)
            {
                _currentTheme = value;
                ApplyTheme();
                ThemeChanged?.Invoke(this, value);
            }
        }
    }

    public event EventHandler<Theme>? ThemeChanged;

    public void ApplyTheme()
    {
        var app = Application.Current;
        if (app?.Resources == null) return;

        // Remove existing theme resources
        var existingTheme = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source?.OriginalString.Contains("Theme.xaml") == true);

        if (existingTheme != null) app.Resources.MergedDictionaries.Remove(existingTheme);

        // Determine actual theme to apply
        bool useDarkTheme;
        if (_currentTheme == Theme.System)
            useDarkTheme = IsSystemDarkTheme();
        else
            useDarkTheme = _currentTheme == Theme.Dark;

        // Add new theme
        var themeUri = useDarkTheme ? DARK_THEME_URI : LIGHT_THEME_URI;
        var newTheme = new ResourceDictionary { Source = new Uri(themeUri) };
        app.Resources.MergedDictionaries.Add(newTheme);
    }

    private bool IsSystemDarkTheme()
    {
        try
        {
            const string registryKeyPath = @"HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
            const string registryValueName = "AppsUseLightTheme";

            var registryValueObject = Registry.GetValue(registryKeyPath, registryValueName, null);
            if (registryValueObject == null)
                return false;

            var registryValue = (int)registryValueObject;
            return registryValue == 0; // 0 = Dark, 1 = Light
        }
        catch
        {
            return false; // Default to light theme if unable to detect
        }
    }
}
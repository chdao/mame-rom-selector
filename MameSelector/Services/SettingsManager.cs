using System.Configuration;
using System.Text.Json;
using MameSelector.Models;

namespace MameSelector.Services;

/// <summary>
/// Manages application settings persistence
/// </summary>
public class SettingsManager
{
    private readonly string _settingsFilePath;
    private AppSettings? _cachedSettings;

    public SettingsManager()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "MameSelector");
        Directory.CreateDirectory(appFolder);
        _settingsFilePath = Path.Combine(appFolder, "settings.json");
    }

    /// <summary>
    /// Loads settings from file or returns default settings
    /// </summary>
    public async Task<AppSettings> LoadSettingsAsync()
    {
        if (_cachedSettings != null)
            return _cachedSettings;

        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath);
                _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            else
            {
                _cachedSettings = new AppSettings();
            }
        }
        catch (Exception ex)
        {
            // If settings file is corrupted, create new default settings
            _cachedSettings = new AppSettings();
            // Log the error (you might want to show this to the user)
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
        }

        return _cachedSettings;
    }

    /// <summary>
    /// Saves settings to file
    /// </summary>
    public async Task SaveSettingsAsync(AppSettings settings)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            
            var json = JsonSerializer.Serialize(settings, options);
            await File.WriteAllTextAsync(_settingsFilePath, json);
            _cachedSettings = settings;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save settings: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets the current settings (loads if not cached)
    /// </summary>
    public async Task<AppSettings> GetSettingsAsync()
    {
        return await LoadSettingsAsync();
    }

    /// <summary>
    /// Resets settings to default values
    /// </summary>
    public async Task ResetSettingsAsync()
    {
        _cachedSettings = new AppSettings();
        await SaveSettingsAsync(_cachedSettings);
    }
}


using System.Configuration;
using System.Text.Json;
using MameSelector.Models;

namespace MameSelector.Services;

/// <summary>
/// Manages application settings persistence
/// </summary>
public class SettingsManager
{
    private string _settingsFilePath;
    private AppSettings? _cachedSettings;

    public SettingsManager()
    {
        // We'll determine the path based on the portable mode setting
        // For now, default to portable mode (alongside executable)
        _settingsFilePath = GetSettingsFilePath(true);
    }

    /// <summary>
    /// Gets the settings file path based on portable mode
    /// </summary>
    private string GetSettingsFilePath(bool portableMode)
    {
        if (portableMode)
        {
            // Store alongside the executable
            var exeDir = AppContext.BaseDirectory;
            return Path.Combine(exeDir, "settings.json");
        }
        else
        {
            // Store in AppData
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "MameSelector");
            Directory.CreateDirectory(appFolder);
            return Path.Combine(appFolder, "settings.json");
        }
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
            // First, try to load from portable location (default)
            var portablePath = GetSettingsFilePath(true);
            var appDataPath = GetSettingsFilePath(false);
            
            AppSettings? loadedSettings = null;
            
            // Try portable first
            if (File.Exists(portablePath))
            {
                var json = await File.ReadAllTextAsync(portablePath);
                loadedSettings = JsonSerializer.Deserialize<AppSettings>(json);
            }
            // If not found in portable, try AppData
            else if (File.Exists(appDataPath))
            {
                var json = await File.ReadAllTextAsync(appDataPath);
                loadedSettings = JsonSerializer.Deserialize<AppSettings>(json);
                
                // If we found settings in AppData but portable mode is enabled, migrate them
                if (loadedSettings?.PortableMode == true)
                {
                    await File.WriteAllTextAsync(portablePath, json);
                    // Don't delete the AppData file yet - let user decide
                }
            }
            
            _cachedSettings = loadedSettings ?? new AppSettings();
            
            // Update the settings file path based on the loaded portable mode setting
            _settingsFilePath = GetSettingsFilePath(_cachedSettings.PortableMode);
        }
        catch (Exception ex)
        {
            // If settings file is corrupted, create new default settings
            _cachedSettings = new AppSettings();
            // Log the error (you might want to show this to the user)
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
            
            // Check if portable mode has changed
            var newPath = GetSettingsFilePath(settings.PortableMode);
            var oldPath = _settingsFilePath;
            
            // Save to the new location
            await File.WriteAllTextAsync(newPath, json);
            
            // If the path changed, clean up the old file
            if (newPath != oldPath && File.Exists(oldPath))
            {
                try
                {
                    File.Delete(oldPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            
            _settingsFilePath = newPath;
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


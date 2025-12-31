using System.Text.Json;

namespace LanTransfer.Core.Settings;

/// <summary>
/// Manages user settings with persistent storage.
/// </summary>
public class SettingsManager
{
    private readonly string _settingsPath;
    private UserSettings _settings;
    
    public UserSettings Settings => _settings;
    public string Username => _settings.Username;
    
    public SettingsManager()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KiTransfert"
        );
        Directory.CreateDirectory(appDataPath);
        _settingsPath = Path.Combine(appDataPath, "settings.json");
        
        _settings = Load();
    }
    
    private UserSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Settings] Failed to load: {ex.Message}");
        }
        
        return new UserSettings();
    }
    
    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            File.WriteAllText(_settingsPath, json);
            Console.WriteLine($"[Settings] Saved: {_settings.Username}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Settings] Failed to save: {ex.Message}");
        }
    }
    
    public void SetUsername(string username)
    {
        _settings.Username = username.Trim();
        Save();
    }
    
    public bool HasUsername => !string.IsNullOrWhiteSpace(_settings.Username);
}

public class UserSettings
{
    public string Username { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

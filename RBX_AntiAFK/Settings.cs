using Newtonsoft.Json;

public class Settings
{
    public string ActionType { get; set; } = "Jump";
    public bool EnableDelay { get; set; } = false;
    public decimal DelaySeconds { get; set; } = 3;
    public bool HideWindowContents { get; set; } = false;

    private const string SettingsFile = "settings.json";

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                var loadedSettings = JsonConvert.DeserializeObject<Settings>(json);

                if (loadedSettings != null)
                {
                    ActionType = loadedSettings.ActionType;
                    EnableDelay = loadedSettings.EnableDelay;
                    DelaySeconds = loadedSettings.DelaySeconds;
                    HideWindowContents = loadedSettings.HideWindowContents;
                }
                else
                {
                    Save();
                }
            }
            else
            {
                Save();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading settings: {ex.Message}");
        }
    }

    public void Save()
    {
        try
        {
            var json = JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(SettingsFile, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving settings: {ex.Message}");
        }
    }
}

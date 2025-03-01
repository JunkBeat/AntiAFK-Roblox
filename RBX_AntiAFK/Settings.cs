using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RBX_AntiAFK;

public class Settings
{
    public string ActionType { get; set; } = "Jump";
    public bool EnableWindowMaximization { get; set; } = false;
    public decimal WindowMaximizationDelaySeconds { get; set; } = 3;
    public bool HideWindowContentsOnMaximizing { get; set; } = false;
    public int DelayBeforeWindowInteractionMilliseconds { get; set; } = 50;
    public int DelayBetweenKeyPressMilliseconds { get; set; } = 45;

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
                    EnableWindowMaximization = loadedSettings.EnableWindowMaximization;
                    WindowMaximizationDelaySeconds = loadedSettings.WindowMaximizationDelaySeconds;
                    HideWindowContentsOnMaximizing = loadedSettings.HideWindowContentsOnMaximizing;
                    DelayBeforeWindowInteractionMilliseconds = loadedSettings.DelayBeforeWindowInteractionMilliseconds;
                    DelayBetweenKeyPressMilliseconds = loadedSettings.DelayBetweenKeyPressMilliseconds;
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

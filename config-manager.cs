using System;
using System.IO;
using System.Text.Json;

namespace ClipboardTranslator
{
    public class TranslationSettings
    {
        public string DefaultSourceLanguage { get; set; } = "Auto Detect";
        public string DefaultTargetLanguage { get; set; } = "English";
        public string DefaultTone { get; set; } = "Neutral";
        public string GoogleApiKey { get; set; } = ""; // Keep for backward compatibility
        public string GeminiApiKey { get; set; } = ""; // New property for Gemini API
        public string OpenAIApiKey { get; set; } = "";
        public string PreferredService { get; set; } = "Gemini"; // Gemini or OpenAI
        public bool StartWithWindows { get; set; } = false;
        public bool StartMinimized { get; set; } = false;
        public bool PlaySoundOnTranslation { get; set; } = false; // Alterado para false
        public int TranslationsToday { get; set; } = 0;
        public DateTime LastTranslationDate { get; set; } = DateTime.MinValue;
        public int MaxTokensLimit { get; set; } = 1000; // Valor padrão
        public bool EnableTokenLimit { get; set; } = true; // Habilitado por padrão
    }

    public static class ConfigManager
    {
        private static readonly string ConfigFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ClipboardTranslator");

        private static readonly string ConfigFile = Path.Combine(ConfigFolder, "settings.json");

        public static TranslationSettings LoadSettings()
        {
            try
            {
                // Create config folder if it doesn't exist
                if (!Directory.Exists(ConfigFolder))
                {
                    Directory.CreateDirectory(ConfigFolder);
                }

                // Check if the config file exists
                if (File.Exists(ConfigFile))
                {
                    // Read and deserialize the file
                    string json = File.ReadAllText(ConfigFile);
                    var settings = JsonSerializer.Deserialize<TranslationSettings>(json);

                    // If it's an older version without GeminiApiKey, migrate the GoogleApiKey
                    if (settings != null && string.IsNullOrEmpty(settings.GeminiApiKey) && !string.IsNullOrEmpty(settings.GoogleApiKey))
                    {
                        settings.GeminiApiKey = settings.GoogleApiKey;
                    }

                    // Return the read settings
                    return settings ?? new TranslationSettings();
                }

                // If the file doesn't exist, create default settings
                var defaultSettings = new TranslationSettings();
                SaveSettings(defaultSettings);
                return defaultSettings;
            }
            catch (Exception)
            {
                // In case of error, return default settings
                return new TranslationSettings();
            }
        }

        public static void SaveSettings(TranslationSettings settings)
        {
            try
            {
                // Create config folder if it doesn't exist
                if (!Directory.Exists(ConfigFolder))
                {
                    Directory.CreateDirectory(ConfigFolder);
                }

                // Serialize and save settings
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(ConfigFile, json);
            }
            catch (Exception)
            {
                // Silently fail (could have a log here)
            }
        }

        // Configure startup with Windows
        public static void SetStartWithWindows(bool enable)
        {
            try
            {
                // Get executable path
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;

                // Get registry key
                var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

                if (key != null)
                {
                    if (enable)
                    {
                        // Add to registry
                        key.SetValue("ClipboardTranslator", exePath);
                    }
                    else
                    {
                        // Remove from registry
                        key.DeleteValue("ClipboardTranslator", false);
                    }
                }
            }
            catch (Exception)
            {
                // Silently fail (could have a log here)
            }
        }
    }
}
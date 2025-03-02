using System;
using System.IO;
using System.Text.Json;

namespace ClipboardTranslator
{
    public class TranslationSettings
    {
        public string DefaultSourceLanguage { get; set; } = "Detecção automática";
        public string DefaultTargetLanguage { get; set; } = "Português";
        public string DefaultTone { get; set; } = "Neutro";
        public string GoogleApiKey { get; set; } = "";
        public bool StartWithWindows { get; set; } = false;
        public bool StartMinimized { get; set; } = false;
        public bool PlaySoundOnTranslation { get; set; } = true;
        public int TranslationsToday { get; set; } = 0;
        public DateTime LastTranslationDate { get; set; } = DateTime.MinValue;
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
                // Criar pasta de configuração se não existir
                if (!Directory.Exists(ConfigFolder))
                {
                    Directory.CreateDirectory(ConfigFolder);
                }
                
                // Verificar se o arquivo de configuração existe
                if (File.Exists(ConfigFile))
                {
                    // Ler e deserializar o arquivo
                    string json = File.ReadAllText(ConfigFile);
                    var settings = JsonSerializer.Deserialize<TranslationSettings>(json);
                    
                    // Retornar as configurações lidas
                    return settings ?? new TranslationSettings();
                }
                
                // Se o arquivo não existir, criar configurações padrão
                var defaultSettings = new TranslationSettings();
                SaveSettings(defaultSettings);
                return defaultSettings;
            }
            catch (Exception)
            {
                // Em caso de erro, retornar configurações padrão
                return new TranslationSettings();
            }
        }
        
        public static void SaveSettings(TranslationSettings settings)
        {
            try
            {
                // Criar pasta de configuração se não existir
                if (!Directory.Exists(ConfigFolder))
                {
                    Directory.CreateDirectory(ConfigFolder);
                }
                
                // Serializar e salvar configurações
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                
                string json = JsonSerializer.Serialize(settings, options);
                File.WriteAllText(ConfigFile, json);
            }
            catch (Exception)
            {
                // Silenciosamente falhar (poderia ter um log aqui)
            }
        }
        
        // Configurar inicialização com o Windows
        public static void SetStartWithWindows(bool enable)
        {
            try
            {
                // Obter caminho do executável
                string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                
                // Obter chave do registro
                var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                
                if (key != null)
                {
                    if (enable)
                    {
                        // Adicionar ao registro
                        key.SetValue("ClipboardTranslator", exePath);
                    }
                    else
                    {
                        // Remover do registro
                        key.DeleteValue("ClipboardTranslator", false);
                    }
                }
            }
            catch (Exception)
            {
                // Silenciosamente falhar (poderia ter um log aqui)
            }
        }
    }
}
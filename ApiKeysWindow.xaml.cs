using ClipboardTranslator;
using System.Windows;

namespace Translator
{
    public partial class ApiKeysWindow : Window
    {
        public TranslationSettings UpdatedSettings { get; private set; }

        public ApiKeysWindow(TranslationSettings currentSettings)
        {
            InitializeComponent();

            // Copiar configurações atuais
            UpdatedSettings = new TranslationSettings
            {
                DefaultSourceLanguage = currentSettings.DefaultSourceLanguage,
                DefaultTargetLanguage = currentSettings.DefaultTargetLanguage,
                DefaultTone = currentSettings.DefaultTone,
                GoogleApiKey = currentSettings.GoogleApiKey,
                StartWithWindows = currentSettings.StartWithWindows,
                StartMinimized = currentSettings.StartMinimized,
                PlaySoundOnTranslation = currentSettings.PlaySoundOnTranslation,
                TranslationsToday = currentSettings.TranslationsToday,
                LastTranslationDate = currentSettings.LastTranslationDate
            };

            // Configurar a interface com os valores atuais
            GoogleApiKeyTextBox.Text = UpdatedSettings.GoogleApiKey;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Atualizar as configurações com base na interface
            UpdatedSettings.GoogleApiKey = GoogleApiKeyTextBox.Text.Trim();

            // Definir resultado e fechar
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Fechar sem salvar
            DialogResult = false;
        }

        private void HelpLink_Click(object sender, RoutedEventArgs e)
        {
            // Abrir a página de ajuda no navegador
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://cloud.google.com/translate/docs/getting-started",
                UseShellExecute = true
            });
        }
    }
}
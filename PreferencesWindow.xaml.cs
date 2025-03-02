using ClipboardTranslator;
using System.Windows;

namespace Translator
{
    public partial class PreferencesWindow : Window
    {
        public TranslationSettings UpdatedSettings { get; private set; }

        public PreferencesWindow(TranslationSettings currentSettings)
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
            StartWithWindowsCheckBox.IsChecked = UpdatedSettings.StartWithWindows;
            StartMinimizedCheckBox.IsChecked = UpdatedSettings.StartMinimized;
            PlaySoundCheckBox.IsChecked = UpdatedSettings.PlaySoundOnTranslation;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Atualizar as configurações com base na interface
            UpdatedSettings.StartWithWindows = StartWithWindowsCheckBox.IsChecked ?? false;
            UpdatedSettings.StartMinimized = StartMinimizedCheckBox.IsChecked ?? false;
            UpdatedSettings.PlaySoundOnTranslation = PlaySoundCheckBox.IsChecked ?? true;

            // Configurar inicialização com o Windows
            ConfigManager.SetStartWithWindows(UpdatedSettings.StartWithWindows);

            // Definir resultado e fechar
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Fechar sem salvar
            DialogResult = false;
        }

        private void ResetStats_Click(object sender, RoutedEventArgs e)
        {
            // Reiniciar estatísticas
            UpdatedSettings.TranslationsToday = 0;
            UpdatedSettings.LastTranslationDate = System.DateTime.Now;

            MessageBox.Show("Estatísticas reiniciadas com sucesso!", "Estatísticas",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
using ClipboardTranslator;
using System.Windows;
using System.Windows.Input;

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
                GeminiApiKey = currentSettings.GeminiApiKey,
                OpenAIApiKey = currentSettings.OpenAIApiKey,
                PreferredService = currentSettings.PreferredService,
                StartWithWindows = currentSettings.StartWithWindows,
                StartMinimized = currentSettings.StartMinimized,
                PlaySoundOnTranslation = currentSettings.PlaySoundOnTranslation,
                TranslationsToday = currentSettings.TranslationsToday,
                LastTranslationDate = currentSettings.LastTranslationDate,
                MaxTokensLimit = currentSettings.MaxTokensLimit,
                EnableTokenLimit = currentSettings.EnableTokenLimit,
                MinimizeToTrayOnClose = currentSettings.MinimizeToTrayOnClose,
                ShowNotificationPopup = currentSettings.ShowNotificationPopup
            };

            // Configurar a interface com os valores atuais
            StartWithWindowsCheckBox.IsChecked = UpdatedSettings.StartWithWindows;
            StartMinimizedCheckBox.IsChecked = UpdatedSettings.StartMinimized;
            PlaySoundCheckBox.IsChecked = UpdatedSettings.PlaySoundOnTranslation;
            EnableTokenLimitCheckBox.IsChecked = UpdatedSettings.EnableTokenLimit;
            MaxTokensTextBox.Text = UpdatedSettings.MaxTokensLimit.ToString();
            MinimizeToTrayOnCloseCheckBox.IsChecked = UpdatedSettings.MinimizeToTrayOnClose;
            ShowNotificationPopupCheckBox.IsChecked = UpdatedSettings.ShowNotificationPopup;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Atualizar as configurações com base na interface
            UpdatedSettings.StartWithWindows = StartWithWindowsCheckBox.IsChecked ?? false;
            UpdatedSettings.StartMinimized = StartMinimizedCheckBox.IsChecked ?? false;
            UpdatedSettings.PlaySoundOnTranslation = PlaySoundCheckBox.IsChecked ?? false;
            UpdatedSettings.EnableTokenLimit = EnableTokenLimitCheckBox.IsChecked ?? true;
            UpdatedSettings.MinimizeToTrayOnClose = MinimizeToTrayOnCloseCheckBox.IsChecked ?? true;
            UpdatedSettings.ShowNotificationPopup = ShowNotificationPopupCheckBox.IsChecked ?? true;

            if (int.TryParse(MaxTokensTextBox.Text, out int maxTokens) && maxTokens > 0)
            {
                UpdatedSettings.MaxTokensLimit = maxTokens;
            }

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

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }
    }
}
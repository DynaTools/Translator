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

            // Copy current settings
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
                ShowNotificationPopup = false // Always disabled since we're removing this feature
            };

            // Configure interface with current values
            StartWithWindowsCheckBox.IsChecked = UpdatedSettings.StartWithWindows;
            StartMinimizedCheckBox.IsChecked = UpdatedSettings.StartMinimized;
            PlaySoundCheckBox.IsChecked = UpdatedSettings.PlaySoundOnTranslation;
            EnableTokenLimitCheckBox.IsChecked = UpdatedSettings.EnableTokenLimit;
            MaxTokensTextBox.Text = UpdatedSettings.MaxTokensLimit.ToString();
            MinimizeToTrayOnCloseCheckBox.IsChecked = UpdatedSettings.MinimizeToTrayOnClose;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Update settings based on interface
            UpdatedSettings.StartWithWindows = StartWithWindowsCheckBox.IsChecked ?? false;
            UpdatedSettings.StartMinimized = StartMinimizedCheckBox.IsChecked ?? false;
            UpdatedSettings.PlaySoundOnTranslation = PlaySoundCheckBox.IsChecked ?? false;
            UpdatedSettings.EnableTokenLimit = EnableTokenLimitCheckBox.IsChecked ?? true;
            UpdatedSettings.MinimizeToTrayOnClose = MinimizeToTrayOnCloseCheckBox.IsChecked ?? true;
            UpdatedSettings.ShowNotificationPopup = false; // Always disabled

            if (int.TryParse(MaxTokensTextBox.Text, out int maxTokens) && maxTokens > 0)
            {
                UpdatedSettings.MaxTokensLimit = maxTokens;
            }

            // Configure Windows startup
            ConfigManager.SetStartWithWindows(UpdatedSettings.StartWithWindows);

            // Set result and close
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Close without saving
            DialogResult = false;
        }

        private void ResetStats_Click(object sender, RoutedEventArgs e)
        {
            // Reset statistics
            UpdatedSettings.TranslationsToday = 0;
            UpdatedSettings.LastTranslationDate = System.DateTime.Now;

            MessageBox.Show("Statistics reset successfully!", "Statistics",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }
    }
}
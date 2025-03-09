using ClipboardTranslator;
using System.Windows;
using System.Threading.Tasks;
using System;

namespace Translator
{
    public partial class ApiKeysWindow : Window
    {
        public TranslationSettings UpdatedSettings { get; private set; }

        public ApiKeysWindow(TranslationSettings currentSettings)
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
                ShowNotificationPopup = currentSettings.ShowNotificationPopup
            };

            // Configure interface with current values
            GeminiApiKeyTextBox.Text = UpdatedSettings.GeminiApiKey;
            OpenAIApiKeyTextBox.Text = UpdatedSettings.OpenAIApiKey;

            // Configure preferred service
            if (UpdatedSettings.PreferredService == "OpenAI")
            {
                OpenAIRadioButton.IsChecked = true;
            }
            else
            {
                GeminiRadioButton.IsChecked = true;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Update settings based on interface
            UpdatedSettings.GeminiApiKey = GeminiApiKeyTextBox.Text.Trim();
            UpdatedSettings.OpenAIApiKey = OpenAIApiKeyTextBox.Text.Trim();

            // Determine preferred service
            UpdatedSettings.PreferredService = OpenAIRadioButton.IsChecked == true ? "OpenAI" : "Gemini";

            // Set result and close
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Close without saving
            DialogResult = false;
        }

        private void GeminiHelpLink_Click(object sender, RoutedEventArgs e)
        {
            // Open help page in browser with updated documentation URL
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://ai.google.dev/tutorials/ai-studio_quickstart",
                UseShellExecute = true
            });
        }

        private void OpenAIHelpLink_Click(object sender, RoutedEventArgs e)
        {
            // Open help page in browser
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://platform.openai.com/api-keys",
                UseShellExecute = true
            });
        }

        private async void TestGeminiButton_Click(object sender, RoutedEventArgs e)
        {
            string apiKey = GeminiApiKeyTextBox.Text.Trim();

            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show("Please enter a valid API key.", "Invalid Key",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TestGeminiButton.IsEnabled = false;
            TestGeminiButton.Content = "Testing...";

            try
            {
                // Create temporary service instance with updated implementation
                var testService = new GeminiTranslationService();
                testService.SetApiKey(apiKey);

                // Test a simple translation
                var result = await testService.TranslateAsync(
                    "Hello, this is a connection test.",
                    "en",
                    "es",
                    "neutral");

                if (result.Success)
                {
                    MessageBox.Show(
                        $"Connection successful!\nTranslated text: {result.TranslatedText}",
                        "Connection Test",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"Error testing connection: {result.ErrorMessage}",
                        "Test Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error testing connection: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                TestGeminiButton.IsEnabled = true;
                TestGeminiButton.Content = "Test Connection";
            }
        }

        private async void TestOpenAIButton_Click(object sender, RoutedEventArgs e)
        {
            string apiKey = OpenAIApiKeyTextBox.Text.Trim();

            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show("Please enter a valid API key.", "Invalid Key",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TestOpenAIButton.IsEnabled = false;
            TestOpenAIButton.Content = "Testing...";

            try
            {
                // Create temporary service instance
                var testService = new OpenAITranslationService();
                testService.SetApiKey(apiKey);

                // Test a simple translation
                var result = await testService.TranslateAsync(
                    "Hello, this is a connection test.",
                    "en",
                    "fr",
                    "neutral");

                if (result.Success)
                {
                    MessageBox.Show(
                        $"Connection successful!\nTranslated text: {result.TranslatedText}",
                        "Connection Test",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"Error testing connection: {result.ErrorMessage}",
                        "Test Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error testing connection: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                TestOpenAIButton.IsEnabled = true;
                TestOpenAIButton.Content = "Test Connection";
            }
        }
    }
}
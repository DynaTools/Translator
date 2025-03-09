using System;
using System.Windows;
using System.Windows.Controls;
using ClipboardTranslator;
using WpfMessageBox = System.Windows.MessageBox;
using WpfTabControl = System.Windows.Controls.TabControl;
using System.Threading.Tasks;

namespace Translator
{
    // Partial class for handling AI parameter-related methods
    public partial class MainWindow
    {
        public void AIParameter_Changed(object sender, RoutedEventArgs e)
        {
            // This gets called when any AI parameter is changed
            // We'll use this to update the settings in real-time

            // Ensure aiParameters is initialized
            if (aiParameters == null)
            {
                aiParameters = new AIParameters();
            }

            // Update the AI parameters - add null checks
            if (TemperatureSlider != null)
            {
                aiParameters.Temperature = TemperatureSlider.Value;
            }

            if (TopPSlider != null)
            {
                aiParameters.TopP = TopPSlider.Value;
            }

            if (FrequencyPenaltySlider != null)
            {
                aiParameters.FrequencyPenalty = FrequencyPenaltySlider.Value;
            }

            if (PresencePenaltySlider != null)
            {
                aiParameters.PresencePenalty = PresencePenaltySlider.Value;
            }

            if (EnableModelVersionSelector != null && ModelVersionSelector != null &&
                EnableModelVersionSelector.IsChecked == true && ModelVersionSelector.SelectedItem != null)
            {
                aiParameters.ModelVersion = ((ComboBoxItem)ModelVersionSelector.SelectedItem).Content.ToString();
            }
            else
            {
                aiParameters.ModelVersion = "Default";
            }

            // Update the translation service
            if (translationService != null)
            {
                translationService.SetAIParameters(aiParameters);
            }

            // Save the settings
            if (settings != null)
            {
                settings.AIParameters = aiParameters;
                ConfigManager.SaveSettings(settings);
            }

            // Clear the translation cache since parameters changed
            if (translationCache != null)
            {
                translationCache.Clear();
            }
        }

        public void ResetAIParameters_Click(object sender, RoutedEventArgs e)
        {
            // Reset to default values
            TemperatureSlider.Value = 0.7;
            TopPSlider.Value = 0.95;
            FrequencyPenaltySlider.Value = 0;
            PresencePenaltySlider.Value = 0;
            EnableModelVersionSelector.IsChecked = false;
            ModelVersionSelector.SelectedIndex = 0;

            // This will trigger the AIParameter_Changed event
            DebugLog("AI parameters reset to defaults");
        }

        public async void TestAIParameters_Click(object sender, RoutedEventArgs e)
        {
            // Test the current AI parameters with a sample translation
            if (string.IsNullOrWhiteSpace(lastCopiedText))
            {
                WpfMessageBox.Show(
                    "No text available to test. Copy text to the clipboard first.",
                    "No Text",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            try
            {
                // Make sure translation service has latest AI parameters
                translationService.SetAIParameters(new AIParameters
                {
                    Temperature = TemperatureSlider.Value,
                    TopP = TopPSlider.Value,
                    FrequencyPenalty = FrequencyPenaltySlider.Value,
                    PresencePenalty = PresencePenaltySlider.Value,
                    ModelVersion = EnableModelVersionSelector.IsChecked == true
                        ? ((ComboBoxItem)ModelVersionSelector.SelectedItem)?.Content.ToString() ?? "Default"
                        : "Default"
                });

                // Get the source and target languages
                string sourceLanguage = ((ComboBoxItem)SourceLanguage.SelectedItem).Tag as string ?? "auto";
                string targetLanguage = ((ComboBoxItem)TargetLanguage.SelectedItem).Tag as string ?? "en";
                string tone = ((ComboBoxItem)TranslationTone.SelectedItem).Content.ToString().ToLower();

                // Show a progress dialog
                StatusBarText.Text = "Testing AI parameters...";

                // Perform the translation
                var translationResult = await translationService.TranslateAsync(
                    lastCopiedText, sourceLanguage, targetLanguage, tone);

                if (translationResult.Success)
                {
                    // Update the translation preview
                    TranslationPreview.Text = translationResult.TranslatedText;

                    // Switch to the Translation Preview tab
                    var tabControl = (WpfTabControl)TabControlMain;
                    tabControl.SelectedIndex = 0;

                    // Update status
                    StatusBarText.Text = "AI parameters test successful";

                    // Show a dialog with the results
                    WpfMessageBox.Show(
                        "AI parameters test successful. The translation is displayed in the preview tab.",
                        "Test Successful",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    // Show an error message
                    WpfMessageBox.Show(
                        $"Error testing AI parameters: {translationResult.ErrorMessage}",
                        "Test Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    StatusBarText.Text = "AI parameters test failed";
                }
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show(
                    $"Error testing AI parameters: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusBarText.Text = "AI parameters test error";
            }
        }
    }
}
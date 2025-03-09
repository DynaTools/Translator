using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms; // Reference to System.Windows.Forms
using System.Drawing; // Reference to System.Drawing
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Text;
using ClipboardTranslator;
using System.Threading;
using System.Linq;
using WpfComboBox = System.Windows.Controls.ComboBox;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;
using WpfTabControl = System.Windows.Controls.TabControl;
using WpfMessageBox = System.Windows.MessageBox;
using System.Security.Cryptography;
using Translator.Properties;

namespace Translator
{
    public partial class MainWindow : Window
    {
        // All the code stays the same until line 755
        // At line 755, replace MessageBox with WpfMessageBox

        private async void ShowAllTones_Click(object sender, RoutedEventArgs e)
        {
            // Make sure we have text to translate
            if (string.IsNullOrWhiteSpace(lastCopiedText))
            {
                WpfMessageBox.Show(
                    "No text available to translate. Copy text to the clipboard first.",
                    "No Text",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // Clear existing tone panels
            AllTonesPanel.Children.Clear();

            // Get the source and target languages
            string sourceLanguage = "auto";
            string targetLanguage = "en";

            if (SourceLanguage.SelectedItem != null)
            {
                sourceLanguage = ((ComboBoxItem)SourceLanguage.SelectedItem).Tag as string ?? "auto";
            }

            if (TargetLanguage.SelectedItem != null)
            {
                targetLanguage = ((ComboBoxItem)TargetLanguage.SelectedItem).Tag as string ?? "en";
            }

            // Get all available tones from the translation tone combo box
            List<string> tones = new List<string>();
            foreach (ComboBoxItem item in TranslationTone.Items)
            {
                tones.Add(item.Content.ToString().ToLower());
            }

            // Create a progress indicator
            TextBlock progressText = new TextBlock
            {
                Text = "Generating translations for all tones...",
                Margin = new Thickness(0, 10, 0, 10),
                FontWeight = FontWeights.Bold
            };
            AllTonesPanel.Children.Add(progressText);

            // Translate the text with each tone
            foreach (string tone in tones)
            {
                // Create a border for each tone
                Border toneBorder = new Border
                {
                    BorderBrush = new SolidColorBrush(Colors.LightGray),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 5, 0, 5),
                    Padding = new Thickness(10),
                    Background = new SolidColorBrush(Colors.WhiteSmoke)
                };

                // Create a panel for the tone's content
                StackPanel tonePanel = new StackPanel();

                // Add a header with the tone name
                TextBlock header = new TextBlock
                {
                    Text = char.ToUpper(tone[0]) + tone.Substring(1) + " Tone",
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 5)
                };
                tonePanel.Children.Add(header);

                // Add a waiting message
                TextBlock translationText = new TextBlock
                {
                    Text = "Translating...",
                    TextWrapping = TextWrapping.Wrap
                };
                tonePanel.Children.Add(translationText);

                // Add a button to copy this translation
                WpfButton copyButton = new WpfButton
                {
                    Content = "Use This Translation",
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    Margin = new Thickness(0, 5, 0, 0),
                    Padding = new Thickness(8, 3, 8, 3),
                    Tag = translationText  // Store reference to the text block
                };
                copyButton.Click += CopyToneTranslation_Click;
                tonePanel.Children.Add(copyButton);

                // Add the tone panel to the border
                toneBorder.Child = tonePanel;

                // Add the border to the main panel
                AllTonesPanel.Children.Add(toneBorder);

                // Perform the translation asynchronously
                translationText.Text = "Translating...";

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

                // Start the translation task
                var translationTask = Task.Run(async () =>
                {
                    try
                    {
                        var translationResult = await translationService.TranslateAsync(
                            lastCopiedText, sourceLanguage, targetLanguage, tone);

                        return translationResult;
                    }
                    catch (Exception ex)
                    {
                        return new TranslationResult
                        {
                            Success = false,
                            ErrorMessage = ex.Message
                        };
                    }
                });

                // Handle the result
                var toneResult = await translationTask;

                // Update the UI with the translation result
                await Dispatcher.InvokeAsync(() =>
                {
                    if (toneResult.Success)
                    {
                        translationText.Text = toneResult.TranslatedText;
                        copyButton.IsEnabled = true;
                    }
                    else
                    {
                        translationText.Text = $"Error: {toneResult.ErrorMessage}";
                        translationText.Foreground = new SolidColorBrush(Colors.Red);
                        copyButton.IsEnabled = false;
                    }
                });
            }

            // Remove the progress indicator
            AllTonesPanel.Children.Remove(progressText);

            // Switch to the All Tones tab
            var tabControl = (WpfTabControl)((Grid)AllTonesPanel.Parent).Parent;
            tabControl.SelectedIndex = 1;
        }

        private void CopyToneTranslation_Click(object sender, RoutedEventArgs e)
        {
            WpfButton button = (WpfButton)sender;
            TextBlock textBlock = (TextBlock)button.Tag;

            // Copy the translation to the clipboard
            System.Windows.Clipboard.SetText(textBlock.Text);

            // Also update the translation preview
            TranslationPreview.Text = textBlock.Text;

            // Switch to the Translation Preview tab
            var tabControl = (WpfTabControl)((Grid)AllTonesPanel.Parent).Parent;
            tabControl.SelectedIndex = 0;

            // Update status
            DebugLog("Translation copied from tone comparison");
        }

        private void ApplyEdits_Click(object sender, RoutedEventArgs e)
        {
            // Copy the edited text to the clipboard
            string editedText = TranslationPreview.Text;
            if (!string.IsNullOrEmpty(editedText))
            {
                System.Windows.Clipboard.SetText(editedText);
                DebugLog("Edited text copied to clipboard");
            }
        }

        private void AIParameter_Changed(object sender, RoutedEventArgs e)
        {
            // This gets called when any AI parameter is changed
            // We'll use this to update the settings in real-time

            // Update the AI parameters
            aiParameters.Temperature = TemperatureSlider.Value;
            aiParameters.TopP = TopPSlider.Value;
            aiParameters.FrequencyPenalty = FrequencyPenaltySlider.Value;
            aiParameters.PresencePenalty = PresencePenaltySlider.Value;

            if (EnableModelVersionSelector.IsChecked == true && ModelVersionSelector.SelectedItem != null)
            {
                aiParameters.ModelVersion = ((ComboBoxItem)ModelVersionSelector.SelectedItem).Content.ToString();
            }
            else
            {
                aiParameters.ModelVersion = "Default";
            }

            // Update the translation service
            translationService.SetAIParameters(aiParameters);

            // Save the settings
            if (settings != null)
            {
                settings.AIParameters = aiParameters;
                ConfigManager.SaveSettings(settings);
            }

            // Clear the translation cache since parameters changed
            translationCache.Clear();
        }

        private void ResetAIParameters_Click(object sender, RoutedEventArgs e)
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

        private async void TestAIParameters_Click(object sender, RoutedEventArgs e)
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
                var result = await translationService.TranslateAsync(
                    lastCopiedText, sourceLanguage, targetLanguage, tone);

                if (result.Success)
                {
                    // Update the translation preview
                    TranslationPreview.Text = result.TranslatedText;

                    // Switch to the Translation Preview tab
                    var tabControl = (WpfTabControl)((Grid)AllTonesPanel.Parent).Parent;
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
                        $"Error testing AI parameters: {result.ErrorMessage}",
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
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClipboardTranslator;
using WpfButton = System.Windows.Controls.Button;
using WpfTabControl = System.Windows.Controls.TabControl;
using WpfMessageBox = System.Windows.MessageBox;

namespace Translator
{
    // Partial class for handling tone comparison functionality
    public partial class MainWindow
    {
        public async void ShowAllTones_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Debug logs to track execution
                DebugLog("ShowAllTones_Click iniciado");

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
                DebugLog("AllTonesPanel limpo");

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

                DebugLog($"Idiomas selecionados: fonte={sourceLanguage}, alvo={targetLanguage}");

                // Get all available tones from the translation tone combo box
                List<string> tones = new List<string>();
                foreach (ComboBoxItem item in TranslationTone.Items)
                {
                    tones.Add(item.Content.ToString().ToLower());
                }

                DebugLog($"Tons encontrados: {string.Join(", ", tones)}");

                // Create a progress indicator
                TextBlock progressText = new TextBlock
                {
                    Text = "Generating translations for all tones...",
                    Margin = new Thickness(0, 10, 0, 10),
                    FontWeight = FontWeights.Bold
                };
                AllTonesPanel.Children.Add(progressText);
                DebugLog("Indicador de progresso adicionado");

                // First ensure we can find and switch to the AllTones tab
                WpfTabControl tabControl = TabControlMain;
                if (tabControl == null)
                {
                    DebugLog("ERRO: Não foi possível encontrar o TabControl pai");
                    WpfMessageBox.Show(
                        "Error finding the tab control. Please report this issue.",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                // Switch to the All Tones tab immediately so user sees something happening
                DebugLog("Mudando para a aba de Todos os Tons (índice 1)");
                tabControl.SelectedIndex = 1;

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
                    DebugLog($"Painel para o tom '{tone}' adicionado");

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
                    DebugLog($"Iniciando tradução para o tom '{tone}'");
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
                            DebugLog($"Erro na tradução para o tom '{tone}': {ex.Message}");
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
                            DebugLog($"Tradução para o tom '{tone}' concluída com sucesso");
                        }
                        else
                        {
                            translationText.Text = $"Error: {toneResult.ErrorMessage}";
                            translationText.Foreground = new SolidColorBrush(Colors.Red);
                            copyButton.IsEnabled = false;
                            DebugLog($"Erro na tradução para o tom '{tone}': {toneResult.ErrorMessage}");
                        }
                    });
                }

                // Remove the progress indicator
                AllTonesPanel.Children.Remove(progressText);
                DebugLog("Indicador de progresso removido");
            }
            catch (Exception ex)
            {
                DebugLog($"Erro na função ShowAllTones_Click: {ex.Message}");
                DebugLog($"Stack trace: {ex.StackTrace}");

                // Clear the panel and show error
                AllTonesPanel.Children.Clear();

                TextBlock errorText = new TextBlock
                {
                    Text = $"Error generating translations: {ex.Message}",
                    Foreground = new SolidColorBrush(Colors.Red),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 10, 0, 10)
                };
                AllTonesPanel.Children.Add(errorText);

                // Try to switch to the All Tones tab to show the error
                try
                {
                    TabControlMain.SelectedIndex = 1;
                }
                catch
                {
                    // Ignore errors in tab switching after an exception
                }
            }
        }

        private void CopyToneTranslation_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                WpfButton button = (WpfButton)sender;
                TextBlock textBlock = (TextBlock)button.Tag;

                // Copy the translation to the clipboard
                System.Windows.Clipboard.SetText(textBlock.Text);

                // Also update the translation preview
                TranslationPreview.Text = textBlock.Text;

                // Switch to the Translation Preview tab
                TabControlMain.SelectedIndex = 0;

                // Update status
                DebugLog("Translation copied from tone comparison");

                // Show confirmation in status bar
                StatusBarText.Text = "Translation copied to clipboard";
            }
            catch (Exception ex)
            {
                DebugLog($"Erro ao copiar tradução: {ex.Message}");
            }
        }
    }
}
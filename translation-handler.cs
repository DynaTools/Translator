using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading.Tasks;
using ClipboardTranslator;
using WpfMessageBox = System.Windows.MessageBox;

namespace Translator
{
    // Partial class for handling translation functionality
    public partial class MainWindow
    {
        private async void OnClipboardChangedHandler()
        {
            // Check if monitoring is enabled
            if (!isMonitoringEnabled)
                return;

            try
            {
                // Get text from clipboard
                string clipboardText = "";

                await Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        // Try to get text from clipboard
                        if (System.Windows.Clipboard.ContainsText())
                        {
                            clipboardText = System.Windows.Clipboard.GetText();
                        }
                    }
                    catch (Exception ex)
                    {
                        DebugLog($"Error getting clipboard text: {ex.Message}");
                    }
                });

                // Check if we have text and it's different from the last one
                if (!string.IsNullOrWhiteSpace(clipboardText) && clipboardText != lastCopiedText)
                {
                    lastCopiedText = clipboardText;

                    // Check if token limit is enabled and exceeded
                    if (settings.EnableTokenLimit)
                    {
                        int estimatedTokens = TextTokenizer.EstimateTokenCount(clipboardText);

                        if (estimatedTokens > settings.MaxTokensLimit)
                        {
                            // Token limit exceeded, show notification
                            await Dispatcher.InvokeAsync(() =>
                            {
                                StatusBarText.Text = $"Text too long ({estimatedTokens} tokens > {settings.MaxTokensLimit} limit)";
                            });

                            NotificationService.ShowWarning(
                                "Token limit exceeded",
                                $"The copied text is too long ({estimatedTokens} tokens). The configured limit is {settings.MaxTokensLimit} tokens."
                            );

                            return;
                        }
                    }

                    // Translate the text
                    await TranslateTextAsync(clipboardText);
                }
            }
            catch (Exception ex)
            {
                DebugLog($"Error processing clipboard change: {ex.Message}");
            }
        }

        internal async Task TranslateTextAsync(string text)
        {
            try
            {
                // Get the selected languages and tone
                string sourceLanguage = "auto";
                string targetLanguage = "en";
                string tone = "neutral";

                await Dispatcher.InvokeAsync(() =>
                {
                    // Get source language
                    if (SourceLanguage.SelectedItem != null)
                    {
                        sourceLanguage = ((ComboBoxItem)SourceLanguage.SelectedItem).Tag as string ?? "auto";
                    }

                    // Get target language
                    if (TargetLanguage.SelectedItem != null)
                    {
                        targetLanguage = ((ComboBoxItem)TargetLanguage.SelectedItem).Tag as string ?? "en";
                    }

                    // Get tone
                    if (TranslationTone.SelectedItem != null)
                    {
                        tone = ((ComboBoxItem)TranslationTone.SelectedItem).Content.ToString().ToLower();
                    }

                    // Update status
                    StatusBarText.Text = "Translating...";
                });

                // Generate cache key
                string cacheKey = $"{text}_{sourceLanguage}_{targetLanguage}_{tone}";

                // Check if translation is in cache
                if (translationCache.ContainsKey(cacheKey))
                {
                    var cachedResult = translationCache[cacheKey];

                    // Apply the cached translation
                    await ApplyTranslationResultAsync(cachedResult);

                    await Dispatcher.InvokeAsync(() =>
                    {
                        StatusBarText.Text = "Translation applied from cache";
                    });

                    return;
                }

                // Perform translation
                var translationResult = await translationService.TranslateAsync(text, sourceLanguage, targetLanguage, tone);

                if (translationResult.Success)
                {
                    // Cache the result
                    translationCache[cacheKey] = translationResult;

                    // Apply the translation
                    await ApplyTranslationResultAsync(translationResult);
                }
                else
                {
                    // Show error
                    await Dispatcher.InvokeAsync(() =>
                    {
                        StatusBarText.Text = $"Translation error: {translationResult.ErrorMessage}";
                        WpfMessageBox.Show(
                            $"Translation Error: {translationResult.ErrorMessage}",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    });

                    // Show notification
                    NotificationService.ShowError(
                        "Translation Error",
                        translationResult.ErrorMessage
                    );
                }
            }
            catch (Exception ex)
            {
                DebugLog($"Error translating text: {ex.Message}");

                await Dispatcher.InvokeAsync(() =>
                {
                    StatusBarText.Text = $"Translation error: {ex.Message}";
                });
            }
        }

        internal async Task ApplyTranslationResultAsync(TranslationResult result)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    // Update preview
                    TranslationPreview.Text = result.TranslatedText;

                    // Copy to clipboard
                    System.Windows.Clipboard.SetText(result.TranslatedText);

                    // Update statistics
                    settings.TranslationsToday++;
                    settings.LastTranslationDate = DateTime.Now;
                    lastDetectedLanguage = result.DetectedLanguage ?? "";

                    // Update UI
                    UpdateStatistics();

                    // Save settings
                    ConfigManager.SaveSettings(settings);

                    // Update status
                    StatusBarText.Text = "Translation completed";

                    // Play sound if enabled
                    if (settings.PlaySoundOnTranslation)
                    {
                        SoundService.PlayTranslationCompleteSound();
                    }

                    // Note: Popup notification functionality has been removed
                    // The ShowTranslationCompleted method call is kept but the method is now a no-op
                }
                catch (Exception ex)
                {
                    DebugLog($"Error applying translation: {ex.Message}");
                }
            });
        }
    }
}
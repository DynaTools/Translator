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
using System.Security.Cryptography;

namespace Translator
{
    public partial class MainWindow : Window
    {
        private NotifyIcon trayIcon;
        private bool isActive = true;

        // Dictionary to map original text and target language to avoid repetitions
        private Dictionary<string, string> translationCache = new Dictionary<string, string>();

        private Dictionary<string, string> languageCodes = new Dictionary<string, string>();
        private ClipboardMonitor clipboardMonitor;
        private ITranslationService translationService;
        private int translationsToday = 0;
        private DateTime lastTranslationDate = DateTime.MinValue;
        private TranslationSettings settings = new TranslationSettings();

        // Semaphore to prevent simultaneous translations
        private SemaphoreSlim translationSemaphore = new SemaphoreSlim(1, 1);

        // Store the last copied text for debug
        private string lastCopiedText = string.Empty;

        // Control of last translation
        private string lastSourceLanguage = string.Empty;
        private string lastTargetLanguage = string.Empty;

        // AI Parameters
        private AIParameters aiParameters = new AIParameters
        {
            Temperature = 0.7,
            TopP = 0.95,
            FrequencyPenalty = 0,
            PresencePenalty = 0,
            ModelVersion = "Default"
        };

        public MainWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();
            NotificationService.Initialize(trayIcon);
            InitializeLanguageCodes();
            SetupLanguageCombos();
            LoadSettings();
            InitializeTranslationService();
            InitializeAIParameters();

            // Start clipboard monitoring
            clipboardMonitor = new ClipboardMonitor(this);
            clipboardMonitor.ClipboardChanged += OnClipboardChanged;

            // Configure events for when the window is loaded and closed
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        private void InitializeAIParameters()
        {
            // Set the initial values for AI parameter controls based on saved settings
            TemperatureSlider.Value = settings.AIParameters?.Temperature ?? 0.7;
            TopPSlider.Value = settings.AIParameters?.TopP ?? 0.95;
            FrequencyPenaltySlider.Value = settings.AIParameters?.FrequencyPenalty ?? 0;
            PresencePenaltySlider.Value = settings.AIParameters?.PresencePenalty ?? 0;

            EnableModelVersionSelector.IsChecked = !string.IsNullOrEmpty(settings.AIParameters?.ModelVersion) &&
                                                   settings.AIParameters?.ModelVersion != "Default";

            // Set the model version selector
            if (!string.IsNullOrEmpty(settings.AIParameters?.ModelVersion))
            {
                foreach (ComboBoxItem item in ModelVersionSelector.Items)
                {
                    if (item.Content.ToString() == settings.AIParameters.ModelVersion)
                    {
                        ModelVersionSelector.SelectedItem = item;
                        break;
                    }
                }
            }
            else
            {
                ModelVersionSelector.SelectedIndex = 0; // Default (Latest)
            }
        }

        private void SaveAllSettings()
        {
            // Save current settings
            if (settings != null)
            {
                if (SourceLanguage?.SelectedItem != null)
                {
                    settings.DefaultSourceLanguage = ((ComboBoxItem)SourceLanguage.SelectedItem).Content.ToString();
                }

                if (TargetLanguage?.SelectedItem != null)
                {
                    settings.DefaultTargetLanguage = ((ComboBoxItem)TargetLanguage.SelectedItem).Content.ToString();
                }

                if (TranslationTone?.SelectedItem != null)
                {
                    settings.DefaultTone = ((ComboBoxItem)TranslationTone.SelectedItem).Content.ToString();
                }

                // Save AI parameters
                settings.AIParameters = new AIParameters
                {
                    Temperature = TemperatureSlider.Value,
                    TopP = TopPSlider.Value,
                    FrequencyPenalty = FrequencyPenaltySlider.Value,
                    PresencePenalty = PresencePenaltySlider.Value,
                    ModelVersion = EnableModelVersionSelector.IsChecked == true
                        ? ((ComboBoxItem)ModelVersionSelector.SelectedItem)?.Content.ToString() ?? "Default"
                        : "Default"
                };

                ConfigManager.SaveSettings(settings);
                DebugLog("All settings saved");
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            clipboardMonitor.Initialize(hwnd);
            UpdateTranslationCounter();

            // If should start minimized
            if (settings.StartMinimized)
            {
                this.Hide();
                trayIcon.Visible = true;
            }

            // Update interface with settings
            StatusBarText.Text = "Ready to translate";
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // If the setting is enabled, minimize to the tray instead of closing
            if (settings.MinimizeToTrayOnClose)
            {
                e.Cancel = true;
                this.Hide();
                trayIcon.Visible = true;
            }
            else
            {
                // If we should close, remove tray icon
                if (trayIcon != null)
                {
                    trayIcon.Visible = false;
                    trayIcon.Dispose();
                }

                // Disable clipboard monitoring
                clipboardMonitor.Dispose();
            }
        }

        private void InitializeTrayIcon()
        {
            trayIcon = new NotifyIcon
            {
                Icon = new Icon(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "translate_icon.ico")),
                Visible = false,
                Text = "Clipboard Translator"
            };

            trayIcon.Click += TrayIcon_Click;

            // Create menu for tray icon
            ContextMenuStrip contextMenu = new ContextMenuStrip();

            ToolStripMenuItem openItem = new ToolStripMenuItem("Open");
            openItem.Click += (s, e) => ShowMainWindow();

            ToolStripMenuItem toggleItem = new ToolStripMenuItem("Pause/Resume");
            toggleItem.Click += (s, e) => ToggleTranslationStatus();

            ToolStripMenuItem exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => System.Windows.Application.Current.Shutdown();

            contextMenu.Items.Add(openItem);
            contextMenu.Items.Add(toggleItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            trayIcon.ContextMenuStrip = contextMenu;
        }

        private void TrayIcon_Click(object sender, EventArgs e)
        {
            // If the click is with the left button, show the window
            // Fully qualify MouseEventArgs to avoid ambiguity
            if (e is System.Windows.Forms.MouseEventArgs mouseEvent && mouseEvent.Button == System.Windows.Forms.MouseButtons.Left)
            {
                ShowMainWindow();
            }
        }

        private void ShowMainWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void InitializeLanguageCodes()
        {
            languageCodes.Clear();
            languageCodes.Add("Auto Detect", "auto");
            languageCodes.Add("Portuguese", "pt");
            languageCodes.Add("English", "en");
            languageCodes.Add("Italian", "it");
            languageCodes.Add("Spanish", "es");
            languageCodes.Add("French", "fr");
            languageCodes.Add("German", "de");
        }

        private void SetupLanguageCombos()
        {
            // Clear existing items
            SourceLanguage.Items.Clear();
            TargetLanguage.Items.Clear();

            // Source languages (with Auto Detect)
            SourceLanguage.Items.Add(new ComboBoxItem { Content = "Auto Detect", Tag = "auto" });
            SourceLanguage.Items.Add(new ComboBoxItem { Content = "Portuguese", Tag = "pt" });
            SourceLanguage.Items.Add(new ComboBoxItem { Content = "English", Tag = "en" });
            SourceLanguage.Items.Add(new ComboBoxItem { Content = "Italian", Tag = "it" });
            SourceLanguage.Items.Add(new ComboBoxItem { Content = "Spanish", Tag = "es" });
            SourceLanguage.Items.Add(new ComboBoxItem { Content = "French", Tag = "fr" });
            SourceLanguage.Items.Add(new ComboBoxItem { Content = "German", Tag = "de" });

            // Target languages (without Auto Detect)
            TargetLanguage.Items.Add(new ComboBoxItem { Content = "Portuguese", Tag = "pt" });
            TargetLanguage.Items.Add(new ComboBoxItem { Content = "English", Tag = "en" });
            TargetLanguage.Items.Add(new ComboBoxItem { Content = "Italian", Tag = "it" });
            TargetLanguage.Items.Add(new ComboBoxItem { Content = "Spanish", Tag = "es" });
            TargetLanguage.Items.Add(new ComboBoxItem { Content = "French", Tag = "fr" });
            TargetLanguage.Items.Add(new ComboBoxItem { Content = "German", Tag = "de" });
        }

        private void InitializeTranslationService()
        {
            // Load settings
            settings = ConfigManager.LoadSettings();

            // Update AI parameters with loaded settings if they exist
            if (settings.AIParameters != null)
            {
                aiParameters = settings.AIParameters;
            }

            // Select service based on configuration
            if (settings.PreferredService == "OpenAI" && !string.IsNullOrEmpty(settings.OpenAIApiKey))
            {
                translationService = new OpenAITranslationService();
                translationService.SetApiKey(settings.OpenAIApiKey);
                translationService.SetAIParameters(aiParameters);
                StatusBarText.Text = "Using service: OpenAI";
            }
            else if (settings.PreferredService == "Gemini" && !string.IsNullOrEmpty(settings.GeminiApiKey))
            {
                translationService = new GeminiTranslationService();
                translationService.SetApiKey(settings.GeminiApiKey);
                translationService.SetAIParameters(aiParameters);
                StatusBarText.Text = "Using service: Google Gemini Flash";
            }
            else
            {
                // Default fallback to free service
                translationService = new GeminiTranslationService();
                StatusBarText.Text = "Using service: Free translation API (limited)";
            }
        }

        private void LoadSettings()
        {
            settings = ConfigManager.LoadSettings();

            // Apply saved source language
            if (!string.IsNullOrEmpty(settings.DefaultSourceLanguage))
            {
                foreach (ComboBoxItem item in SourceLanguage.Items)
                {
                    if (item.Content.ToString() == settings.DefaultSourceLanguage)
                    {
                        SourceLanguage.SelectedItem = item;
                        break;
                    }
                }
            }

            // Apply saved target language
            if (!string.IsNullOrEmpty(settings.DefaultTargetLanguage))
            {
                foreach (ComboBoxItem item in TargetLanguage.Items)
                {
                    if (item.Content.ToString() == settings.DefaultTargetLanguage)
                    {
                        TargetLanguage.SelectedItem = item;
                        break;
                    }
                }
            }

            // Apply saved tone
            if (!string.IsNullOrEmpty(settings.DefaultTone))
            {
                foreach (ComboBoxItem item in TranslationTone.Items)
                {
                    if (item.Content.ToString() == settings.DefaultTone)
                    {
                        TranslationTone.SelectedItem = item;
                        break;
                    }
                }
            }

            // Check saved statistics
            if (settings.LastTranslationDate.Date == DateTime.Today)
            {
                translationsToday = settings.TranslationsToday;
            }
            else
            {
                settings.TranslationsToday = 0;
                settings.LastTranslationDate = DateTime.Today;
                ConfigManager.SaveSettings(settings);
            }

            UpdateTranslationCounter();
        }

        private void DebugLog(string message)
        {
            if (StatusBarText != null)
            {
                StatusBarText.Text = message;
            }
            else
            {
                // Handle the case where StatusBarText is null
                System.Diagnostics.Debug.WriteLine("StatusBarText is null.");
            }
        }

        private void UpdateTranslationCounter()
        {
            TranslationCount.Text = translationsToday.ToString();
        }

        private async void OnClipboardChanged()
        {
            if (!isActive) return;

            try
            {
                // Try to obtain the translation semaphore with a short timeout
                // If can't obtain it, another translation is in progress
                if (!await translationSemaphore.WaitAsync(100))
                {
                    StatusBarText.Text = "Translation already in progress, please wait...";
                    return;
                }

                try
                {
                    // Check if there is text in the clipboard
                    if (System.Windows.Clipboard.ContainsText())
                    {
                        string clipboardText = System.Windows.Clipboard.GetText().Trim();
                        lastCopiedText = clipboardText;

                        // Ignore if the text is too short
                        if (string.IsNullOrWhiteSpace(clipboardText) || clipboardText.Length < 2)
                        {
                            DebugLog("Text too short for translation");
                            return;
                        }

                        // Check token limit (moved after getting the text)
                        if (settings.EnableTokenLimit)
                        {
                            // Use the token estimator method from the helper class
                            int estimatedTokens = TextTokenizer.EstimateTokenCount(clipboardText);
                            if (estimatedTokens > settings.MaxTokensLimit)
                            {
                                string message = $"The copied text exceeds the configured token limit ({settings.MaxTokensLimit}).\n" +
                                                 $"Estimated tokens: {estimatedTokens}.\n\n" +
                                                 "Do you want to continue with the translation?";

                                var dialogResult = System.Windows.MessageBox.Show(message, "Token Limit Warning",
                                   System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);

                                if (dialogResult == System.Windows.MessageBoxResult.No)
                                {
                                    DebugLog("Translation canceled by user (token limit)");
                                    return;
                                }
                            }
                        }

                        // Get source language code directly from Tag
                        string sourceLanguage = "auto";
                        if (SourceLanguage.SelectedItem != null)
                        {
                            var selectedItem = (ComboBoxItem)SourceLanguage.SelectedItem;
                            sourceLanguage = selectedItem.Tag as string ?? "auto";
                            DebugLog($"Source language: {selectedItem.Content} (code: {sourceLanguage})");
                        }

                        // Get target language code directly from Tag
                        string targetLanguage = "en";
                        if (TargetLanguage.SelectedItem != null)
                        {
                            var selectedItem = (ComboBoxItem)TargetLanguage.SelectedItem;
                            targetLanguage = selectedItem.Tag as string ?? "en";
                            DebugLog($"Target language: {selectedItem.Content} (code: {targetLanguage})");
                        }

                        // Skip translation if source and target are the same (and source isn't auto)
                        if (sourceLanguage != "auto" && sourceLanguage == targetLanguage)
                        {
                            DebugLog("Source and target languages are the same - skipping translation");
                            return;
                        }

                        // Create a more specific cache key that includes both languages
                        string cacheKey = $"{clipboardText}|{sourceLanguage}|{targetLanguage}";

                        // Check if this exact translation is already in cache
                        if (translationCache.ContainsKey(cacheKey))
                        {
                            // Use cached translation
                            string cachedTranslation = translationCache[cacheKey];
                            System.Windows.Clipboard.SetText(cachedTranslation);
                            TranslationPreview.Text = cachedTranslation;
                            DebugLog("Translation retrieved from cache");
                            return;
                        }

                        string tone = "neutral";
                        if (TranslationTone.SelectedItem != null)
                        {
                            tone = ((ComboBoxItem)TranslationTone.SelectedItem).Content.ToString().ToLower();
                        }

                        // Update last translation info
                        lastSourceLanguage = sourceLanguage;
                        lastTargetLanguage = targetLanguage;

                        // Update status
                        DebugLog("Translating...");

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

                        // Perform the translation
                        TranslationResult result = await translationService.TranslateAsync(clipboardText, sourceLanguage, targetLanguage, tone);

                        // Process result
                        if (result.Success)
                        {
                            // Store successful translation in cache with the specific key
                            translationCache[cacheKey] = result.TranslatedText;

                            // Keep cache size reasonable
                            if (translationCache.Count > 50)
                            {
                                // Remove oldest entry
                                var oldestKey = translationCache.Keys.First();
                                translationCache.Remove(oldestKey);
                            }

                            // Put translated text in clipboard
                            System.Windows.Clipboard.SetText(result.TranslatedText);

                            // Update interface
                            TranslationPreview.Text = result.TranslatedText;
                            LastDetectedLanguage.Text = result.DetectedLanguage;
                            DebugLog("Translation completed");

                            // Get source language display name
                            string sourceLangDisplay = ((ComboBoxItem)SourceLanguage.SelectedItem).Content.ToString();

                            // Get target language display name
                            string targetLangDisplay = ((ComboBoxItem)TargetLanguage.SelectedItem).Content.ToString();

                            // Call notification service to show a message to the user
                            NotificationService.ShowTranslationCompleted(sourceLangDisplay, targetLangDisplay, settings.ShowNotificationPopup);

                            // Check if should play sound
                            if (settings.PlaySoundOnTranslation)
                            {
                                try
                                {
                                    SoundService.PlayTranslationCompleteSound();
                                }
                                catch
                                {
                                    // Ignore sound errors
                                }
                            }

                            // Update statistics
                            translationsToday++;
                            settings.TranslationsToday = translationsToday;
                            settings.LastTranslationDate = DateTime.Today;
                            ConfigManager.SaveSettings(settings);
                            UpdateTranslationCounter();
                        }
                        else
                        {
                            DebugLog("Error: " + result.ErrorMessage);
                        }
                    }
                }
                finally
                {
                    // Release the semaphore in any case
                    translationSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                DebugLog("Error: " + ex.Message);
                try
                {
                    // Ensure the semaphore is released in case of error
                    translationSemaphore.Release();
                }
                catch
                {
                    // Ignore semaphore errors
                }
            }
        }

        private void ToggleStatus_Click(object sender, RoutedEventArgs e)
        {
            ToggleTranslationStatus();
        }

        private void ToggleTranslationStatus()
        {
            isActive = !isActive;

            if (isActive)
            {
                StatusText.Text = "Active";
                StatusText.Foreground = System.Windows.Media.Brushes.Green;
                ToggleStatus.Content = "Pause";
                DebugLog("Monitoring clipboard");

                // Clear the cache when reactivating
                translationCache.Clear();
            }
            else
            {
                StatusText.Text = "Paused";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                ToggleStatus.Content = "Resume";
                DebugLog("Monitoring paused");
            }
        }

        private void SwapLanguages_Click(object sender, RoutedEventArgs e)
        {
            // Don't allow swap if source is "Auto Detect"
            if (SourceLanguage.SelectedIndex == 0) return;

            // Get currently selected items
            ComboBoxItem sourceItem = (ComboBoxItem)SourceLanguage.SelectedItem;
            ComboBoxItem targetItem = (ComboBoxItem)TargetLanguage.SelectedItem;

            // Find matching items in opposite lists
            ComboBoxItem newSourceItem = null;
            ComboBoxItem newTargetItem = null;

            // Find target language in source list
            foreach (ComboBoxItem item in SourceLanguage.Items)
            {
                if (item.Tag?.ToString() == targetItem.Tag?.ToString())
                {
                    newSourceItem = item;
                    break;
                }
            }

            // Find source language in target list
            foreach (ComboBoxItem item in TargetLanguage.Items)
            {
                if (item.Tag?.ToString() == sourceItem.Tag?.ToString())
                {
                    newTargetItem = item;
                    break;
                }
            }

            // Apply the swap
            if (newSourceItem != null && newTargetItem != null)
            {
                SourceLanguage.SelectedItem = newSourceItem;
                TargetLanguage.SelectedItem = newTargetItem;

                // Clear cache when swapping languages
                translationCache.Clear();
                DebugLog("Languages swapped and cache cleared");
            }
        }

        private void LanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            // Clear cache when languages change
            translationCache.Clear();

            // Debug info about language selection
            if (sender is WpfComboBox comboBox)
            {
                if (comboBox.SelectedItem is ComboBoxItem selectedItem)
                {
                    string languageType = comboBox.Name == "SourceLanguage" ? "Source" : "Target";
                    string languageName = selectedItem.Content.ToString();
                    string languageCode = selectedItem.Tag?.ToString() ?? "unknown";

                    DebugLog($"{languageType} language changed to: {languageName} (code: {languageCode})");
                }
            }

            // Save language settings
            SaveLanguageSettings();
        }

        private void ToneChanged(object sender, SelectionChangedEventArgs e)
        {
            // Clear cache when tone changes
            translationCache.Clear();

            if (settings != null && TranslationTone.SelectedItem != null)
            {
                settings.DefaultTone = ((ComboBoxItem)TranslationTone.SelectedItem).Content.ToString();
                ConfigManager.SaveSettings(settings);
                DebugLog($"Translation tone changed to: {settings.DefaultTone}");
            }
        }

        private void SaveLanguageSettings()
        {
            if (settings == null) return;

            // Save source language display name
            if (SourceLanguage?.SelectedItem != null)
            {
                settings.DefaultSourceLanguage = ((ComboBoxItem)SourceLanguage.SelectedItem).Content.ToString();
            }

            // Save target language display name
            if (TargetLanguage?.SelectedItem != null)
            {
                settings.DefaultTargetLanguage = ((ComboBoxItem)TargetLanguage.SelectedItem).Content.ToString();
            }

            ConfigManager.SaveSettings(settings);
            DebugLog("Language settings saved");
        }

        private void MinimizeToTray_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            trayIcon.Visible = true;
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void Preferences_Click(object sender, RoutedEventArgs e)
        {
            PreferencesWindow preferencesWindow = new PreferencesWindow(settings);
            preferencesWindow.Owner = this;

            if (preferencesWindow.ShowDialog() == true)
            {
                settings = preferencesWindow.UpdatedSettings;
                ConfigManager.SaveSettings(settings);
                LoadSettings();
            }
        }

        private void ApiKeys_Click(object sender, RoutedEventArgs e)
        {
            ApiKeysWindow apiKeysWindow = new ApiKeysWindow(settings);
            apiKeysWindow.Owner = this;

            if (apiKeysWindow.ShowDialog() == true)
            {
                settings = apiKeysWindow.UpdatedSettings;
                ConfigManager.SaveSettings(settings);

                // Reinitialize the translation service with new settings
                InitializeTranslationService();

                // Clear cache when changing service
                translationCache.Clear();
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow aboutWindow = new AboutWindow();
            aboutWindow.Owner = this;
            aboutWindow.ShowDialog();
        }

        // New methods for the additional features

        private async void ShowAllTones_Click(object sender, RoutedEventArgs e)
        {
            // Make sure we have text to translate
            if (string.IsNullOrWhiteSpace(lastCopiedText))
            {
                MessageBox.Show(
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
                Button copyButton = new Button
                {
                    Content = "Use This Translation",
                    HorizontalAlignment = HorizontalAlignment.Right,
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
                        var result = await translationService.TranslateAsync(
                            lastCopiedText, sourceLanguage, targetLanguage, tone);

                        return result;
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
                var result = await translationTask;

                // Update the UI with the translation result
                await Dispatcher.InvokeAsync(() =>
                {
                    if (result.Success)
                    {
                        translationText.Text = result.TranslatedText;
                        copyButton.IsEnabled = true;
                    }
                    else
                    {
                        translationText.Text = $"Error: {result.ErrorMessage}";
                        translationText.Foreground = new SolidColorBrush(Colors.Red);
                        copyButton.IsEnabled = false;
                    }
                });
            }

            // Remove the progress indicator
            AllTonesPanel.Children.Remove(progressText);

            // Switch to the All Tones tab
            var tabControl = (TabControl)((Grid)AllTonesPanel.Parent).Parent;
            tabControl.SelectedIndex = 1;
        }

        private void CopyToneTranslation_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            TextBlock textBlock = (TextBlock)button.Tag;

            // Copy the translation to the clipboard
            System.Windows.Clipboard.SetText(textBlock.Text);

            // Also update the translation preview
            TranslationPreview.Text = textBlock.Text;

            // Switch to the Translation Preview tab
            var tabControl = (TabControl)((Grid)AllTonesPanel.Parent).Parent;
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
                MessageBox.Show(
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
                    var tabControl = (TabControl)((Grid)AllTonesPanel.Parent).Parent;
                    tabControl.SelectedIndex = 0;

                    // Update status
                    StatusBarText.Text = "AI parameters test successful";

                    // Show a dialog with the results
                    MessageBox.Show(
                        "AI parameters test successful. The translation is displayed in the preview tab.",
                        "Test Successful",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    // Show an error message
                    MessageBox.Show(
                        $"Error testing AI parameters: {result.ErrorMessage}",
                        "Test Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    StatusBarText.Text = "AI parameters test failed";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error testing AI parameters: {ex.Message}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusBarText.Text = "AI parameters test error";
            }
        }
    }
}
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
using System.IO;

namespace Translator
{
    public partial class MainWindow : Window
    {
        // Clipboard monitor
        private ClipboardMonitor clipboardMonitor;

        // Translation service
        private ITranslationService translationService;

        // Settings
        private TranslationSettings settings;

        // AI parameters
        private AIParameters aiParameters;

        // Last copied text
        private string lastCopiedText = "";

        // Last detected language
        private string lastDetectedLanguage = "";

        // Translation cache to avoid re-translating the same text
        private Dictionary<string, TranslationResult> translationCache = new Dictionary<string, TranslationResult>();

        // System tray icon
        private NotifyIcon trayIcon;

        // Translation monitoring status
        private bool isMonitoringEnabled = true;

        public MainWindow()
        {
            InitializeComponent();

            // Load settings
            settings = ConfigManager.LoadSettings();

            // Initialize AI parameters
            aiParameters = settings.AIParameters ?? new AIParameters();

            // Initialize the translation service
            InitializeTranslationService();

            // Initialize clipboard monitor
            InitializeClipboardMonitor();

            // Initialize system tray icon
            InitializeSystemTrayIcon();

            // Configure interface based on settings
            ConfigureInterface();

            // Set event handlers
            SetEventHandlers();

            // Check for statistics reset
            CheckDailyStatisticsReset();

            // Atualizar UI para mostrar status pausado
            StatusText.Text = "Paused";
            StatusText.Foreground = new SolidColorBrush(Colors.Red);
            ToggleStatus.Content = "Resume";
            StatusBarText.Text = "Clipboard monitoring paused";
        }

        private void InitializeTranslationService()
        {
            // Create the translation service based on preferred service
            if (settings.PreferredService == "OpenAI")
            {
                translationService = new OpenAITranslationService();
                translationService.SetApiKey(settings.OpenAIApiKey);
            }
            else
            {
                translationService = new GeminiTranslationService();
                translationService.SetApiKey(settings.GeminiApiKey);
            }

            // Set AI parameters
            translationService.SetAIParameters(aiParameters);
        }

        private void InitializeClipboardMonitor()
        {
            // Create a clipboard monitor instance
            clipboardMonitor = new ClipboardMonitor(this);

            // Subscribe to clipboard change events
            clipboardMonitor.ClipboardChanged += OnClipboardChanged;

            // Initialize after window is loaded
            this.Loaded += (s, e) =>
            {
                var windowHandle = new WindowInteropHelper(this).Handle;
                clipboardMonitor.Initialize(windowHandle);

                // Check if we should start minimized
                if (settings.StartMinimized)
                {
                    this.WindowState = WindowState.Minimized;
                    MinimizeToTray();
                }
            };
        }

        private void InitializeSystemTrayIcon()
        {
            // Create tray icon
            trayIcon = new NotifyIcon
            {
                Icon = new Icon(System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Resources/translate_icon.ico")).Stream),
                Visible = true,
                Text = "Clipboard Translator"
            };

            // Create context menu
            var contextMenu = new ContextMenuStrip();

            // Show/Hide window
            var showHideItem = new ToolStripMenuItem("Show/Hide Window");
            showHideItem.Click += (s, e) => ToggleWindowVisibility();
            contextMenu.Items.Add(showHideItem);

            // Enable/Disable translation
            var enableDisableItem = new ToolStripMenuItem("Enable/Disable Translation");
            enableDisableItem.Click += (s, e) => ToggleTranslationStatus();
            contextMenu.Items.Add(enableDisableItem);

            // Separator
            contextMenu.Items.Add(new ToolStripSeparator());

            // Exit
            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => System.Windows.Application.Current.Shutdown();
            contextMenu.Items.Add(exitItem);

            // Assign context menu
            trayIcon.ContextMenuStrip = contextMenu;

            // Double-click to show/hide window
            trayIcon.DoubleClick += (s, e) => ToggleWindowVisibility();

            // Initialize the notification service
            NotificationService.Initialize(trayIcon);
        }

        private void ConfigureInterface()
        {
            // Configure language combo boxes
            ConfigureLanguageComboBoxes();

            // Configure tone combo box
            ConfigureToneComboBox();

            // Configure AI parameters
            ConfigureAIParameters();

            // Configure statistics
            UpdateStatistics();
        }

        private void ConfigureLanguageComboBoxes()
        {
            // Clear and add items to source language combo box
            SourceLanguage.Items.Clear();
            SourceLanguage.Items.Add(new ComboBoxItem { Content = "Auto Detect", Tag = "auto" });
            SourceLanguage.Items.Add(new ComboBoxItem { Content = "Portuguese", Tag = "pt" });
            SourceLanguage.Items.Add(new ComboBoxItem { Content = "English", Tag = "en" });
            SourceLanguage.Items.Add(new ComboBoxItem { Content = "Italian", Tag = "it" });
            SourceLanguage.Items.Add(new ComboBoxItem { Content = "Spanish", Tag = "es" });
            SourceLanguage.Items.Add(new ComboBoxItem { Content = "French", Tag = "fr" });
            SourceLanguage.Items.Add(new ComboBoxItem { Content = "German", Tag = "de" });

            // Clear and add items to target language combo box
            TargetLanguage.Items.Clear();
            TargetLanguage.Items.Add(new ComboBoxItem { Content = "Portuguese", Tag = "pt" });
            TargetLanguage.Items.Add(new ComboBoxItem { Content = "English", Tag = "en" });
            TargetLanguage.Items.Add(new ComboBoxItem { Content = "Italian", Tag = "it" });
            TargetLanguage.Items.Add(new ComboBoxItem { Content = "Spanish", Tag = "es" });
            TargetLanguage.Items.Add(new ComboBoxItem { Content = "French", Tag = "fr" });
            TargetLanguage.Items.Add(new ComboBoxItem { Content = "German", Tag = "de" });

            // Select default languages from settings
            SelectDefaultLanguages();
        }

        private void SelectDefaultLanguages()
        {
            // Find and select the source language based on settings
            foreach (ComboBoxItem item in SourceLanguage.Items)
            {
                if (item.Content.ToString() == settings.DefaultSourceLanguage)
                {
                    SourceLanguage.SelectedItem = item;
                    break;
                }
            }

            // If no item was selected, select the first one
            if (SourceLanguage.SelectedIndex == -1 && SourceLanguage.Items.Count > 0)
            {
                SourceLanguage.SelectedIndex = 0;
            }

            // Find and select the target language based on settings
            foreach (ComboBoxItem item in TargetLanguage.Items)
            {
                if (item.Content.ToString() == settings.DefaultTargetLanguage)
                {
                    TargetLanguage.SelectedItem = item;
                    break;
                }
            }

            // If no item was selected, select English or the first one
            if (TargetLanguage.SelectedIndex == -1)
            {
                bool foundEnglish = false;

                foreach (ComboBoxItem item in TargetLanguage.Items)
                {
                    if (item.Content.ToString() == "English")
                    {
                        TargetLanguage.SelectedItem = item;
                        foundEnglish = true;
                        break;
                    }
                }

                if (!foundEnglish && TargetLanguage.Items.Count > 0)
                {
                    TargetLanguage.SelectedIndex = 0;
                }
            }
        }

        private void ConfigureToneComboBox()
        {
            // Clear and add items to tone combo box
            TranslationTone.Items.Clear();
            TranslationTone.Items.Add(new ComboBoxItem { Content = "Neutral" });
            TranslationTone.Items.Add(new ComboBoxItem { Content = "Formal" });
            TranslationTone.Items.Add(new ComboBoxItem { Content = "Casual" });
            TranslationTone.Items.Add(new ComboBoxItem { Content = "Technical" });
            TranslationTone.Items.Add(new ComboBoxItem { Content = "Professional" });

            // Select default tone from settings
            foreach (ComboBoxItem item in TranslationTone.Items)
            {
                if (item.Content.ToString() == settings.DefaultTone)
                {
                    TranslationTone.SelectedItem = item;
                    break;
                }
            }

            // If no item was selected, select the first one
            if (TranslationTone.SelectedIndex == -1 && TranslationTone.Items.Count > 0)
            {
                TranslationTone.SelectedIndex = 0;
            }
        }

        private void ConfigureAIParameters()
        {
            // Set AI parameter sliders
            TemperatureSlider.Value = aiParameters.Temperature;
            TopPSlider.Value = aiParameters.TopP;
            FrequencyPenaltySlider.Value = aiParameters.FrequencyPenalty;
            PresencePenaltySlider.Value = aiParameters.PresencePenalty;

            // Configure model version selector
            ModelVersionSelector.Items.Clear();
            ModelVersionSelector.Items.Add(new ComboBoxItem { Content = "Default (Latest)" });
            ModelVersionSelector.Items.Add(new ComboBoxItem { Content = "GPT-3.5 Turbo" });
            ModelVersionSelector.Items.Add(new ComboBoxItem { Content = "GPT-4" });
            ModelVersionSelector.Items.Add(new ComboBoxItem { Content = "Gemini Pro" });
            ModelVersionSelector.Items.Add(new ComboBoxItem { Content = "Gemini Flash" });

            // Select model version from settings
            bool foundModel = false;
            if (!string.IsNullOrEmpty(aiParameters.ModelVersion) && aiParameters.ModelVersion != "Default")
            {
                foreach (ComboBoxItem item in ModelVersionSelector.Items)
                {
                    if (item.Content.ToString() == aiParameters.ModelVersion)
                    {
                        ModelVersionSelector.SelectedItem = item;
                        EnableModelVersionSelector.IsChecked = true;
                        foundModel = true;
                        break;
                    }
                }
            }

            if (!foundModel)
            {
                ModelVersionSelector.SelectedIndex = 0;
                EnableModelVersionSelector.IsChecked = false;
            }
        }

        private void UpdateStatistics()
        {
            // Update the statistics display
            TranslationCount.Text = settings.TranslationsToday.ToString();
            LastDetectedLanguage.Text = lastDetectedLanguage;
        }

        private void SetEventHandlers()
        {
            // Set window closing handler
            this.Closing += OnWindowClosing;
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Check if we should minimize to tray instead of closing
            if (settings.MinimizeToTrayOnClose)
            {
                e.Cancel = true;
                MinimizeToTray();
            }
            else
            {
                // Save settings before closing
                SaveSettings();

                // Dispose resources
                clipboardMonitor?.Dispose();
                trayIcon?.Dispose();
            }
        }

        private void CheckDailyStatisticsReset()
        {
            // Check if it's a new day and reset statistics if needed
            DateTime now = DateTime.Now;
            DateTime lastDate = settings.LastTranslationDate;

            if (lastDate.Date < now.Date)
            {
                // It's a new day, reset the counter
                settings.TranslationsToday = 0;
                settings.LastTranslationDate = now;

                // Save the settings
                ConfigManager.SaveSettings(settings);

                // Update the statistics display
                UpdateStatistics();
            }
        }

        private void SaveSettings()
        {
            if (settings == null)
            {
                settings = new TranslationSettings(); // Ensure settings is initialized
            }

            // Save current selected languages as defaults
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
            settings.AIParameters = aiParameters;

            // Save settings to file
            ConfigManager.SaveSettings(settings);
        }

        private void ToggleWindowVisibility()
        {
            if (this.WindowState == WindowState.Minimized || !this.IsVisible)
            {
                // Show and restore window
                this.Show();
                this.WindowState = WindowState.Normal;
                this.Activate();
            }
            else
            {
                // Minimize to tray
                MinimizeToTray();
            }
        }

        private void MinimizeToTray()
        {
            this.WindowState = WindowState.Minimized;
            this.Hide();
        }

        // Em MainWindow.xaml.cs, modifique o método ToggleTranslationStatus
        private void ToggleTranslationStatus()
        {
            isMonitoringEnabled = !isMonitoringEnabled;

            // Aplicar a alteração ao monitor do clipboard
            clipboardMonitor.SetMonitoringEnabled(isMonitoringEnabled);

            // Update UI
            if (isMonitoringEnabled)
            {
                StatusText.Text = "Active";
                StatusText.Foreground = new SolidColorBrush(Colors.Green);
                ToggleStatus.Content = "Pause";
                StatusBarText.Text = "Clipboard monitoring active";
            }
            else
            {
                StatusText.Text = "Paused";
                StatusText.Foreground = new SolidColorBrush(Colors.Red);
                ToggleStatus.Content = "Resume";
                StatusBarText.Text = "Clipboard monitoring paused";
            }

            // Mostrar mensagem de status
            if (!isMonitoringEnabled)
            {
                NotificationService.ShowWarning(
                    "Clipboard Monitoring Paused",
                    "Translation has been paused. Click 'Resume' to start monitoring again.");
            }
        }

        private async void OnClipboardChanged()
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
                    await TranslateClipboardText(clipboardText);
                }
            }
            catch (Exception ex)
            {
                DebugLog($"Error processing clipboard change: {ex.Message}");
            }
        }

        private async Task TranslateClipboardText(string text)
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
                    await ApplyTranslation(cachedResult);

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
                    await ApplyTranslation(translationResult);
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

        private async Task ApplyTranslation(TranslationResult result)
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

                    // Show notification if enabled
                    if (settings.ShowNotificationPopup)
                    {
                        NotificationService.ShowTranslationCompleted(
                            result.DetectedLanguage ?? "Unknown",
                            ((ComboBoxItem)TargetLanguage.SelectedItem).Content.ToString(),
                            settings.ShowNotificationPopup
                        );
                    }
                }
                catch (Exception ex)
                {
                    DebugLog($"Error applying translation: {ex.Message}");
                }
            });
        }

        private void DebugLog(string message)
        {
            // Write log message to debug console or file
            System.Diagnostics.Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");

            // Could also write to a log file
            try
            {
                string logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ClipboardTranslator",
                    "debug.log");

                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
            }
            catch
            {
                // Ignore errors writing to log file
            }
        }

        // Event handlers for UI elements

        private void MinimizeToTray_Click(object sender, RoutedEventArgs e)
        {
            MinimizeToTray();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            // Save settings
            SaveSettings();

            // Force application to exit
            System.Windows.Application.Current.Shutdown();
        }

        private void Preferences_Click(object sender, RoutedEventArgs e)
        {
            // Open preferences window
            var preferencesWindow = new PreferencesWindow(settings);
            preferencesWindow.Owner = this;

            if (preferencesWindow.ShowDialog() == true)
            {
                // Update settings with changes
                settings = preferencesWindow.UpdatedSettings;

                // Save settings
                ConfigManager.SaveSettings(settings);
            }
        }

        private void ApiKeys_Click(object sender, RoutedEventArgs e)
        {
            // Open API keys window
            var apiKeysWindow = new ApiKeysWindow(settings);
            apiKeysWindow.Owner = this;

            if (apiKeysWindow.ShowDialog() == true)
            {
                // Update settings with changes
                settings = apiKeysWindow.UpdatedSettings;

                // Reinitialize translation service with new API keys
                InitializeTranslationService();

                // Save settings
                ConfigManager.SaveSettings(settings);
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            // Open about window
            var aboutWindow = new AboutWindow();
            aboutWindow.Owner = this;
            aboutWindow.ShowDialog();
        }

        // Em MainWindow.xaml.cs, antes de ativar o monitoramento
        private void ToggleStatus_Click(object sender, RoutedEventArgs e)
        {
            // Se estiver tentando ativar, verificar primeiro se há chave API configurada
            if (!isMonitoringEnabled) // Vai mudar para ativo
            {
                bool hasApiKey = false;

                // Verificar se há alguma chave configurada
                if (settings.PreferredService == "OpenAI")
                {
                    hasApiKey = !string.IsNullOrEmpty(settings.OpenAIApiKey);
                }
                else
                {
                    hasApiKey = !string.IsNullOrEmpty(settings.GeminiApiKey);
                }

                if (!hasApiKey)
                {
                    WpfMessageBox.Show(
                        "No API key configured. Please configure your API key in Settings > API Keys before enabling translation.",
                        "API Key Required",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    // Abrir a janela de configuração de API automaticamente
                    ApiKeys_Click(sender, e);
                    return;
                }
            }

            // Se tem chave ou está desativando, continuar normalmente
            ToggleTranslationStatus();
        }

        private void SwapLanguages_Click(object sender, RoutedEventArgs e)
        {
            // Can only swap if source is not auto-detect
            if (SourceLanguage.SelectedIndex > 0)
            {
                int sourceIndex = SourceLanguage.SelectedIndex;
                int targetIndex = TargetLanguage.SelectedIndex;

                // Subtract 1 from source index when setting target because
                // source has "Auto Detect" as first item
                TargetLanguage.SelectedIndex = sourceIndex - 1;
                SourceLanguage.SelectedIndex = targetIndex + 1;
            }
        }

        private void LanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            // Clear translation cache when language changes
            translationCache.Clear();

            // Save settings
            SaveSettings();
        }

        private void ToneChanged(object sender, SelectionChangedEventArgs e)
        {
            // Clear translation cache when tone changes
            translationCache.Clear();

            // Save settings
            SaveSettings();
        }

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

            // Switch to the All Tones tab - versão corrigida
            WpfTabControl tabControl = null;

            // Navegar pela hierarquia corretamente
            FrameworkElement parent = AllTonesPanel;
            while (parent != null && tabControl == null)
            {
                parent = parent.Parent as FrameworkElement;
                tabControl = parent as WpfTabControl;
            }

            // Se encontrou o TabControl, selecionar a aba
            if (tabControl != null)
            {
                tabControl.SelectedIndex = 1;
            }
            else
            {
                // Caso não consiga encontrar o TabControl, registrar erro no log
                DebugLog("Não foi possível encontrar o TabControl pai");
            }
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
                var translationResult = await translationService.TranslateAsync(
                    lastCopiedText, sourceLanguage, targetLanguage, tone);

                if (translationResult.Success)
                {
                    // Update the translation preview
                    TranslationPreview.Text = translationResult.TranslatedText;

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
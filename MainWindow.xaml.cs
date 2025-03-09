using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Drawing;
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
        // Alteração aqui: mudamos para false para começar pausado
        private bool isMonitoringEnabled = false;

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

            // Update UI to show paused status
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
            clipboardMonitor.ClipboardChanged += OnClipboardChangedHandler;

            // Initialize after window is loaded
            this.Loaded += (s, e) =>
            {
                var windowHandle = new WindowInteropHelper(this).Handle;
                clipboardMonitor.Initialize(windowHandle);

                // Adicionando essa linha para garantir que o monitor comece pausado
                clipboardMonitor.SetMonitoringEnabled(isMonitoringEnabled);

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

        private void ToggleTranslationStatus()
        {
            isMonitoringEnabled = !isMonitoringEnabled;

            // Apply change to clipboard monitor
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
        }

        // Debug logging method
        internal void DebugLog(string message)
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

        private void ToggleStatus_Click(object sender, RoutedEventArgs e)
        {
            // Check for API key if trying to activate
            if (!isMonitoringEnabled) // Going to change to active
            {
                bool hasApiKey = false;

                // Check if any key is configured
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

                    // Open API keys window automatically
                    ApiKeys_Click(sender, e);
                    return;
                }
            }

            // Toggle the status if key exists or disabling
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
    }
}
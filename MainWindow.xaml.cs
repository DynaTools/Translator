using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms; // Referência ao System.Windows.Forms
using System.Drawing; // Referência ao System.Drawing
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Text;
using ClipboardTranslator;
using System.Threading;
using System.Linq;

namespace Translator
{
    public partial class MainWindow : Window
    {
        private NotifyIcon trayIcon;
        private bool isActive = true;

        // Dicionário para mapear texto original e idioma destino para evitar repetições
        private Dictionary<string, Dictionary<string, string>> translationCache = new Dictionary<string, Dictionary<string, string>>();

        private Dictionary<string, string> languageCodes = new Dictionary<string, string>();
        private ClipboardMonitor clipboardMonitor;
        private ITranslationService translationService;
        private int translationsToday = 0;
        private DateTime lastTranslationDate = DateTime.MinValue;
        private TranslationSettings settings = new TranslationSettings();

        // Semáforo para evitar traduções simultâneas
        private SemaphoreSlim translationSemaphore = new SemaphoreSlim(1, 1);

        // Armazenar o último texto copiado para debug
        private string lastCopiedText = string.Empty;

        // Controle de última tradução
        private string lastSourceLanguage = string.Empty;
        private string lastTargetLanguage = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();
            InitializeLanguageCodes();
            LoadSettings();
            InitializeTranslationService();

            // Iniciar o monitoramento da área de transferência
            clipboardMonitor = new ClipboardMonitor(this);
            clipboardMonitor.ClipboardChanged += OnClipboardChanged;

            // Configurar eventos para quando a janela for carregada e fechada
            this.Loaded += MainWindow_Loaded;
            this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            clipboardMonitor.Initialize(hwnd);
            UpdateTranslationCounter();

            // Se deve iniciar minimizado
            if (settings.StartMinimized)
            {
                this.Hide();
                trayIcon.Visible = true;
            }

            // Atualizar a interface com as configurações
            StatusBarText.Text = "Pronto para traduzir";
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Remover o ícone da bandeja do sistema
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }

            // Desativar o monitoramento de área de transferência
            clipboardMonitor.Dispose();
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

            // Criar menu para o ícone da bandeja
            ContextMenuStrip contextMenu = new ContextMenuStrip();

            ToolStripMenuItem openItem = new ToolStripMenuItem("Abrir");
            openItem.Click += (s, e) => ShowMainWindow();

            ToolStripMenuItem toggleItem = new ToolStripMenuItem("Pausar/Retomar");
            toggleItem.Click += (s, e) => ToggleTranslationStatus();

            ToolStripMenuItem exitItem = new ToolStripMenuItem("Sair");
            exitItem.Click += (s, e) => System.Windows.Application.Current.Shutdown();

            contextMenu.Items.Add(openItem);
            contextMenu.Items.Add(toggleItem);
            contextMenu.Items.Add(new ToolStripSeparator());
            contextMenu.Items.Add(exitItem);

            trayIcon.ContextMenuStrip = contextMenu;
        }

        private void TrayIcon_Click(object sender, EventArgs e)
        {
            // Se o clique for com o botão esquerdo, mostra a janela
            // Qualificar completamente o MouseEventArgs para evitar ambiguidade
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
            languageCodes.Add("Detecção automática", "auto");
            languageCodes.Add("Portuguese", "pt");
            languageCodes.Add("Português", "pt");
            languageCodes.Add("English", "en");
            languageCodes.Add("Inglês", "en");
            languageCodes.Add("Italian", "it");
            languageCodes.Add("Italiano", "it");
            languageCodes.Add("Spanish", "es");
            languageCodes.Add("Espanhol", "es");
            languageCodes.Add("French", "fr");
            languageCodes.Add("Francês", "fr");
            languageCodes.Add("German", "de");
            languageCodes.Add("Alemão", "de");
        }

        private void InitializeTranslationService()
        {
            // Load settings
            settings = ConfigManager.LoadSettings();

            // Select service based on configuration
            if (settings.PreferredService == "OpenAI" && !string.IsNullOrEmpty(settings.OpenAIApiKey))
            {
                translationService = new OpenAITranslationService();
                translationService.SetApiKey(settings.OpenAIApiKey);
                StatusBarText.Text = "Using service: OpenAI";
            }
            else if (settings.PreferredService == "Gemini" && !string.IsNullOrEmpty(settings.GeminiApiKey))
            {
                translationService = new GeminiTranslationService();
                translationService.SetApiKey(settings.GeminiApiKey);
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

            // Definir valores padrão com base nas configurações
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

            // Verificar estatísticas salvas
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

        private void UpdateTranslationCounter()
        {
            TranslationCount.Text = translationsToday.ToString();
        }

        private async void OnClipboardChanged()
        {
            if (!isActive) return;

            try
            {
                // Tentar obter o semáforo de tradução com um timeout curto
                // Se não conseguir, outra tradução está em andamento
                if (!await translationSemaphore.WaitAsync(100))
                {
                    StatusBarText.Text = "Tradução já em andamento, aguarde...";
                    return;
                }

                try
                {
                    // Verificar se há texto no clipboard
                    if (System.Windows.Clipboard.ContainsText())
                    {
                        string clipboardText = System.Windows.Clipboard.GetText().Trim();
                        lastCopiedText = clipboardText;

                        // Ignorar se o texto for muito curto
                        if (string.IsNullOrWhiteSpace(clipboardText) || clipboardText.Length < 2)
                        {
                            StatusBarText.Text = "Texto muito curto para tradução";
                            return;
                        }

                        // Verificar limite de tokens (movido para depois de obter o texto)
                        if (settings.EnableTokenLimit)
                        {
                            // Use o método estimador de tokens da classe auxiliar
                            int estimatedTokens = TextTokenizer.EstimateTokenCount(clipboardText);
                            if (estimatedTokens > settings.MaxTokensLimit)
                            {
                                string message = $"O texto copiado excede o limite de tokens configurado ({settings.MaxTokensLimit}).\n" +
                                                 $"Tokens estimados: {estimatedTokens}.\n\n" +
                                                 "Deseja continuar com a tradução?";

                                var dialogResult = System.Windows.MessageBox.Show(message, "Aviso de Limite de Tokens",
                                   System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);

                                if (dialogResult == System.Windows.MessageBoxResult.No)
                                {
                                    StatusBarText.Text = "Tradução cancelada pelo usuário (limite de tokens)";
                                    return;
                                }
                            }
                        }

                        // Obter parâmetros de tradução
                        string sourceLanguage = "auto";
                        if (SourceLanguage.SelectedItem != null)
                        {
                            var sourceLang = ((ComboBoxItem)SourceLanguage.SelectedItem).Content.ToString();
                            sourceLanguage = languageCodes.ContainsKey(sourceLang) ? languageCodes[sourceLang] : "auto";
                        }

                        string targetLanguage = "en";
                        if (TargetLanguage.SelectedItem != null)
                        {
                            var targetLang = ((ComboBoxItem)TargetLanguage.SelectedItem).Content.ToString();
                            targetLanguage = languageCodes.ContainsKey(targetLang) ? languageCodes[targetLang] : "en";
                        }

                        // Verificar cache se o mesmo texto já foi traduzido para o mesmo idioma alvo
                        if (translationCache.ContainsKey(clipboardText) &&
                            translationCache[clipboardText].ContainsKey(targetLanguage))
                        {
                            // Usar tradução em cache
                            string cachedTranslation = translationCache[clipboardText][targetLanguage];
                            System.Windows.Clipboard.SetText(cachedTranslation);
                            TranslationPreview.Text = cachedTranslation;
                            StatusBarText.Text = "Texto recuperado do cache";
                            return;
                        }

                        string tone = "neutral";
                        if (TranslationTone.SelectedItem != null)
                        {
                            tone = ((ComboBoxItem)TranslationTone.SelectedItem).Content.ToString().ToLower();
                        }

                        // Atualizar last translation info
                        lastSourceLanguage = sourceLanguage;
                        lastTargetLanguage = targetLanguage;

                        // Atualizar status
                        StatusBarText.Text = "Traduzindo...";

                        // Realizar a tradução
                        TranslationResult result = await translationService.TranslateAsync(clipboardText, sourceLanguage, targetLanguage, tone);

                        // Processar resultado
                        if (result.Success)
                        {
                            // Armazenar em cache
                            if (!translationCache.ContainsKey(clipboardText))
                            {
                                translationCache[clipboardText] = new Dictionary<string, string>();
                            }
                            translationCache[clipboardText][targetLanguage] = result.TranslatedText;

                            // Manter o cache com tamanho razoável
                            if (translationCache.Count > 50)
                            {
                                // Remover o item mais antigo
                                var oldest = translationCache.Keys.First();
                                translationCache.Remove(oldest);
                            }

                            // Colocar texto traduzido na área de transferência
                            System.Windows.Clipboard.SetText(result.TranslatedText);

                            // Atualizar interface
                            TranslationPreview.Text = result.TranslatedText;
                            LastDetectedLanguage.Text = result.DetectedLanguage;
                            StatusBarText.Text = "Tradução concluída";

                            // Verificar se deve tocar som
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

                            // Atualizar estatísticas
                            translationsToday++;
                            settings.TranslationsToday = translationsToday;
                            settings.LastTranslationDate = DateTime.Today;
                            ConfigManager.SaveSettings(settings);
                            UpdateTranslationCounter();
                        }
                        else
                        {
                            StatusBarText.Text = "Erro: " + result.ErrorMessage;
                        }
                    }
                }
                finally
                {
                    // Liberar o semáforo em qualquer caso
                    translationSemaphore.Release();
                }
            }
            catch (Exception ex)
            {
                StatusBarText.Text = "Erro: " + ex.Message;
                try
                {
                    // Garantir que o semáforo é liberado em caso de erro
                    translationSemaphore.Release();
                }
                catch
                {
                    // Ignorar erros no semáforo
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
                StatusText.Text = "Ativo";
                StatusText.Foreground = System.Windows.Media.Brushes.Green;
                ToggleStatus.Content = "Pausar";
                StatusBarText.Text = "Monitorando área de transferência";

                // Limpar o cache ao reativar
                translationCache.Clear();
            }
            else
            {
                StatusText.Text = "Pausado";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                ToggleStatus.Content = "Retomar";
                StatusBarText.Text = "Monitoramento pausado";
            }
        }

        private void SwapLanguages_Click(object sender, RoutedEventArgs e)
        {
            // Não permitir troca se a origem for "Detecção automática"
            if (SourceLanguage.SelectedIndex == 0) return;

            int sourceIndex = SourceLanguage.SelectedIndex;
            int targetIndex = TargetLanguage.SelectedIndex;

            // Ajustar índice para compensar a opção "Detecção automática"
            int adjustedSourceIndex = sourceIndex - 1;

            TargetLanguage.SelectedIndex = adjustedSourceIndex;
            SourceLanguage.SelectedIndex = targetIndex + 1; // +1 porque a lista de origem tem a opção "auto"

            // Limpar cache ao trocar idiomas
            translationCache.Clear();
        }

        private void LanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            // Limpar cache ao mudar idiomas
            translationCache.Clear();

            // Salvar configurações quando idiomas forem alterados
            SaveLanguageSettings();
        }

        private void ToneChanged(object sender, SelectionChangedEventArgs e)
        {
            // Limpar cache ao mudar tom
            translationCache.Clear();

            if (settings != null && TranslationTone.SelectedItem != null)
            {
                settings.DefaultTone = ((ComboBoxItem)TranslationTone.SelectedItem).Content.ToString();
                ConfigManager.SaveSettings(settings);
            }
        }

        private void SaveLanguageSettings()
        {
            if (settings == null) return;

            if (SourceLanguage.SelectedItem != null)
            {
                settings.DefaultSourceLanguage = ((ComboBoxItem)SourceLanguage.SelectedItem).Content.ToString();
            }

            if (TargetLanguage != null && TargetLanguage.SelectedItem != null)
            {
                settings.DefaultTargetLanguage = ((ComboBoxItem)TargetLanguage.SelectedItem).Content.ToString();
            }

            ConfigManager.SaveSettings(settings);
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

                // Reinicializar o serviço de tradução com as novas configurações
                InitializeTranslationService();

                // Limpar cache ao mudar serviço
                translationCache.Clear();
            }
        }

        private void About_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow aboutWindow = new AboutWindow();
            aboutWindow.Owner = this;
            aboutWindow.ShowDialog();
        }
    }
}
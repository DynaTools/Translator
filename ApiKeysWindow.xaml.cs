using ClipboardTranslator;
using System;
using System.Windows;

namespace Translator
{
    public partial class ApiKeysWindow : Window
    {
        public TranslationSettings UpdatedSettings { get; private set; }

        public ApiKeysWindow(TranslationSettings currentSettings)
        {
            InitializeComponent();

            // Copiar configurações atuais
            UpdatedSettings = new TranslationSettings
            {
                DefaultSourceLanguage = currentSettings.DefaultSourceLanguage,
                DefaultTargetLanguage = currentSettings.DefaultTargetLanguage,
                DefaultTone = currentSettings.DefaultTone,
                GoogleApiKey = currentSettings.GoogleApiKey,
                OpenAIApiKey = currentSettings.OpenAIApiKey,
                PreferredService = currentSettings.PreferredService,
                StartWithWindows = currentSettings.StartWithWindows,
                StartMinimized = currentSettings.StartMinimized,
                PlaySoundOnTranslation = currentSettings.PlaySoundOnTranslation,
                TranslationsToday = currentSettings.TranslationsToday,
                LastTranslationDate = currentSettings.LastTranslationDate
            };

            // Configurar a interface com os valores atuais
            GoogleApiKeyTextBox.Text = UpdatedSettings.GoogleApiKey;
            OpenAIApiKeyTextBox.Text = UpdatedSettings.OpenAIApiKey;

            // Configurar o serviço preferido
            if (UpdatedSettings.PreferredService == "OpenAI")
            {
                OpenAIRadioButton.IsChecked = true;
            }
            else
            {
                GoogleRadioButton.IsChecked = true;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Atualizar as configurações com base na interface
            UpdatedSettings.GoogleApiKey = GoogleApiKeyTextBox.Text.Trim();
            UpdatedSettings.OpenAIApiKey = OpenAIApiKeyTextBox.Text.Trim();

            // Determinar o serviço preferido
            UpdatedSettings.PreferredService = OpenAIRadioButton.IsChecked == true ? "OpenAI" : "Google";

            // Definir resultado e fechar
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Fechar sem salvar
            DialogResult = false;
        }

        private void GoogleHelpLink_Click(object sender, RoutedEventArgs e)
        {
            // Abrir a página de ajuda no navegador
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://cloud.google.com/translate/docs/getting-started",
                UseShellExecute = true
            });
        }

        private async void TestGoogleButton_Click(object sender, RoutedEventArgs e)
        {
            string apiKey = GoogleApiKeyTextBox.Text.Trim();

            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show("Por favor, insira uma chave de API válida.", "Chave Inválida",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TestGoogleButton.IsEnabled = false;
            TestGoogleButton.Content = "Testando...";

            try
            {
                // Criar uma instância temporária do serviço
                var testService = new GoogleTranslationService();
                testService.SetApiKey(apiKey);

                // Testar uma tradução simples
                var result = await testService.TranslateAsync(
                    "Olá, isso é um teste de conexão.",
                    "pt",
                    "en",
                    "neutro");

                if (result.Success)
                {
                    MessageBox.Show(
                        $"Conexão bem-sucedida!\nTexto traduzido: {result.TranslatedText}",
                        "Teste de Conexão",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"Erro ao testar conexão: {result.ErrorMessage}",
                        "Falha no Teste",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Erro ao testar conexão: {ex.Message}",
                    "Erro",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                TestGoogleButton.IsEnabled = true;
                TestGoogleButton.Content = "Testar Conexão";
            }
        }

        private void OpenAIHelpLink_Click(object sender, RoutedEventArgs e)
        {
            // Abrir a página de ajuda no navegador
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://platform.openai.com/api-keys",
                UseShellExecute = true
            });
        }

        private async void TestOpenAIButton_Click(object sender, RoutedEventArgs e)
        {
            string apiKey = OpenAIApiKeyTextBox.Text.Trim();

            if (string.IsNullOrEmpty(apiKey))
            {
                MessageBox.Show("Por favor, insira uma chave de API válida.", "Chave Inválida",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TestOpenAIButton.IsEnabled = false;
            TestOpenAIButton.Content = "Testando...";

            try
            {
                // Criar uma instância temporária do serviço
                var testService = new OpenAITranslationService();
                testService.SetApiKey(apiKey);

                // Testar uma tradução simples
                var result = await testService.TranslateAsync(
                    "Olá, isso é um teste de conexão.",
                    "pt",
                    "en",
                    "neutro");

                if (result.Success)
                {
                    MessageBox.Show(
                        $"Conexão bem-sucedida!\nTexto traduzido: {result.TranslatedText}",
                        "Teste de Conexão",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show(
                        $"Erro ao testar conexão: {result.ErrorMessage}",
                        "Falha no Teste",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Erro ao testar conexão: {ex.Message}",
                    "Erro",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                TestOpenAIButton.IsEnabled = true;
                TestOpenAIButton.Content = "Testar Conexão";
            }
        }
    }
}
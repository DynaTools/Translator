using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web; // Referência necessária

namespace Translator
{
    // Classe para armazenar os resultados da tradução
    public class TranslationResult
    {
        public bool Success { get; set; }
        public string TranslatedText { get; set; }
        public string DetectedLanguage { get; set; }
        public string ErrorMessage { get; set; }

        public TranslationResult()
        {
            Success = false;
            TranslatedText = string.Empty;
            DetectedLanguage = string.Empty;
            ErrorMessage = string.Empty;
        }
    }

    // Interface para serviços de tradução
    public interface ITranslationService
    {
        Task<TranslationResult> TranslateAsync(string text, string sourceLanguage, string targetLanguage, string tone);
        void SetApiKey(string apiKey);
    }

    // Implementação do serviço do Google Translate
    public class GoogleTranslationService : ITranslationService
    {
        private string apiKey;
        private HttpClient httpClient;

        public GoogleTranslationService()
        {
            httpClient = new HttpClient();
        }

        public void SetApiKey(string apiKey)
        {
            this.apiKey = apiKey;
        }

        public async Task<TranslationResult> TranslateAsync(string text, string sourceLanguage, string targetLanguage, string tone)
        {
            TranslationResult result = new TranslationResult();

            try
            {
                // Se não tiver API key configurada, usar o método alternativo
                if (string.IsNullOrEmpty(apiKey))
                {
                    return await TranslateWithAlternativeMethodAsync(text, sourceLanguage, targetLanguage, tone);
                }

                // Construir URL para a API do Google Cloud Translation
                string url = $"https://translation.googleapis.com/language/translate/v2?key={apiKey}";

                // Ajustar o texto com base no tom
                string adjustedText = AdjustTextForTone(text, tone);

                // Construir o corpo da requisição
                var requestBody = new
                {
                    q = adjustedText,
                    source = sourceLanguage == "auto" ? null : sourceLanguage,
                    target = targetLanguage,
                    format = "text"
                };

                var jsonRequest = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                // Enviar a requisição
                var response = await httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var translationResponse = JsonSerializer.Deserialize<GoogleTranslationResponse>(jsonResponse);

                    if (translationResponse?.Data?.Translations != null && translationResponse.Data.Translations.Count > 0)
                    {
                        result.Success = true;
                        result.TranslatedText = translationResponse.Data.Translations[0].TranslatedText;
                        result.DetectedLanguage = sourceLanguage == "auto" ?
                            translationResponse.Data.Translations[0].DetectedSourceLanguage : sourceLanguage;
                    }
                    else
                    {
                        result.ErrorMessage = "Não foi possível obter uma tradução válida.";
                    }
                }
                else
                {
                    result.ErrorMessage = $"Erro na API: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}";
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Erro ao traduzir: {ex.Message}";
            }

            return result;
        }

        // Rest of the code...

        // Método alternativo para tradução sem API key
        private async Task<TranslationResult> TranslateWithAlternativeMethodAsync(string text, string sourceLanguage, string targetLanguage, string tone)
        {
            TranslationResult result = new TranslationResult();

            try
            {
                // Ajustar o texto com base no tom
                string adjustedText = AdjustTextForTone(text, tone);

                // Usar a API gratuita do Google Translate
                string url = "https://translate.googleapis.com/translate_a/single?client=gtx&dt=t";

                // Adicionar parâmetros
                url += $"&sl={sourceLanguage}";
                url += $"&tl={targetLanguage}";
                url += $"&q={Uri.EscapeDataString(adjustedText)}"; // Use Uri.EscapeDataString instead of HttpUtility.UrlEncode

                // Enviar a requisição
                var response = await httpClient.GetAsync(url);

                // Rest of the code...
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Erro no método alternativo: {ex.Message}";
            }

            return result;
        }

        // Ajustar o texto com base no tom selecionado
        private string AdjustTextForTone(string text, string tone)
        {
            // Não modificar o texto original se o tom for neutro
            if (tone == "neutral" || tone == "neutro")
                return text;

            // Adicionar contexto para o tom no início do texto, para melhorar o resultado da tradução
            switch (tone.ToLower())
            {
                case "formal":
                    return $"[Por favor, traduza isto em um tom formal e profissional]: {text}";
                case "coloquial":
                    return $"[Traduza isto de maneira coloquial e informal]: {text}";
                case "técnico":
                case "tecnico":
                    return $"[Traduza isto usando terminologia técnica e precisa]: {text}";
                case "profissional":
                    return $"[Traduza isto para um contexto de negócios profissional]: {text}";
                default:
                    return text;
            }
        }

        // Mapear códigos de idioma para nomes
        private string MapLanguageCodeToName(string code)
        {
            switch (code.ToLower())
            {
                case "pt": return "Português";
                case "en": return "Inglês";
                case "it": return "Italiano";
                case "es": return "Espanhol";
                case "fr": return "Francês";
                case "de": return "Alemão";
                default: return code;
            }
        }
    }

    // Classe para deserialização da resposta da API do Google
    public class GoogleTranslationResponse
    {
        public TranslationData Data { get; set; }
    }

    public class TranslationData
    {
        public List<Translation> Translations { get; set; }
    }

    public class Translation
    {
        public string TranslatedText { get; set; }
        public string DetectedSourceLanguage { get; set; }
    }
}
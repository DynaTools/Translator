using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

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
                            MapLanguageCodeToName(translationResponse.Data.Translations[0].DetectedSourceLanguage) :
                            MapLanguageCodeToName(sourceLanguage);
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
                url += $"&q={Uri.EscapeDataString(adjustedText)}";

                // Enviar a requisição
                var response = await httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();

                    // Processar resposta JSON (formato diferente da API oficial)
                    // O formato é um array complexo, então precisamos parsear manualmente
                    try
                    {
                        using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                        {
                            var root = doc.RootElement;

                            // Construir a tradução a partir dos segmentos
                            var sb = new StringBuilder();
                            var firstArray = root[0];

                            for (int i = 0; i < firstArray.GetArrayLength(); i++)
                            {
                                var translationSegment = firstArray[i][0];
                                if (translationSegment.ValueKind != JsonValueKind.Null)
                                {
                                    sb.Append(translationSegment.GetString());
                                }
                            }

                            // Obter idioma detectado (se estiver disponível)
                            string detectedLanguage = "auto";
                            if (root.GetArrayLength() > 2 && root[2].ValueKind != JsonValueKind.Null)
                            {
                                detectedLanguage = root[2].GetString();
                            }

                            result.Success = true;
                            result.TranslatedText = sb.ToString();
                            result.DetectedLanguage = MapLanguageCodeToName(detectedLanguage);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.ErrorMessage = $"Erro ao processar resposta: {ex.Message}";
                    }
                }
                else
                {
                    result.ErrorMessage = $"Erro na API alternativa: {response.StatusCode}";
                }
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
            if (string.IsNullOrEmpty(code)) return "Desconhecido";

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

    // Implementação do serviço OpenAI para tradução
    public class OpenAITranslationService : ITranslationService
    {
        private string apiKey;
        private HttpClient httpClient;
        private const string ApiEndpoint = "https://api.openai.com/v1/chat/completions";

        public OpenAITranslationService()
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

            if (string.IsNullOrEmpty(apiKey))
            {
                result.ErrorMessage = "A chave de API da OpenAI não está configurada.";
                return result;
            }

            try
            {
                // Converter códigos de idioma para nomes completos para melhor desempenho com a IA
                string sourceLang = MapLanguageCodeToName(sourceLanguage);
                string targetLang = MapLanguageCodeToName(targetLanguage);

                // Criar o prompt para a IA baseado nos parâmetros
                string prompt = CreateTranslationPrompt(text, sourceLang, targetLang, tone);

                // Configurar a requisição
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                // Criar o corpo da requisição
                var requestBody = new
                {
                    model = "gpt-3.5-turbo", // Modelo mais econômico e rápido para traduções
                    messages = new[]
                    {
                        new { role = "system", content = "Você é um tradutor profissional preciso. Responda apenas com o texto traduzido, sem explicações adicionais." },
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.3, // Valor baixo para mais consistência
                    max_tokens = 2048
                };

                var jsonRequest = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                // Enviar a requisição
                var response = await httpClient.PostAsync(ApiEndpoint, content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                    {
                        var choices = doc.RootElement.GetProperty("choices");
                        if (choices.GetArrayLength() > 0)
                        {
                            var message = choices[0].GetProperty("message");
                            var translatedText = message.GetProperty("content").GetString();

                            result.Success = true;
                            result.TranslatedText = translatedText?.Trim();

                            // Para OpenAI, não temos detecção de idioma automática, então usamos o fornecido
                            // ou indicamos que foi feito pela IA
                            result.DetectedLanguage = sourceLanguage == "auto" ?
                                "Detectado pela IA" : MapLanguageCodeToName(sourceLanguage);
                        }
                        else
                        {
                            result.ErrorMessage = "Não foi possível obter uma tradução da OpenAI.";
                        }
                    }
                }
                else
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    result.ErrorMessage = $"Erro na API da OpenAI: {response.StatusCode} - {errorResponse}";
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Erro ao traduzir com OpenAI: {ex.Message}";
            }

            return result;
        }

        private string CreateTranslationPrompt(string text, string sourceLanguage, string targetLanguage, string tone)
        {
            string prompt;

            if (sourceLanguage == "auto" || sourceLanguage == "Detecção automática")
            {
                prompt = $"Traduza o seguinte texto para {targetLanguage}:\n\n{text}";
            }
            else
            {
                prompt = $"Traduza o seguinte texto de {sourceLanguage} para {targetLanguage}:\n\n{text}";
            }

            // Adicionar instruções de tom se não for neutro
            if (tone.ToLower() != "neutro" && tone.ToLower() != "neutral")
            {
                switch (tone.ToLower())
                {
                    case "formal":
                        prompt += "\n\nUse um tom formal e profissional na tradução.";
                        break;
                    case "coloquial":
                        prompt += "\n\nUse um tom casual e coloquial na tradução.";
                        break;
                    case "técnico":
                        prompt += "\n\nUse terminologia técnica e precisa na tradução.";
                        break;
                    case "profissional":
                        prompt += "\n\nUse um tom adequado para ambientes de negócios na tradução.";
                        break;
                }
            }

            return prompt;
        }

        // Mapear códigos de idioma para nomes
        private string MapLanguageCodeToName(string code)
        {
            if (string.IsNullOrEmpty(code) || code == "auto") return "Detecção automática";

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

    // Classes para deserialização da resposta da API do Google
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
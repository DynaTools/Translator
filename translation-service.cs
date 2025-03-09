using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;

namespace ClipboardTranslator
{
    public class TranslationResult
    {
        public bool Success { get; set; }
        public string TranslatedText { get; set; }
        public string DetectedLanguage { get; set; }
        public string ErrorMessage { get; set; }
        public string DetailedError { get; set; } // Campo adicional para erros detalhados
    }

    public interface ITranslationService
    {
        Task<TranslationResult> TranslateAsync(string text, string sourceLanguage, string targetLanguage, string tone);
        void SetApiKey(string apiKey);
        void SetAIParameters(AIParameters parameters);
    }

    public class OpenAITranslationService : ITranslationService
    {
        private string apiKey;
        private AIParameters aiParameters;
        private static readonly HttpClient client = new HttpClient();

        public OpenAITranslationService()
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            aiParameters = new AIParameters();
        }

        public void SetApiKey(string apiKey)
        {
            this.apiKey = apiKey;
        }

        public void SetAIParameters(AIParameters parameters)
        {
            this.aiParameters = parameters ?? new AIParameters();
        }

        private bool IsQuotaExceededError(Exception ex)
        {
            return ex.Message.Contains("429") ||
                  ex.Message.Contains("quota") ||
                  ex.Message.Contains("RESOURCE_EXHAUSTED");
        }

        public async Task<TranslationResult> TranslateAsync(string text, string sourceLanguage, string targetLanguage, string tone)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                return new TranslationResult
                {
                    Success = false,
                    ErrorMessage = "Chave da API não configurada."
                };
            }

            try
            {
                // Configurar a URL da API e os cabeçalhos
                var url = "https://api.openai.com/v1/chat/completions";
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                // Determinar o modelo
                string model = "gpt-3.5-turbo";
                if (aiParameters.ModelVersion != "Default")
                {
                    if (aiParameters.ModelVersion == "GPT-4")
                    {
                        model = "gpt-4";
                    }
                }

                // Construir a mensagem para o modelo
                string systemPrompt = GetTranslationPrompt(sourceLanguage, targetLanguage, tone);

                // Preparar a requisição JSON
                var requestData = new
                {
                    model = model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = text }
                    },
                    temperature = aiParameters.Temperature,
                    top_p = aiParameters.TopP,
                    frequency_penalty = aiParameters.FrequencyPenalty,
                    presence_penalty = aiParameters.PresencePenalty,
                };

                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Fazer a requisição
                var response = await client.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                // Verificar por erros na resposta
                if (!response.IsSuccessStatusCode)
                {
                    // Tratamento específico para erro 429 (Too Many Requests)
                    if ((int)response.StatusCode == 429)
                    {
                        return new TranslationResult
                        {
                            Success = false,
                            ErrorMessage = "Cota da API excedida. Tente novamente mais tarde ou verifique seu plano de assinatura.",
                            DetailedError = $"API Error: 429. {responseContent}"
                        };
                    }

                    return new TranslationResult
                    {
                        Success = false,
                        ErrorMessage = $"API Error: {(int)response.StatusCode}. {responseContent}",
                        DetailedError = responseContent
                    };
                }

                // Processar a resposta
                using (JsonDocument doc = JsonDocument.Parse(responseContent))
                {
                    string translatedText = doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();

                    return new TranslationResult
                    {
                        Success = true,
                        TranslatedText = translatedText,
                        DetectedLanguage = sourceLanguage == "auto" ? "Auto" : sourceLanguage
                    };
                }
            }
            catch (Exception ex)
            {
                if (IsQuotaExceededError(ex))
                {
                    return new TranslationResult
                    {
                        Success = false,
                        ErrorMessage = "Cota da API excedida. Tente novamente mais tarde ou verifique seu plano de assinatura.",
                        DetailedError = ex.Message
                    };
                }
                else
                {
                    return new TranslationResult
                    {
                        Success = false,
                        ErrorMessage = $"Erro na tradução: {ex.Message}",
                        DetailedError = ex.ToString()
                    };
                }
            }
        }

        private string GetTranslationPrompt(string sourceLanguage, string targetLanguage, string tone)
        {
            string fromLanguage = sourceLanguage == "auto" ? "qualquer idioma" : GetLanguageName(sourceLanguage);
            string toLanguage = GetLanguageName(targetLanguage);

            string prompt = $"Você é um tradutor especializado em traduzir de {fromLanguage} para {toLanguage}.";

            // Adicionar instruções de tom
            switch (tone.ToLower())
            {
                case "formal":
                    prompt += " Use linguagem formal e técnica, apropriada para documentos de negócios ou acadêmicos.";
                    break;
                case "casual":
                    prompt += " Use linguagem casual e conversacional, como em uma conversa entre amigos.";
                    break;
                case "technical":
                    prompt += " Use terminologia técnica e precisa, apropriada para documentação técnica.";
                    break;
                case "professional":
                    prompt += " Use linguagem profissional, clara e direta, apropriada para comunicação de negócios.";
                    break;
                default: // neutral
                    prompt += " Use linguagem neutra e clara.";
                    break;
            }

            prompt += " Traduza apenas o texto, sem adicionar explicações ou notas. Preserve a formatação original.";

            return prompt;
        }

        private string GetLanguageName(string languageCode)
        {
            switch (languageCode.ToLower())
            {
                case "en": return "inglês";
                case "pt": return "português";
                case "es": return "espanhol";
                case "fr": return "francês";
                case "de": return "alemão";
                case "it": return "italiano";
                default: return languageCode;
            }
        }
    }

    public class GeminiTranslationService : ITranslationService
    {
        private string apiKey;
        private AIParameters aiParameters;
        private static readonly HttpClient client = new HttpClient();

        public GeminiTranslationService()
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            aiParameters = new AIParameters();
        }

        public void SetApiKey(string apiKey)
        {
            this.apiKey = apiKey;
        }

        public void SetAIParameters(AIParameters parameters)
        {
            this.aiParameters = parameters ?? new AIParameters();
        }

        private bool IsQuotaExceededError(Exception ex)
        {
            return ex.Message.Contains("429") ||
                   ex.Message.Contains("quota") ||
                   ex.Message.Contains("RESOURCE_EXHAUSTED");
        }

        public async Task<TranslationResult> TranslateAsync(string text, string sourceLanguage, string targetLanguage, string tone)
        {
            if (string.IsNullOrEmpty(apiKey))
            {
                return new TranslationResult
                {
                    Success = false,
                    ErrorMessage = "Chave da API não configurada."
                };
            }

            try
            {
                // Modelo padrão atualizado para Gemini 2.0
                string model = "gemini-2.0-flash";

                // Configuração dos modelos com base na seleção do usuário
                if (aiParameters.ModelVersion != "Default")
                {
                    switch (aiParameters.ModelVersion)
                    {
                        case "Gemini Flash":
                            model = "gemini-2.0-flash";
                            break;
                        case "Gemini Pro":
                            model = "gemini-2.0-pro";
                            break;
                        case "Gemini Flash-Lite":
                            model = "gemini-2.0-flash-lite";
                            break;
                    }
                }

                // Endpoint da API v1beta
                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

                // Construir a mensagem para o modelo
                string systemPrompt = GetTranslationPrompt(sourceLanguage, targetLanguage, tone);

                // Payload simplificado conforme exemplo
                var requestData = new
                {
                    contents = new[]
                    {
                    new
                    {
                        parts = new[]
                        {
                            new { text = $"{systemPrompt}\n\n{text}" }
                        }
                    }
                },
                    generationConfig = new
                    {
                        temperature = aiParameters.Temperature,
                        topP = aiParameters.TopP
                    }
                };

                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Fazer a requisição
                var response = await client.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                // Verificar por erros na resposta
                if (!response.IsSuccessStatusCode)
                {
                    if ((int)response.StatusCode == 429)
                    {
                        return new TranslationResult
                        {
                            Success = false,
                            ErrorMessage = "Cota da API excedida. Tente novamente mais tarde ou verifique seu plano de assinatura.",
                            DetailedError = $"API Error: 429. {responseContent}"
                        };
                    }

                    return new TranslationResult
                    {
                        Success = false,
                        ErrorMessage = $"API Error: {(int)response.StatusCode}. {responseContent}",
                        DetailedError = responseContent
                    };
                }

                // Processar a resposta
                using (JsonDocument doc = JsonDocument.Parse(responseContent))
                {
                    string translatedText = doc.RootElement
                        .GetProperty("candidates")[0]
                        .GetProperty("content")
                        .GetProperty("parts")[0]
                        .GetProperty("text")
                        .GetString();

                    return new TranslationResult
                    {
                        Success = true,
                        TranslatedText = translatedText,
                        DetectedLanguage = sourceLanguage == "auto" ? "Auto" : sourceLanguage
                    };
                }
            }
            catch (Exception ex)
            {
                if (IsQuotaExceededError(ex))
                {
                    return new TranslationResult
                    {
                        Success = false,
                        ErrorMessage = "Cota da API excedida. Tente novamente mais tarde ou verifique seu plano de assinatura.",
                        DetailedError = ex.Message
                    };
                }
                else
                {
                    return new TranslationResult
                    {
                        Success = false,
                        ErrorMessage = $"Erro na tradução: {ex.Message}",
                        DetailedError = ex.ToString()
                    };
                }
            }
        }

        private string GetTranslationPrompt(string sourceLanguage, string targetLanguage, string tone)
        {
            string fromLanguage = sourceLanguage == "auto" ? "qualquer idioma" : GetLanguageName(sourceLanguage);
            string toLanguage = GetLanguageName(targetLanguage);

            string prompt = $"Você é um tradutor especializado em traduzir de {fromLanguage} para {toLanguage}.";

            // Adicionar instruções de tom
            switch (tone.ToLower())
            {
                case "formal":
                    prompt += " Use linguagem formal e técnica, apropriada para documentos de negócios ou acadêmicos.";
                    break;
                case "casual":
                    prompt += " Use linguagem casual e conversacional, como em uma conversa entre amigos.";
                    break;
                case "technical":
                    prompt += " Use terminologia técnica e precisa, apropriada para documentação técnica.";
                    break;
                case "professional":
                    prompt += " Use linguagem profissional, clara e direta, apropriada para comunicação de negócios.";
                    break;
                default: // neutral
                    prompt += " Use linguagem neutra e clara.";
                    break;
            }

            prompt += " Traduza apenas o texto, sem adicionar explicações ou notas. Preserve a formatação original.";

            return prompt;
        }

        private string GetLanguageName(string languageCode)
        {
            switch (languageCode.ToLower())
            {
                case "en": return "inglês";
                case "pt": return "português";
                case "es": return "espanhol";
                case "fr": return "francês";
                case "de": return "alemão";
                case "it": return "italiano";
                default: return languageCode;
            }
        }
    }

    // Classe para estimar tokens
    public static class TextTokenizer
    {
        // Estimativa simples: em média, 1 token = 4 caracteres em inglês
        // Para outros idiomas isso pode variar, mas é uma boa aproximação
        public static int EstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // Aproximação: dividir o número de caracteres por 4
            return (int)Math.Ceiling(text.Length / 4.0);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace ClipboardTranslator
{
    // Define the interface for translation services
    public interface ITranslationService
    {
        void SetApiKey(string apiKey);
        void SetAIParameters(AIParameters parameters);
        Task<TranslationResult> TranslateAsync(string text, string sourceLanguage, string targetLanguage, string tone);
    }

    // Result class to hold translation information
    public class TranslationResult
    {
        public bool Success { get; set; }
        public string TranslatedText { get; set; }
        public string DetectedLanguage { get; set; }
        public string ErrorMessage { get; set; }
    }

    // AI parameters class to hold settings for translation models
    public class TranslationServiceBase
    {
        protected AIParameters aiParameters = new AIParameters();

        public void SetAIParameters(AIParameters parameters)
        {
            aiParameters = parameters ?? new AIParameters();
        }
    }

    // OpenAI service implementation
    public class OpenAITranslationService : TranslationServiceBase, ITranslationService
    {
        private string apiKey;
        private readonly HttpClient httpClient;

        // Language name mapping
        private static readonly Dictionary<string, string> languageNames = new Dictionary<string, string>
        {
            { "en", "English" },
            { "pt", "Portuguese" },
            { "es", "Spanish" },
            { "fr", "French" },
            { "de", "German" },
            { "it", "Italian" },
            { "auto", "Automatically detected" }
        };

        public OpenAITranslationService()
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            // Timeout is set to 30 seconds
            httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public void SetApiKey(string apiKey)
        {
            this.apiKey = apiKey;
        }

        public async Task<TranslationResult> TranslateAsync(string text, string sourceLanguage, string targetLanguage, string tone)
        {
            try
            {
                // Check for API key
                if (string.IsNullOrEmpty(apiKey))
                {
                    return new TranslationResult
                    {
                        Success = false,
                        ErrorMessage = "API key is not set. Please configure it in Settings > API Keys."
                    };
                }

                // Set authorization header with API key
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                // Get language names for better prompting
                string sourceLangName = languageNames.ContainsKey(sourceLanguage) ? languageNames[sourceLanguage] : sourceLanguage;
                string targetLangName = languageNames.ContainsKey(targetLanguage) ? languageNames[targetLanguage] : targetLanguage;

                // Create the system message based on the translation request
                string systemMessage = $"You are a professional translator. Translate the user's text from {sourceLangName} to {targetLangName} using a {tone} tone. Only respond with the translated text, nothing else.";

                // Prepare the request payload
                var requestPayload = new
                {
                    model = GetModelName(),
                    messages = new[]
                    {
                        new { role = "system", content = systemMessage },
                        new { role = "user", content = text }
                    },
                    temperature = aiParameters.Temperature,
                    top_p = aiParameters.TopP,
                    frequency_penalty = aiParameters.FrequencyPenalty,
                    presence_penalty = aiParameters.PresencePenalty
                };

                // Convert the payload to JSON
                string jsonPayload = JsonSerializer.Serialize(requestPayload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Send the request to the OpenAI API
                var response = await httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);

                // Check if the request was successful
                if (response.IsSuccessStatusCode)
                {
                    // Parse the response
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                    {
                        JsonElement root = doc.RootElement;

                        // Get the translated text from the choices
                        if (root.TryGetProperty("choices", out JsonElement choices) && choices.GetArrayLength() > 0)
                        {
                            string translatedText = choices[0].GetProperty("message").GetProperty("content").GetString();

                            // Return the success result
                            return new TranslationResult
                            {
                                Success = true,
                                TranslatedText = translatedText,
                                DetectedLanguage = sourceLangName
                            };
                        }
                    }

                    // If we got here, there was an issue with parsing the response
                    return new TranslationResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to parse translation response"
                    };
                }
                else
                {
                    // Read error details
                    string errorResponse = await response.Content.ReadAsStringAsync();

                    return new TranslationResult
                    {
                        Success = false,
                        ErrorMessage = $"API Error: {response.StatusCode}. {errorResponse}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new TranslationResult
                {
                    Success = false,
                    ErrorMessage = $"Translation Error: {ex.Message}"
                };
            }
        }

        private string GetModelName()
        {
            // Return the appropriate model based on the AI parameters
            if (string.IsNullOrEmpty(aiParameters.ModelVersion) || aiParameters.ModelVersion == "Default")
            {
                return "gpt-3.5-turbo"; // Default model
            }

            switch (aiParameters.ModelVersion)
            {
                case "GPT-3.5 Turbo":
                    return "gpt-3.5-turbo";
                case "GPT-4":
                    return "gpt-4";
                default:
                    return "gpt-3.5-turbo"; // Fallback to default
            }
        }
    }

    // Gemini service implementation
    public class GeminiTranslationService : TranslationServiceBase, ITranslationService
    {
        private string apiKey;
        private readonly HttpClient httpClient;

        // Language name mapping
        private static readonly Dictionary<string, string> languageNames = new Dictionary<string, string>
        {
            { "en", "English" },
            { "pt", "Portuguese" },
            { "es", "Spanish" },
            { "fr", "French" },
            { "de", "German" },
            { "it", "Italian" },
            { "auto", "Automatically detected" }
        };

        public GeminiTranslationService()
        {
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            // Timeout is set to 30 seconds
            httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public void SetApiKey(string apiKey)
        {
            this.apiKey = apiKey;
        }

        public async Task<TranslationResult> TranslateAsync(string text, string sourceLanguage, string targetLanguage, string tone)
        {
            try
            {
                // Check for API key
                if (string.IsNullOrEmpty(apiKey))
                {
                    // Fall back to a free translation API
                    return await FallbackTranslationAsync(text, sourceLanguage, targetLanguage);
                }

                // Get language names for better prompting
                string sourceLangName = languageNames.ContainsKey(sourceLanguage) ? languageNames[sourceLanguage] : sourceLanguage;
                string targetLangName = languageNames.ContainsKey(targetLanguage) ? languageNames[targetLanguage] : targetLanguage;

                // Create the prompt based on the translation request
                string prompt = $"Translate the following text from {sourceLangName} to {targetLangName} using a {tone} tone. Only respond with the translated text, nothing else:\n\n{text}";

                // Prepare the request payload based on the model
                string modelName = GetModelName();

                var requestPayload = new
                {
                    contents = new[]
                    {
                        new { role = "user", parts = new[] { new { text = prompt } } }
                    },
                    generationConfig = new
                    {
                        temperature = aiParameters.Temperature,
                        topP = aiParameters.TopP
                    }
                };

                // Convert the payload to JSON
                string jsonPayload = JsonSerializer.Serialize(requestPayload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // Send the request to the Gemini API
                string apiEndpoint = $"https://generativelanguage.googleapis.com/v1/models/{modelName}:generateContent?key={apiKey}";
                var response = await httpClient.PostAsync(apiEndpoint, content);

                // Check if the request was successful
                if (response.IsSuccessStatusCode)
                {
                    // Parse the response
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                    {
                        JsonElement root = doc.RootElement;

                        // Navigate to the content
                        if (root.TryGetProperty("candidates", out JsonElement candidates) &&
                            candidates.GetArrayLength() > 0 &&
                            candidates[0].TryGetProperty("content", out JsonElement content_el) &&
                            content_el.TryGetProperty("parts", out JsonElement parts) &&
                            parts.GetArrayLength() > 0 &&
                            parts[0].TryGetProperty("text", out JsonElement textEl))
                        {
                            string translatedText = textEl.GetString();

                            // Return the success result
                            return new TranslationResult
                            {
                                Success = true,
                                TranslatedText = translatedText,
                                DetectedLanguage = sourceLangName
                            };
                        }
                    }

                    // If we got here, there was an issue with parsing the response
                    return new TranslationResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to parse translation response"
                    };
                }
                else
                {
                    // Read error details
                    string errorResponse = await response.Content.ReadAsStringAsync();

                    // If the error is related to the API key or permissions, try fallback
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                        response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                    {
                        return await FallbackTranslationAsync(text, sourceLanguage, targetLanguage);
                    }

                    return new TranslationResult
                    {
                        Success = false,
                        ErrorMessage = $"API Error: {response.StatusCode}. {errorResponse}"
                    };
                }
            }
            catch (Exception ex)
            {
                // Try fallback on any error
                try
                {
                    return await FallbackTranslationAsync(text, sourceLanguage, targetLanguage);
                }
                catch
                {
                    return new TranslationResult
                    {
                        Success = false,
                        ErrorMessage = $"Translation Error: {ex.Message}"
                    };
                }
            }
        }

        private async Task<TranslationResult> FallbackTranslationAsync(string text, string sourceLanguage, string targetLanguage)
        {
            try
            {
                // Use a free translation API as fallback
                // This is a basic implementation using a public API endpoint

                // URL encode the text
                string encodedText = HttpUtility.UrlEncode(text);

                // For automatic detection, don't specify source language
                string sourceParam = sourceLanguage == "auto" ? "" : $"&source={sourceLanguage}";

                // Build the request URL
                string requestUrl = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sourceLanguage}&tl={targetLanguage}&dt=t&q={encodedText}";

                // Send the request
                var response = await httpClient.GetAsync(requestUrl);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();

                    // This API returns a nested array structure that's not standard JSON
                    // We need to parse it manually

                    // The response is like [[[translated,original,null,null]],null,"en"]
                    // where the last element is the detected language

                    // Remove starting and ending brackets
                    jsonResponse = jsonResponse.TrimStart('[').TrimEnd(']');

                    // Get first array which contains translations
                    int firstArrayEnd = jsonResponse.IndexOf("],");
                    if (firstArrayEnd > 0)
                    {
                        string translationsJson = jsonResponse.Substring(0, firstArrayEnd + 1);

                        // Parse translations
                        using (JsonDocument doc = JsonDocument.Parse("[" + translationsJson + "]"))
                        {
                            var translationParts = new List<string>();
                            foreach (var item in doc.RootElement[0].EnumerateArray())
                            {
                                if (item.GetArrayLength() > 0)
                                {
                                    translationParts.Add(item[0].GetString());
                                }
                            }

                            string fullTranslation = string.Join(" ", translationParts);

                            // Try to get detected language
                            string detectedLang = "auto";

                            // The last element in the response has the detected language
                            int langIndex = jsonResponse.LastIndexOf(",\"");
                            if (langIndex > 0)
                            {
                                detectedLang = jsonResponse.Substring(langIndex + 2).Trim('"', ']');

                                // Map to language name if possible
                                if (languageNames.ContainsKey(detectedLang))
                                {
                                    detectedLang = languageNames[detectedLang];
                                }
                            }

                            return new TranslationResult
                            {
                                Success = true,
                                TranslatedText = fullTranslation,
                                DetectedLanguage = detectedLang
                            };
                        }
                    }

                    return new TranslationResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to parse translation from fallback service"
                    };
                }
                else
                {
                    return new TranslationResult
                    {
                        Success = false,
                        ErrorMessage = $"Fallback service error: {response.StatusCode}"
                    };
                }
            }
            catch (Exception ex)
            {
                return new TranslationResult
                {
                    Success = false,
                    ErrorMessage = $"Fallback translation error: {ex.Message}"
                };
            }
        }

        private string GetModelName()
        {
            // Return the appropriate model based on the AI parameters
            if (string.IsNullOrEmpty(aiParameters.ModelVersion) ||
                aiParameters.ModelVersion == "Default" ||
                aiParameters.ModelVersion == "Gemini Flash")
            {
                return "gemini-1.5-flash"; // Default model
            }

            switch (aiParameters.ModelVersion)
            {
                case "Gemini Pro":
                    return "gemini-1.5-pro";
                default:
                    return "gemini-1.5-flash"; // Fallback to default
            }
        }
    }

    // Helper class for estimating token count
    public static class TextTokenizer
    {
        // Simple approximation: ~4 characters per token for English text
        // This is just an estimation, actual tokenization depends on the model
        private const int CHARS_PER_TOKEN = 4;

        public static int EstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            return (text.Length + CHARS_PER_TOKEN - 1) / CHARS_PER_TOKEN;
        }
    }
}
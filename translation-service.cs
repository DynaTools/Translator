using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace Translator
{
    // Class to store translation results
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

    // Interface for translation services
    public interface ITranslationService
    {
        Task<TranslationResult> TranslateAsync(string text, string sourceLanguage, string targetLanguage, string tone);
        void SetApiKey(string apiKey);
    }

    // Implementation of Google Gemini Flash API service for translation
    public class GeminiTranslationService : ITranslationService
    {
        private string apiKey;
        private HttpClient httpClient;
        private const string ApiEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-flash:generateContent";

        public GeminiTranslationService()
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
                // If no API key is configured, use the alternative method
                if (string.IsNullOrEmpty(apiKey))
                {
                    return await TranslateWithAlternativeMethodAsync(text, sourceLanguage, targetLanguage, tone);
                }

                // Build URL for the Gemini API
                string url = $"{ApiEndpoint}?key={apiKey}";

                // Adjust text based on tone
                string prompt = CreateTranslationPrompt(text, sourceLanguage, targetLanguage, tone);

                // Build request body
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new
                                {
                                    text = prompt
                                }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.2,
                        topK = 40,
                        topP = 0.95,
                        maxOutputTokens = 2048
                    }
                };

                var jsonRequest = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                // Send request
                var response = await httpClient.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                    {
                        // Extract the translated text from the response
                        if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
                            candidates.GetArrayLength() > 0 &&
                            candidates[0].TryGetProperty("content", out var content1) &&
                            content1.TryGetProperty("parts", out var parts) &&
                            parts.GetArrayLength() > 0 &&
                            parts[0].TryGetProperty("text", out var text1))
                        {
                            string translatedText = text1.GetString();

                            // Clean up formatting and any prefixes/suffixes
                            translatedText = CleanTranslationOutput(translatedText);

                            result.Success = true;
                            result.TranslatedText = translatedText;
                            result.DetectedLanguage = sourceLanguage == "auto" ?
                                "Detected by AI" : MapLanguageCodeToName(sourceLanguage);
                        }
                        else
                        {
                            result.ErrorMessage = "Could not extract translation from response.";
                        }
                    }
                }
                else
                {
                    result.ErrorMessage = $"Gemini API Error: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}";
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Error translating with Gemini: {ex.Message}";
            }

            return result;
        }

        // Alternative translation method for when API key is not available
        private async Task<TranslationResult> TranslateWithAlternativeMethodAsync(string text, string sourceLanguage, string targetLanguage, string tone)
        {
            TranslationResult result = new TranslationResult();

            try
            {
                // No need for tone adjustment in alternative method
                // The free API doesn't support tone, but we'll keep the text as is
                string adjustedText = text;

                // Use the free Google Translate API
                string url = "https://translate.googleapis.com/translate_a/single?client=gtx&dt=t";

                // Add parameters
                url += $"&sl={sourceLanguage}";
                url += $"&tl={targetLanguage}";
                url += $"&q={Uri.EscapeDataString(adjustedText)}";

                // Send request
                var response = await httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();

                    // Process JSON response (different format from the official API)
                    // The format is a complex array, so we need to parse it manually
                    try
                    {
                        using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                        {
                            var root = doc.RootElement;

                            // Build translation from segments
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

                            // Get detected language (if available)
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
                        result.ErrorMessage = $"Error processing response: {ex.Message}";
                    }
                }
                else
                {
                    result.ErrorMessage = $"Alternative API Error: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Error in alternative method: {ex.Message}";
            }

            return result;
        }

        // Clean up any formatting or extra text in the translation
        private string CleanTranslationOutput(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Remove any quotes that may wrap the text
            text = text.Trim('"', '\'', ' ', '\n', '\r');

            // Some models like to prefix with "Translation: " - remove that
            if (text.StartsWith("Translation:", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring("Translation:".Length).Trim();
            }

            return text;
        }

        // Create translation prompt based on parameters
        private string CreateTranslationPrompt(string text, string sourceLanguage, string targetLanguage, string tone)
        {
            string prompt;

            if (sourceLanguage == "auto" || sourceLanguage == "Auto Detect")
            {
                prompt = $"Translate the following text into {MapLanguageCodeToName(targetLanguage)}:\n\n{text}";
            }
            else
            {
                prompt = $"Translate the following text from {MapLanguageCodeToName(sourceLanguage)} to {MapLanguageCodeToName(targetLanguage)}:\n\n{text}";
            }

            // Add tone instructions if not neutral
            if (tone.ToLower() != "neutral")
            {
                switch (tone.ToLower())
                {
                    case "formal":
                        prompt += "\n\nUse a formal and professional tone in the translation.";
                        break;
                    case "casual":
                        prompt += "\n\nUse a casual and colloquial tone in the translation.";
                        break;
                    case "technical":
                        prompt += "\n\nUse precise technical terminology in the translation.";
                        break;
                    case "professional":
                        prompt += "\n\nUse a business-appropriate tone in the translation.";
                        break;
                }
            }

            // Add instruction to return only the translated text without explanations
            prompt += "\n\nReturn only the translated text without any additional comments, explanations, or quotes.";

            return prompt;
        }

        // Map language codes to names
        private string MapLanguageCodeToName(string code)
        {
            if (string.IsNullOrEmpty(code) || code == "auto") return "Auto Detect";

            switch (code.ToLower())
            {
                case "pt": return "Portuguese";
                case "en": return "English";
                case "it": return "Italian";
                case "es": return "Spanish";
                case "fr": return "French";
                case "de": return "German";
                default: return code;
            }
        }
    }

    // Implementation of OpenAI API service
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
                result.ErrorMessage = "OpenAI API key is not configured.";
                return result;
            }

            try
            {
                // Convert language codes to full names for better AI performance
                string sourceLang = MapLanguageCodeToName(sourceLanguage);
                string targetLang = MapLanguageCodeToName(targetLanguage);

                // Create AI prompt based on parameters
                string prompt = CreateTranslationPrompt(text, sourceLang, targetLang, tone);

                // Configure request
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                // Create request body
                var requestBody = new
                {
                    model = "gpt-3.5-turbo", // More economical and fast model for translations
                    messages = new[]
                    {
                        new { role = "system", content = "You are a precise professional translator. Respond only with the translated text, without additional explanations." },
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.3, // Low value for more consistency
                    max_tokens = 2048
                };

                var jsonRequest = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                // Send request
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

                            // Clean up any extra formatting
                            translatedText = CleanTranslationOutput(translatedText);

                            result.Success = true;
                            result.TranslatedText = translatedText;

                            // For OpenAI, we don't have automatic language detection, so we use what was provided
                            // or indicate it was done by AI
                            result.DetectedLanguage = sourceLanguage == "auto" ?
                                "Detected by AI" : MapLanguageCodeToName(sourceLanguage);
                        }
                        else
                        {
                            result.ErrorMessage = "Could not get translation from OpenAI.";
                        }
                    }
                }
                else
                {
                    string errorResponse = await response.Content.ReadAsStringAsync();
                    result.ErrorMessage = $"OpenAI API Error: {response.StatusCode} - {errorResponse}";
                }
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Error translating with OpenAI: {ex.Message}";
            }

            return result;
        }

        private string CreateTranslationPrompt(string text, string sourceLanguage, string targetLanguage, string tone)
        {
            string prompt;

            if (sourceLanguage == "auto" || sourceLanguage == "Auto Detect")
            {
                prompt = $"Translate the following text to {targetLanguage}:\n\n{text}";
            }
            else
            {
                prompt = $"Translate the following text from {sourceLanguage} to {targetLanguage}:\n\n{text}";
            }

            // Add tone instructions if not neutral
            if (tone.ToLower() != "neutral")
            {
                switch (tone.ToLower())
                {
                    case "formal":
                        prompt += "\n\nUse a formal and professional tone in the translation.";
                        break;
                    case "casual":
                        prompt += "\n\nUse a casual and colloquial tone in the translation.";
                        break;
                    case "technical":
                        prompt += "\n\nUse precise technical terminology in the translation.";
                        break;
                    case "professional":
                        prompt += "\n\nUse a business-appropriate tone in the translation.";
                        break;
                }
            }

            return prompt;
        }

        // Clean up any formatting or extra text in the translation
        private string CleanTranslationOutput(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Remove any quotes that may wrap the text
            text = text.Trim('"', '\'', ' ', '\n', '\r');

            // Some models like to prefix with "Translation: " - remove that
            if (text.StartsWith("Translation:", StringComparison.OrdinalIgnoreCase))
            {
                text = text.Substring("Translation:".Length).Trim();
            }

            return text;
        }

        // Map language codes to names
        private string MapLanguageCodeToName(string code)
        {
            if (string.IsNullOrEmpty(code) || code == "auto") return "Auto Detect";

            switch (code.ToLower())
            {
                case "pt": return "Portuguese";
                case "en": return "English";
                case "it": return "Italian";
                case "es": return "Spanish";
                case "fr": return "French";
                case "de": return "German";
                default: return code;
            }
        }
    }
}
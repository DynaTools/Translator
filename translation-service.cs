using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;

namespace ClipboardTranslator
{
    /// <summary>
    /// Result from translation operation
    /// </summary>
    public class TranslationResult
    {
        /// <summary>
        /// Whether the translation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Translated text if successful
        /// </summary>
        public string TranslatedText { get; set; }

        /// <summary>
        /// Error message if not successful
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Detected source language code (if available)
        /// </summary>
        public string DetectedLanguage { get; set; }

        /// <summary>
        /// Constructor for successful translation
        /// </summary>
        public static TranslationResult CreateSuccess(string translatedText, string detectedLanguage = "")
        {
            return new TranslationResult
            {
                Success = true,
                TranslatedText = translatedText,
                DetectedLanguage = detectedLanguage
            };
        }

        /// <summary>
        /// Constructor for failed translation
        /// </summary>
        public static TranslationResult CreateError(string errorMessage)
        {
            return new TranslationResult
            {
                Success = false,
                ErrorMessage = errorMessage
            };
        }
    }

    /// <summary>
    /// Helper class for estimating token count in text
    /// </summary>
    public static class TextTokenizer
    {
        /// <summary>
        /// Estimates the number of tokens in a text
        /// Uses a simple approximation of 4 characters = 1 token
        /// </summary>
        public static int EstimateTokenCount(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            // Simple approximation: average of 4 characters per token
            // This is a rough estimate as actual tokenization depends on the model
            return (int)Math.Ceiling(text.Length / 4.0);
        }
    }

    /// <summary>
    /// Interface for translation services
    /// </summary>
    public interface ITranslationService
    {
        /// <summary>
        /// Sets the API key for the service
        /// </summary>
        void SetApiKey(string apiKey);

        /// <summary>
        /// Translates text from source language to target language
        /// </summary>
        Task<TranslationResult> TranslateAsync(string text, string sourceLanguage, string targetLanguage, string tone);
    }

    /// <summary>
    /// Translation service using Google Gemini API
    /// </summary>
    public class GeminiTranslationService : ITranslationService
    {
        private string apiKey;
        private readonly HttpClient httpClient;

        // Full language names to language codes mapping
        private readonly Dictionary<string, string> languageNameToCode = new Dictionary<string, string>
        {
            { "English", "en" },
            { "Portuguese", "pt" },
            { "Spanish", "es" },
            { "French", "fr" },
            { "German", "de" },
            { "Italian", "it" },
            { "Auto Detect", "auto" }
        };

        public GeminiTranslationService()
        {
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        public void SetApiKey(string apiKey)
        {
            this.apiKey = apiKey;
        }

        public async Task<TranslationResult> TranslateAsync(string text, string sourceLanguage, string targetLanguage, string tone)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(text))
                {
                    return TranslationResult.CreateError("Text is empty");
                }

                // Ensure we have proper language codes
                string sourceCode = EnsureLanguageCode(sourceLanguage);
                string targetCode = EnsureLanguageCode(targetLanguage);

                // Create the Google Gemini API request
                string apiUrl = string.IsNullOrEmpty(apiKey)
                    ? "https://generativelanguage.googleapis.com/v1/models/gemini-pro:generateContent?key=" + apiKey
                    : "https://generativelanguage.googleapis.com/v1/models/gemini-pro:generateContent?key=AIzaSyBSmH9LuGNHn9MSfZxpXjh9AXHzDqNIDpk"; // Free API key for limited usage

                // Build the prompt for translation with tone
                string prompt = BuildTranslationPrompt(text, sourceCode, targetCode, tone);

                // Create the request body
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    }
                };

                // Serialize to JSON
                string jsonRequest = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                // Send request
                var response = await httpClient.PostAsync(apiUrl, content);
                string responseJson = await response.Content.ReadAsStringAsync();

                // Check for success
                if (!response.IsSuccessStatusCode)
                {
                    return TranslationResult.CreateError($"API error: {response.StatusCode}, {responseJson}");
                }

                // Parse the response
                using (JsonDocument doc = JsonDocument.Parse(responseJson))
                {
                    JsonElement root = doc.RootElement;

                    // Check for errors or broken response
                    if (root.TryGetProperty("error", out JsonElement errorElement))
                    {
                        string message = errorElement.TryGetProperty("message", out JsonElement messageElement)
                            ? messageElement.GetString()
                            : "Unknown API error";
                        return TranslationResult.CreateError(message);
                    }

                    // Extract translated text from the response
                    if (root.TryGetProperty("candidates", out JsonElement candidates) &&
                        candidates.GetArrayLength() > 0)
                    {
                        var firstCandidate = candidates[0];
                        if (firstCandidate.TryGetProperty("content", out JsonElement content1) &&
                            content1.TryGetProperty("parts", out JsonElement parts) &&
                            parts.GetArrayLength() > 0)
                        {
                            string translatedText = parts[0].GetProperty("text").GetString();

                            // Clean up any quotes or formatting from the AI response
                            translatedText = CleanTranslationResponse(translatedText);

                            return TranslationResult.CreateSuccess(translatedText, DetectLanguageName(sourceCode));
                        }
                    }

                    return TranslationResult.CreateError("Unable to parse translation from response");
                }
            }
            catch (Exception ex)
            {
                return TranslationResult.CreateError($"Translation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Builds a prompt for translation with the specified tone
        /// </summary>
        private string BuildTranslationPrompt(string text, string sourceCode, string targetCode, string tone)
        {
            // Get human-readable language names for clarity in the prompt
            string sourceName = DetectLanguageName(sourceCode);
            string targetName = DetectLanguageName(targetCode);

            if (sourceCode == "auto")
            {
                // For auto-detection
                return $"Please translate the following text into {targetName}. If you recognize the source language, mention it. Use a {tone} tone:\n\n{text}";
            }
            else
            {
                // When source language is specified
                return $"Please translate the following {sourceName} text into {targetName} using a {tone} tone. Only respond with the translation, no explanations:\n\n{text}";
            }
        }

        /// <summary>
        /// Cleans up the translation response from common formatting issues
        /// </summary>
        private string CleanTranslationResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return response;

            // Remove any explanation text that might be included at the beginning or end
            string[] commonPrefixes = new[]
            {
                "Here's the translation:",
                "Translation:",
                "Here is the translation:",
                "Translated text:",
                "The translation is:"
            };

            string cleanedResponse = response;

            foreach (var prefix in commonPrefixes)
            {
                if (cleanedResponse.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    cleanedResponse = cleanedResponse.Substring(prefix.Length).Trim();
                }
            }

            // Remove quotes if the text is wrapped in them
            if ((cleanedResponse.StartsWith("\"") && cleanedResponse.EndsWith("\"")) ||
                (cleanedResponse.StartsWith("'") && cleanedResponse.EndsWith("'")))
            {
                cleanedResponse = cleanedResponse.Substring(1, cleanedResponse.Length - 2);
            }

            // Remove detected language info if included
            int detectedIndex = cleanedResponse.IndexOf("Detected language:", StringComparison.OrdinalIgnoreCase);
            if (detectedIndex > 0)
            {
                cleanedResponse = cleanedResponse.Substring(0, detectedIndex).Trim();
            }

            return cleanedResponse.Trim();
        }

        /// <summary>
        /// Ensures we have a valid language code
        /// </summary>
        private string EnsureLanguageCode(string language)
        {
            // If already a valid 2-letter code, just return it
            if (language != null && language.Length == 2)
                return language.ToLower();

            // Check if it's a full language name and convert to code
            if (language != null && languageNameToCode.TryGetValue(language, out string code))
                return code;

            // Default fallbacks
            return language == "auto" ? "auto" : "en";
        }

        /// <summary>
        /// Gets the human-readable language name from a code
        /// </summary>
        private string DetectLanguageName(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
                return "Unknown";

            // Some common codes to names
            var codeToName = new Dictionary<string, string>
            {
                { "en", "English" },
                { "pt", "Portuguese" },
                { "es", "Spanish" },
                { "fr", "French" },
                { "de", "German" },
                { "it", "Italian" },
                { "auto", "Auto-detected" }
            };

            return codeToName.TryGetValue(languageCode.ToLower(), out string name) ? name : languageCode;
        }
    }

    /// <summary>
    /// Translation service using OpenAI API
    /// </summary>
    public class OpenAITranslationService : ITranslationService
    {
        private string apiKey;
        private readonly HttpClient httpClient;

        // Full language names to language codes mapping
        private readonly Dictionary<string, string> languageNameToCode = new Dictionary<string, string>
        {
            { "English", "en" },
            { "Portuguese", "pt" },
            { "Spanish", "es" },
            { "French", "fr" },
            { "German", "de" },
            { "Italian", "it" },
            { "Auto Detect", "auto" }
        };

        public OpenAITranslationService()
        {
            httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public void SetApiKey(string apiKey)
        {
            this.apiKey = apiKey;
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        }

        public async Task<TranslationResult> TranslateAsync(string text, string sourceLanguage, string targetLanguage, string tone)
        {
            try
            {
                // Validate inputs
                if (string.IsNullOrWhiteSpace(text))
                {
                    return TranslationResult.CreateError("Text is empty");
                }

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return TranslationResult.CreateError("OpenAI API key is not configured");
                }

                // Ensure we have proper language codes
                string sourceCode = EnsureLanguageCode(sourceLanguage);
                string targetCode = EnsureLanguageCode(targetLanguage);

                // Get language names for better prompts
                string targetName = DetectLanguageName(targetCode);

                // Create the OpenAI API request
                string apiUrl = "https://api.openai.com/v1/chat/completions";

                // Build the prompt for translation with tone
                string systemPrompt = "You are a professional translator. Translate text accurately while maintaining the original meaning and style. Only respond with the translation, no explanations or additional text.";
                string userPrompt = BuildTranslationPrompt(text, sourceCode, targetCode, tone);

                // Create the request body (using GPT-3.5 for cost efficiency)
                var requestBody = new
                {
                    model = "gpt-3.5-turbo",
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = userPrompt }
                    },
                    temperature = 0.3, // Lower temp for more accurate translations
                    max_tokens = 2048
                };

                // Serialize to JSON
                string jsonRequest = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");

                // Send request
                var response = await httpClient.PostAsync(apiUrl, content);
                string responseJson = await response.Content.ReadAsStringAsync();

                // Check for success
                if (!response.IsSuccessStatusCode)
                {
                    return TranslationResult.CreateError($"OpenAI API error: {response.StatusCode}, {responseJson}");
                }

                // Parse the response
                using (JsonDocument doc = JsonDocument.Parse(responseJson))
                {
                    JsonElement root = doc.RootElement;

                    // Check for errors
                    if (root.TryGetProperty("error", out JsonElement errorElement))
                    {
                        string message = errorElement.TryGetProperty("message", out JsonElement messageElement)
                            ? messageElement.GetString()
                            : "Unknown API error";
                        return TranslationResult.CreateError(message);
                    }

                    // Extract translated text from the response
                    if (root.TryGetProperty("choices", out JsonElement choices) &&
                        choices.GetArrayLength() > 0)
                    {
                        var firstChoice = choices[0];
                        if (firstChoice.TryGetProperty("message", out JsonElement message) &&
                            message.TryGetProperty("content", out JsonElement content1))
                        {
                            string translatedText = content1.GetString();

                            // Clean up any quotes or formatting from the AI response
                            translatedText = CleanTranslationResponse(translatedText);

                            // Try to extract detected language info if source was auto
                            string detectedLang = sourceCode == "auto" ? ExtractDetectedLanguage(translatedText) : DetectLanguageName(sourceCode);

                            return TranslationResult.CreateSuccess(translatedText, detectedLang);
                        }
                    }

                    return TranslationResult.CreateError("Unable to parse translation from response");
                }
            }
            catch (Exception ex)
            {
                return TranslationResult.CreateError($"Translation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Builds a prompt for translation with the specified tone
        /// </summary>
        private string BuildTranslationPrompt(string text, string sourceCode, string targetCode, string tone)
        {
            // Get human-readable language names for clarity in the prompt
            string sourceName = sourceCode == "auto" ? "auto-detected" : DetectLanguageName(sourceCode);
            string targetName = DetectLanguageName(targetCode);

            // Build a detailed prompt that emphasizes only outputting the translated text
            string prompt;

            if (sourceCode == "auto")
            {
                prompt = $"Translate the following text into {targetName} with a {tone} tone. Detect the source language. Only return the translated text without comments, explanations, or quotes:\n\n{text}";
            }
            else
            {
                prompt = $"Translate the following {sourceName} text into {targetName} with a {tone} tone. Only return the translated text without comments, explanations, or quotes:\n\n{text}";
            }

            return prompt;
        }

        /// <summary>
        /// Cleans up the translation response from common formatting issues
        /// </summary>
        private string CleanTranslationResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return response;

            // Remove any explanation text that might be included at the beginning or end
            string[] commonPrefixes = new[]
            {
                "Here's the translation:",
                "Translation:",
                "Here is the translation:",
                "Translated text:",
                "The translation is:"
            };

            string cleanedResponse = response;

            foreach (var prefix in commonPrefixes)
            {
                if (cleanedResponse.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    cleanedResponse = cleanedResponse.Substring(prefix.Length).Trim();
                }
            }

            // Remove quotes if the text is wrapped in them
            if ((cleanedResponse.StartsWith("\"") && cleanedResponse.EndsWith("\"")) ||
                (cleanedResponse.StartsWith("'") && cleanedResponse.EndsWith("'")))
            {
                cleanedResponse = cleanedResponse.Substring(1, cleanedResponse.Length - 2);
            }

            // Remove detected language info if included at the end
            int detectedIndex = cleanedResponse.LastIndexOf("(Detected language:", StringComparison.OrdinalIgnoreCase);
            if (detectedIndex > 0)
            {
                cleanedResponse = cleanedResponse.Substring(0, detectedIndex).Trim();
            }

            return cleanedResponse.Trim();
        }

        /// <summary>
        /// Try to extract detected language information from the response
        /// </summary>
        private string ExtractDetectedLanguage(string response)
        {
            // Look for detected language info in parentheses
            int startIndex = response.LastIndexOf("(Detected language:", StringComparison.OrdinalIgnoreCase);
            if (startIndex >= 0)
            {
                int endIndex = response.IndexOf(')', startIndex);
                if (endIndex > startIndex)
                {
                    string langInfo = response.Substring(startIndex + 19, endIndex - startIndex - 19).Trim();
                    return langInfo;
                }
            }

            // If no language info found, return unknown
            return "Auto-detected";
        }

        /// <summary>
        /// Ensures we have a valid language code
        /// </summary>
        private string EnsureLanguageCode(string language)
        {
            // If already a valid 2-letter code, just return it
            if (language != null && language.Length == 2)
                return language.ToLower();

            // Check if it's a full language name and convert to code
            if (language != null && languageNameToCode.TryGetValue(language, out string code))
                return code;

            // Default fallbacks
            return language == "auto" ? "auto" : "en";
        }

        /// <summary>
        /// Gets the human-readable language name from a code
        /// </summary>
        private string DetectLanguageName(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
                return "Unknown";

            // Some common codes to names
            var codeToName = new Dictionary<string, string>
            {
                { "en", "English" },
                { "pt", "Portuguese" },
                { "es", "Spanish" },
                { "fr", "French" },
                { "de", "German" },
                { "it", "Italian" },
                { "auto", "auto-detected" }
            };

            return codeToName.TryGetValue(languageCode.ToLower(), out string name) ? name : languageCode;
        }
    }
}
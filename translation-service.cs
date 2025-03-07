using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;
using System.Linq;
using System.Globalization;

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
    /// Language utilities for consistent language handling
    /// </summary>
    public static class LanguageUtils
    {
        // Common language codes and names
        private static readonly Dictionary<string, string> CodeToName = new Dictionary<string, string>
        {
            { "en", "English" },
            { "pt", "Portuguese" },
            { "es", "Spanish" },
            { "fr", "French" },
            { "de", "German" },
            { "it", "Italian" },
            { "auto", "Auto-detected" }
        };

        private static readonly Dictionary<string, string> NameToCode = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase)
        {
            { "English", "en" },
            { "Portuguese", "pt" },
            { "Português", "pt" },
            { "Spanish", "es" },
            { "Espanhol", "es" },
            { "French", "fr" },
            { "Francês", "fr" },
            { "German", "de" },
            { "Alemão", "de" },
            { "Italian", "it" },
            { "Italiano", "it" },
            { "Auto Detect", "auto" },
            { "Detecção automática", "auto" }
        };

        // Get language code from name or return the input if it's already a code
        public static string GetLanguageCode(string language)
        {
            if (string.IsNullOrEmpty(language))
                return "en";

            // If already a valid 2-letter code, just return it
            if (language.Length == 2)
                return language.ToLowerInvariant();

            // Check if it's a full language name and convert to code
            if (NameToCode.TryGetValue(language, out string code))
                return code;

            // Default fallbacks
            return language.Equals("auto", StringComparison.OrdinalIgnoreCase) ? "auto" : "en";
        }

        // Get language name from code
        public static string GetLanguageName(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
                return "Unknown";

            return CodeToName.TryGetValue(languageCode.ToLowerInvariant(), out string name) ? name : languageCode;
        }

        // Try to identify the language of a text by common words/characters
        public static bool IsLikelyInLanguage(string text, string targetLangCode)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(targetLangCode))
                return true; // Can't validate

            // Simple check based on common words/characters in each language
            // This is not foolproof but can catch obvious mismatches
            Dictionary<string, string[]> languageMarkers = new Dictionary<string, string[]>
            {
                { "en", new[] { "the", "and", "is", "in", "to", "of", "a" } },
                { "pt", new[] { "de", "e", "a", "o", "da", "em", "que", "para", "um", "uma" } },
                { "es", new[] { "el", "la", "de", "y", "en", "que", "un", "por", "con", "para" } },
                { "fr", new[] { "le", "la", "de", "et", "en", "un", "une", "du", "dans", "est" } },
                { "it", new[] { "il", "la", "di", "e", "che", "un", "una", "in", "per", "con" } },
                { "de", new[] { "der", "die", "das", "und", "in", "von", "mit", "für", "ist", "zu" } }
            };

            // Skip validation for languages we don't have markers for
            if (!languageMarkers.ContainsKey(targetLangCode))
                return true;

            // Get common words for target language
            string[] markers = languageMarkers[targetLangCode];

            // Create a simple tokenization of the text
            string[] words = text.ToLowerInvariant()
                .Replace(".", " ")
                .Replace(",", " ")
                .Replace("!", " ")
                .Replace("?", " ")
                .Replace("\"", " ")
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            // Count how many common words from the target language appear
            int matches = words.Count(w => markers.Contains(w));

            // If we have at least some matches or the text is very short, assume it's valid
            return matches > 0 || words.Length < 5;
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
        private int maxRetries = 2;

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
                string sourceCode = LanguageUtils.GetLanguageCode(sourceLanguage);
                string targetCode = LanguageUtils.GetLanguageCode(targetLanguage);

                // Create the Google Gemini API request
                string apiUrl = string.IsNullOrEmpty(apiKey)
                    ? "https://generativelanguage.googleapis.com/v1/models/gemini-pro:generateContent?key=" + apiKey
                    : "https://generativelanguage.googleapis.com/v1/models/gemini-pro:generateContent?key=AIzaSyBSmH9LuGNHn9MSfZxpXjh9AXHzDqNIDpk"; // Free API key for limited usage

                // Attempt translation with retries for validation
                for (int attempt = 0; attempt <= maxRetries; attempt++)
                {
                    // Build the prompt for translation with tone
                    string prompt = BuildTranslationPrompt(text, sourceCode, targetCode, tone, attempt > 0);

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
                        },
                        // Lower temperature for more consistent translations
                        generationConfig = new
                        {
                            temperature = 0.1
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
                        if (attempt == maxRetries)
                        {
                            return TranslationResult.CreateError($"API error: {response.StatusCode}, {responseJson}");
                        }
                        continue; // Try again
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

                            if (attempt == maxRetries)
                            {
                                return TranslationResult.CreateError(message);
                            }
                            continue; // Try again
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

                                // Validate the language of the response
                                if (LanguageUtils.IsLikelyInLanguage(translatedText, targetCode) || attempt == maxRetries)
                                {
                                    string detectedLang = sourceCode == "auto"
                                        ? ExtractDetectedLanguage(translatedText)
                                        : LanguageUtils.GetLanguageName(sourceCode);

                                    return TranslationResult.CreateSuccess(translatedText, detectedLang);
                                }

                                // If validation fails and we have retries left, try again with stronger prompt
                                continue;
                            }
                        }

                        if (attempt == maxRetries)
                        {
                            return TranslationResult.CreateError("Unable to parse translation from response");
                        }
                    }
                }

                return TranslationResult.CreateError("Failed to get valid translation after multiple attempts");
            }
            catch (Exception ex)
            {
                return TranslationResult.CreateError($"Translation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Builds a prompt for translation with the specified tone
        /// </summary>
        private string BuildTranslationPrompt(string text, string sourceCode, string targetCode, string tone, bool strongPrompt = false)
        {
            // Get human-readable language names for clarity in the prompt
            string sourceName = LanguageUtils.GetLanguageName(sourceCode);
            string targetName = LanguageUtils.GetLanguageName(targetCode);

            string promptBase;
            if (strongPrompt)
            {
                // Stronger prompt for retries
                if (sourceCode == "auto")
                {
                    promptBase = $"IMPORTANT: You are a professional translator. Translate this text ONLY into {targetName}. " +
                        $"Your ENTIRE response must ONLY be in {targetName}. Use a {tone} tone. " +
                        $"DO NOT include any explanations, notes, or content in any other language:\n\n{text}";
                }
                else
                {
                    promptBase = $"IMPORTANT: You are a professional translator. Translate this {sourceName} text ONLY into {targetName}. " +
                        $"Your ENTIRE response must ONLY be in {targetName}. Use a {tone} tone. " +
                        $"DO NOT include any explanations, notes, or content in any other language:\n\n{text}";
                }
            }
            else
            {
                // Standard prompt
                if (sourceCode == "auto")
                {
                    promptBase = $"You are a professional translator. Translate the following text into {targetName} using a {tone} tone. " +
                        $"Respond ONLY with the translation in {targetName}. No explanations or other text:\n\n{text}";
                }
                else
                {
                    promptBase = $"You are a professional translator. Translate the following {sourceName} text into {targetName} using a {tone} tone. " +
                        $"Respond ONLY with the translation in {targetName}. No explanations or other text:\n\n{text}";
                }
            }

            return promptBase;
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
                "The translation is:",
                "Tradução:",
                "Aqui está a tradução:",
                "Texto traduzido:"
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

            // Additional check for language info in other languages
            string[] languageInfoPatterns = new[]
            {
                "Idioma detectado:",
                "Língua detectada:",
                "Langue détectée:",
                "Lingua rilevata:",
                "Erkannte Sprache:"
            };

            foreach (var pattern in languageInfoPatterns)
            {
                detectedIndex = cleanedResponse.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                if (detectedIndex > 0)
                {
                    cleanedResponse = cleanedResponse.Substring(0, detectedIndex).Trim();
                    break;
                }
            }

            return cleanedResponse.Trim();
        }

        /// <summary>
        /// Try to extract detected language information from the response
        /// </summary>
        private string ExtractDetectedLanguage(string response)
        {
            // Try to find language detection info in various formats and languages
            Dictionary<string, string> detectionPatterns = new Dictionary<string, string>
            {
                { "Detected language:", "en" },
                { "Idioma detectado:", "es" },
                { "Língua detectada:", "pt" },
                { "Langue détectée:", "fr" },
                { "Lingua rilevata:", "it" },
                { "Erkannte Sprache:", "de" }
            };

            foreach (var pattern in detectionPatterns)
            {
                int startIndex = response.IndexOf(pattern.Key, StringComparison.OrdinalIgnoreCase);
                if (startIndex >= 0)
                {
                    // Extract the text after the pattern
                    string remainingText = response.Substring(startIndex + pattern.Key.Length).Trim();
                    // Take the first word or until a punctuation
                    int endIndex = remainingText.IndexOfAny(new[] { '.', ',', ')', '(', '\r', '\n' });
                    string langInfo = endIndex > 0
                        ? remainingText.Substring(0, endIndex).Trim()
                        : remainingText;

                    return langInfo;
                }
            }

            // If no language info found, return auto-detected
            return "Auto-detected";
        }
    }

    /// <summary>
    /// Translation service using OpenAI API
    /// </summary>
    public class OpenAITranslationService : ITranslationService
    {
        private string apiKey;
        private readonly HttpClient httpClient;
        private int maxRetries = 2;

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
                string sourceCode = LanguageUtils.GetLanguageCode(sourceLanguage);
                string targetCode = LanguageUtils.GetLanguageCode(targetLanguage);

                // Create the OpenAI API request
                string apiUrl = "https://api.openai.com/v1/chat/completions";

                // Attempt translation with retries for validation
                for (int attempt = 0; attempt <= maxRetries; attempt++)
                {
                    // Build the prompts for translation with tone
                    string systemPrompt = BuildSystemPrompt(sourceCode, targetCode, attempt > 0);
                    string userPrompt = BuildUserPrompt(text, sourceCode, targetCode, tone, attempt > 0);

                    // Create the request body (using GPT-3.5 for cost efficiency)
                    var requestBody = new
                    {
                        model = "gpt-3.5-turbo",
                        messages = new[]
                        {
                            new { role = "system", content = systemPrompt },
                            new { role = "user", content = userPrompt }
                        },
                        temperature = 0.1, // Lower temp for more accurate translations
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
                        if (attempt == maxRetries)
                        {
                            return TranslationResult.CreateError($"OpenAI API error: {response.StatusCode}, {responseJson}");
                        }
                        continue; // Try again
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

                            if (attempt == maxRetries)
                            {
                                return TranslationResult.CreateError(message);
                            }
                            continue; // Try again
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

                                // Validate the language of the response
                                if (LanguageUtils.IsLikelyInLanguage(translatedText, targetCode) || attempt == maxRetries)
                                {
                                    // Try to extract detected language info if source was auto
                                    string detectedLang = sourceCode == "auto"
                                        ? ExtractDetectedLanguage(translatedText)
                                        : LanguageUtils.GetLanguageName(sourceCode);

                                    return TranslationResult.CreateSuccess(translatedText, detectedLang);
                                }

                                // If validation fails and we have retries left, try again with stronger prompt
                                continue;
                            }
                        }

                        if (attempt == maxRetries)
                        {
                            return TranslationResult.CreateError("Unable to parse translation from response");
                        }
                    }
                }

                return TranslationResult.CreateError("Failed to get valid translation after multiple attempts");
            }
            catch (Exception ex)
            {
                return TranslationResult.CreateError($"Translation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Builds a system prompt for translation
        /// </summary>
        private string BuildSystemPrompt(string sourceCode, string targetCode, bool strongPrompt = false)
        {
            string targetName = LanguageUtils.GetLanguageName(targetCode);

            if (strongPrompt)
            {
                return $"IMPORTANT INSTRUCTION: You are a professional translator. Your task is to translate text STRICTLY into {targetName} ONLY. " +
                    $"Your ENTIRE response must be in {targetName} language ONLY. " +
                    $"DO NOT add any explanation, notes, or text in any other language. " +
                    $"DO NOT add information about detected language. " +
                    $"ONLY produce the translation in {targetName}, nothing else.";
            }
            else
            {
                return $"You are a professional translator specialized in translating into {targetName}. " +
                    $"Translate text accurately while maintaining the original meaning and style. " +
                    $"Only respond with the translation in {targetName}, no explanations or additional text.";
            }
        }

        /// <summary>
        /// Builds a user prompt for translation with the specified tone
        /// </summary>
        private string BuildUserPrompt(string text, string sourceCode, string targetCode, string tone, bool strongPrompt = false)
        {
            // Get human-readable language names for clarity in the prompt
            string sourceName = sourceCode == "auto" ? "auto-detected" : LanguageUtils.GetLanguageName(sourceCode);
            string targetName = LanguageUtils.GetLanguageName(targetCode);

            // Build a detailed prompt that emphasizes only outputting the translated text
            string prompt;

            if (strongPrompt)
            {
                if (sourceCode == "auto")
                {
                    prompt = $"IMPORTANT: Translate this text STRICTLY into {targetName} with a {tone} tone. " +
                        $"Your response must ONLY be the {targetName} translation. " +
                        $"DO NOT include any explanations, comments, detected language info, or quotes. " +
                        $"ONLY THE TRANSLATION IN {targetName.ToUpperInvariant()}:\n\n{text}";
                }
                else
                {
                    prompt = $"IMPORTANT: Translate this {sourceName} text STRICTLY into {targetName} with a {tone} tone. " +
                        $"Your response must ONLY be the {targetName} translation. " +
                        $"DO NOT include any explanations, comments, or quotes. " +
                        $"ONLY THE TRANSLATION IN {targetName.ToUpperInvariant()}:\n\n{text}";
                }
            }
            else
            {
                if (sourceCode == "auto")
                {
                    prompt = $"Translate this text into {targetName} with a {tone} tone. " +
                        $"Only return the translated text without comments, explanations, or quotes:\n\n{text}";
                }
                else
                {
                    prompt = $"Translate this {sourceName} text into {targetName} with a {tone} tone. " +
                        $"Only return the translated text without comments, explanations, or quotes:\n\n{text}";
                }
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
                "The translation is:",
                "Tradução:",
                "Aqui está a tradução:",
                "Texto traduzido:"
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

            // Additional check for language info in other languages
            string[] languageInfoPatterns = new[]
            {
                "(Idioma detectado:",
                "(Língua detectada:",
                "(Langue détectée:",
                "(Lingua rilevata:",
                "(Erkannte Sprache:"
            };

            foreach (var pattern in languageInfoPatterns)
            {
                detectedIndex = cleanedResponse.LastIndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                if (detectedIndex > 0)
                {
                    cleanedResponse = cleanedResponse.Substring(0, detectedIndex).Trim();
                    break;
                }
            }

            return cleanedResponse.Trim();
        }

        /// <summary>
        /// Try to extract detected language information from the response
        /// </summary>
        private string ExtractDetectedLanguage(string response)
        {
            // Try to find language detection info in various formats and languages
            Dictionary<string, string> detectionPatterns = new Dictionary<string, string>
            {
                { "(Detected language:", "en" },
                { "(Idioma detectado:", "es" },
                { "(Língua detectada:", "pt" },
                { "(Langue détectée:", "fr" },
                { "(Lingua rilevata:", "it" },
                { "(Erkannte Sprache:", "de" }
            };

            foreach (var pattern in detectionPatterns)
            {
                int startIndex = response.LastIndexOf(pattern.Key, StringComparison.OrdinalIgnoreCase);
                if (startIndex >= 0)
                {
                    // Extract the text after the pattern
                    string remainingText = response.Substring(startIndex + pattern.Key.Length).Trim();
                    // Take the first word or until a punctuation
                    int endIndex = remainingText.IndexOfAny(new[] { '.', ',', ')', '(', '\r', '\n' });
                    string langInfo = endIndex > 0
                        ? remainingText.Substring(0, endIndex).Trim()
                        : remainingText;

                    // Clean up parenthesis
                    if (langInfo.EndsWith(")"))
                    {
                        langInfo = langInfo.Substring(0, langInfo.Length - 1).Trim();
                    }

                    return langInfo;
                }
            }

            // If no language info found, return auto-detected
            return "Auto-detected";
        }
    }
}
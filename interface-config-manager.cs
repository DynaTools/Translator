using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ClipboardTranslator;

namespace Translator
{
    // Partial class for interface configuration methods
    public partial class MainWindow
    {
        private void ConfigureInterface()
        {
            // Configure language combo boxes
            ConfigureLanguageComboBoxes();

            // Configure tone combo box
            ConfigureToneComboBox();

            // Configure AI parameters
            ConfigureAIParameters();

            // Configure statistics
            UpdateStatistics();
        }

        private void ConfigureLanguageComboBoxes()
        {
            // Clear and add items to source language combo box
            SourceLanguage.Items.Clear();
            SourceLanguage.Items.Add(new ComboBoxItem { Content = "Auto Detect", Tag = "auto" });
            SourceLanguage.Items.Add(new ComboBoxItem { Content = "Portuguese", Tag = "pt" });
            SourceLanguage.Items.Add(new ComboBoxItem { Content = "English", Tag = "en" });
            SourceLanguage.Items.Add(new ComboBoxItem { Content = "Italian", Tag = "it" });
            SourceLanguage.Items.Add(new ComboBoxItem { Content = "Spanish", Tag = "es" });
            SourceLanguage.Items.Add(new ComboBoxItem { Content = "French", Tag = "fr" });
            SourceLanguage.Items.Add(new ComboBoxItem { Content = "German", Tag = "de" });

            // Clear and add items to target language combo box
            TargetLanguage.Items.Clear();
            TargetLanguage.Items.Add(new ComboBoxItem { Content = "Portuguese", Tag = "pt" });
            TargetLanguage.Items.Add(new ComboBoxItem { Content = "English", Tag = "en" });
            TargetLanguage.Items.Add(new ComboBoxItem { Content = "Italian", Tag = "it" });
            TargetLanguage.Items.Add(new ComboBoxItem { Content = "Spanish", Tag = "es" });
            TargetLanguage.Items.Add(new ComboBoxItem { Content = "French", Tag = "fr" });
            TargetLanguage.Items.Add(new ComboBoxItem { Content = "German", Tag = "de" });

            // Select default languages from settings
            SelectDefaultLanguages();
        }

        private void SelectDefaultLanguages()
        {
            // Find and select the source language based on settings
            foreach (ComboBoxItem item in SourceLanguage.Items)
            {
                if (item.Content.ToString() == settings.DefaultSourceLanguage)
                {
                    SourceLanguage.SelectedItem = item;
                    break;
                }
            }

            // If no item was selected, select the first one
            if (SourceLanguage.SelectedIndex == -1 && SourceLanguage.Items.Count > 0)
            {
                SourceLanguage.SelectedIndex = 0;
            }

            // Find and select the target language based on settings
            foreach (ComboBoxItem item in TargetLanguage.Items)
            {
                if (item.Content.ToString() == settings.DefaultTargetLanguage)
                {
                    TargetLanguage.SelectedItem = item;
                    break;
                }
            }

            // If no item was selected, select English or the first one
            if (TargetLanguage.SelectedIndex == -1)
            {
                bool foundEnglish = false;

                foreach (ComboBoxItem item in TargetLanguage.Items)
                {
                    if (item.Content.ToString() == "English")
                    {
                        TargetLanguage.SelectedItem = item;
                        foundEnglish = true;
                        break;
                    }
                }

                if (!foundEnglish && TargetLanguage.Items.Count > 0)
                {
                    TargetLanguage.SelectedIndex = 0;
                }
            }
        }

        private void ConfigureToneComboBox()
        {
            // Clear and add items to tone combo box
            TranslationTone.Items.Clear();
            TranslationTone.Items.Add(new ComboBoxItem { Content = "Neutral" });
            TranslationTone.Items.Add(new ComboBoxItem { Content = "Formal" });
            TranslationTone.Items.Add(new ComboBoxItem { Content = "Casual" });
            TranslationTone.Items.Add(new ComboBoxItem { Content = "Technical" });
            TranslationTone.Items.Add(new ComboBoxItem { Content = "Professional" });

            // Select default tone from settings
            foreach (ComboBoxItem item in TranslationTone.Items)
            {
                if (item.Content.ToString() == settings.DefaultTone)
                {
                    TranslationTone.SelectedItem = item;
                    break;
                }
            }

            // If no item was selected, select the first one
            if (TranslationTone.SelectedIndex == -1 && TranslationTone.Items.Count > 0)
            {
                TranslationTone.SelectedIndex = 0;
            }
        }

        private void ConfigureAIParameters()
        {
            // Set AI parameter sliders
            TemperatureSlider.Value = aiParameters.Temperature;
            TopPSlider.Value = aiParameters.TopP;
            FrequencyPenaltySlider.Value = aiParameters.FrequencyPenalty;
            PresencePenaltySlider.Value = aiParameters.PresencePenalty;

            // Configure model version selector
            ModelVersionSelector.Items.Clear();
            ModelVersionSelector.Items.Add(new ComboBoxItem { Content = "Default (Latest)" });
            ModelVersionSelector.Items.Add(new ComboBoxItem { Content = "GPT-3.5 Turbo" });
            ModelVersionSelector.Items.Add(new ComboBoxItem { Content = "GPT-4" });
            ModelVersionSelector.Items.Add(new ComboBoxItem { Content = "Gemini Pro" });
            ModelVersionSelector.Items.Add(new ComboBoxItem { Content = "Gemini Flash" });

            // Select model version from settings
            bool foundModel = false;
            if (!string.IsNullOrEmpty(aiParameters.ModelVersion) && aiParameters.ModelVersion != "Default")
            {
                foreach (ComboBoxItem item in ModelVersionSelector.Items)
                {
                    if (item.Content.ToString() == aiParameters.ModelVersion)
                    {
                        ModelVersionSelector.SelectedItem = item;
                        EnableModelVersionSelector.IsChecked = true;
                        foundModel = true;
                        break;
                    }
                }
            }

            if (!foundModel)
            {
                ModelVersionSelector.SelectedIndex = 0;
                EnableModelVersionSelector.IsChecked = false;
            }
        }

        internal void UpdateStatistics()
        {
            // Update the statistics display
            TranslationCount.Text = settings.TranslationsToday.ToString();
            LastDetectedLanguage.Text = lastDetectedLanguage;
        }

        private void CheckDailyStatisticsReset()
        {
            // Check if it's a new day and reset statistics if needed
            DateTime now = DateTime.Now;
            DateTime lastDate = settings.LastTranslationDate;

            if (lastDate.Date < now.Date)
            {
                // It's a new day, reset the counter
                settings.TranslationsToday = 0;
                settings.LastTranslationDate = now;

                // Save the settings
                ConfigManager.SaveSettings(settings);

                // Update the statistics display
                UpdateStatistics();
            }
        }

        // Event handlers for language and tone changes
        public void LanguageChanged(object sender, SelectionChangedEventArgs e)
        {
            // Clear translation cache when language changes
            translationCache.Clear();

            // Save settings
            SaveSettings();
        }

        public void ToneChanged(object sender, SelectionChangedEventArgs e)
        {
            // Clear translation cache when tone changes
            translationCache.Clear();

            // Save settings
            SaveSettings();
        }
    }
}
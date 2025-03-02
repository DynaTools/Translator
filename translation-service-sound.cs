using System;
using System.IO;
using System.Media;
using System.Reflection;
using System.Threading.Tasks;

namespace Translator
{
    public static class SoundService
    {
        private static SoundPlayer translationCompleteSoundPlayer;
        private static bool isPlaying = false;
        private static readonly object lockObject = new object();

        static SoundService()
        {
            try
            {
                // Use system sound as default for simplicity - can be replaced with custom sound later
                translationCompleteSoundPlayer = new SoundPlayer(SystemSounds.Exclamation.Location);
            }
            catch
            {
                translationCompleteSoundPlayer = null;
            }
        }

        public static void PlayTranslationCompleteSound()
        {
            lock (lockObject)
            {
                if (isPlaying) return; // Prevent multiple playbacks

                try
                {
                    isPlaying = true;

                    if (translationCompleteSoundPlayer != null)
                    {
                        translationCompleteSoundPlayer.Play();

                        // Reset the flag after a delay
                        Task.Delay(1000).ContinueWith(_ => { isPlaying = false; });
                    }
                    else
                    {
                        // Use a gentle system sound that won't be confused with an error
                        SystemSounds.Question.Play();
                        isPlaying = false;
                    }
                }
                catch
                {
                    isPlaying = false;
                }
            }
        }
    }
}
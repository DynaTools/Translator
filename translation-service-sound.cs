using System;
using System.Media;

namespace Translator
{
    public static class SoundService
    {
        private static bool isPlaying = false;
        private static readonly object lockObject = new object();

        public static void PlayTranslationCompleteSound()
        {
            lock (lockObject)
            {
                if (isPlaying) return; // Prevent multiple playbacks

                try
                {
                    isPlaying = true;

                    // Use system sound directly instead of trying to access a Location property
                    SystemSounds.Asterisk.Play();

                    // Reset the flag after a short delay via System.Threading.Timer
                    System.Threading.Timer timer = null;
                    timer = new System.Threading.Timer((state) =>
                    {
                        isPlaying = false;
                        timer?.Dispose();
                    }, null, 1000, System.Threading.Timeout.Infinite);
                }
                catch
                {
                    isPlaying = false;
                }
            }
        }
    }
}
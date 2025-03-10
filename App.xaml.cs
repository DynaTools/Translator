using System;
using System.Windows;
using System.Windows.Interop;
using System.Drawing;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Translator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // The application icon is set in the project file via ApplicationIcon property
            // and will automatically be used for the taskbar

            // We can still ensure the icon is loaded for other uses like system tray or window icon
            try
            {
                // Add the icon to application resources for potential use
                var iconUri = new Uri("pack://application:,,,/Resources/translate_icon.ico", UriKind.Absolute);
                this.Resources.Add("AppIcon", new BitmapImage(iconUri));

                // When the main window is loaded, we'll also set its icon explicitly
                this.MainWindow.Loaded += (sender, args) =>
                {
                    if (this.MainWindow.Icon == null)
                    {
                        this.MainWindow.Icon = new BitmapImage(iconUri);
                    }
                };
            }
            catch (Exception ex)
            {
                // Log any icon loading errors
                System.Diagnostics.Debug.WriteLine($"Error handling application icon: {ex.Message}");
            }
        }
    }
}
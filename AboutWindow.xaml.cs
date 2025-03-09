using System.Reflection;
using System.Windows;

namespace Translator
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();

            // Get assembly version
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"Version {version.Major}.{version.Minor}.{version.Build}";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            // Open link in browser
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/seuusuario/ClipboardTranslator",
                UseShellExecute = true
            });
        }
    }
}
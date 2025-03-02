using System.Reflection;
using System.Windows;

namespace ClipboardTranslator
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();

            // Obter versão do assembly
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"Versão {version.Major}.{version.Minor}.{version.Build}";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            // Abrir link no navegador
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/seuusuario/ClipboardTranslator",
                UseShellExecute = true
            });
        }
    }
}
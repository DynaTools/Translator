using System;
using System.Windows;

namespace Translator
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            App application = new App();
            application.InitializeComponent();
            application.Run();
        }
    }
}
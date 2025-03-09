using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Threading;
using System.Threading.Tasks;

namespace ClipboardTranslator
{
    public class ClipboardMonitor : IDisposable
    {
        // Delegado para o evento de alteração do clipboard
        public delegate void ClipboardChangedEventHandler();
        public event ClipboardChangedEventHandler ClipboardChanged;

        private IntPtr nextClipboardViewer;
        private Window targetWindow;
        private HwndSource hwndSource;

        // Prevenir chamadas de eventos muito rápidas
        private DateTime lastEventTime = DateTime.MinValue;
        private readonly TimeSpan minEventInterval = TimeSpan.FromMilliseconds(300);

        // Semáforo para evitar concorrência
        private readonly SemaphoreSlim eventSemaphore = new SemaphoreSlim(1, 1);

        // API Win32 para monitorar a área de transferência
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

        // Constantes para mensagens do Windows
        private const int WM_DRAWCLIPBOARD = 0x0308;
        private const int WM_CHANGECBCHAIN = 0x030D;

        public ClipboardMonitor(Window window)
        {
            targetWindow = window;
        }

        public void Initialize(IntPtr windowHandle)
        {
            // Obter o handle da janela
            hwndSource = HwndSource.FromHwnd(windowHandle);
            hwndSource?.AddHook(WndProc);

            // Registrar-se na cadeia de visualização do clipboard
            nextClipboardViewer = SetClipboardViewer(hwndSource.Handle);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_DRAWCLIPBOARD:
                    // Conteúdo do clipboard foi alterado
                    // Usando Task.Run para executar de forma assíncrona sem bloquear
                    Task.Run(() => HandleClipboardChange());

                    // Passar a mensagem para o próximo visualizador
                    SendMessage(nextClipboardViewer, msg, wParam, lParam);
                    break;

                case WM_CHANGECBCHAIN:
                    // Um visualizador foi removido da cadeia
                    if (wParam == nextClipboardViewer)
                    {
                        // O próximo visualizador foi removido, atualize a referência
                        nextClipboardViewer = lParam;
                    }
                    else if (nextClipboardViewer != IntPtr.Zero)
                    {
                        // Passa a mensagem para o próximo visualizador
                        SendMessage(nextClipboardViewer, msg, wParam, lParam);
                    }
                    break;
            }

            return IntPtr.Zero;
        }

        private async Task HandleClipboardChange()
        {
            // Verificar se não estamos processando eventos muito rápido
            DateTime now = DateTime.Now;
            if (now - lastEventTime < minEventInterval)
            {
                return;
            }

            // Tentar adquirir o semáforo para evitar chamadas concorrentes
            if (!await eventSemaphore.WaitAsync(100))
            {
                return;
            }

            try
            {
                lastEventTime = now;

                // Chamar o evento em um thread separado via Dispatcher
                await targetWindow.Dispatcher.InvokeAsync(() =>
                {
                    ClipboardChanged?.Invoke();
                });
            }
            finally
            {
                eventSemaphore.Release();
            }
        }

        public void SetMonitoringEnabled(bool enabled)
        {
            // Se estiver desabilitando, remova temporariamente o hook
            if (!enabled && hwndSource != null)
            {
                hwndSource.RemoveHook(WndProc);
            }
            // Se estiver habilitando, adicione o hook novamente
            else if (enabled && hwndSource != null)
            {
                hwndSource.AddHook(WndProc);
            }
        }

        public void Dispose()
        {
            if (hwndSource != null)
            {
                // Remover-se da cadeia de visualização do clipboard
                if (hwndSource.Handle != IntPtr.Zero && nextClipboardViewer != IntPtr.Zero)
                {
                    ChangeClipboardChain(hwndSource.Handle, nextClipboardViewer);
                }

                // Remover o hook
                hwndSource.RemoveHook(WndProc);
                hwndSource = null;
            }

            eventSemaphore.Dispose();
        }
    }
}
using System;
using System.Runtime.InteropServices;

namespace FluentAurora.Controls;

public static class MessageBox
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    public static void ShowError(string title, string message, Exception? exception = null)
    {
        string fullMessage = message;
        if (exception != null)
        {
            fullMessage += $"\n\nError: {exception.Message}\n\nThe full error is visible in the log file. Press OK to exit.";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            MessageBoxW(IntPtr.Zero, fullMessage, title, 0x00000010);
        }
    }
}
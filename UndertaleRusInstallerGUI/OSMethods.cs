using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace UndertaleRusInstallerGUI
{
    public static class OSMethods
    {
        // Windows
        [DllImport("tinyfiledialogs", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int tinyfd_messageBoxW(string aTitle, string aMessage, string aDialogType, string aIconType, int aDefaultButton);
        [DllImport("tinyfiledialogs", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr tinyfd_openFileDialogW(string aTitle, string aDefaultPathAndFile, int aNumOfFilterPatterns, string[] aFilterPatterns, string aSingleFilterDescription, int aAllowMultipleSelects);
        [DllImport("tinyfiledialogs", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr tinyfd_selectFolderDialogW(string aTitle, string aDefaultPath = "");

        // Linux/MacOS
        [DllImport("tinyfiledialogs", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern int tinyfd_messageBox(string aTitle, string aMessage, string aDialogType, string aIconType, int aDefaultButton);
        [DllImport("tinyfiledialogs", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr tinyfd_openFileDialog(string aTitle, string aDefaultPathAndFile, int aNumOfFilterPatterns, string[] aFilterPatterns, string aSingleFilterDescription, int aAllowMultipleSelects);
        [DllImport("tinyfiledialogs", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr tinyfd_selectFolderDialog(string aTitle, string aDefaultPath = "");

        public static int MessageBox(string title, string message, string dialogType, string iconType, int defaultButton)
        {
            message = message.Replace('"', '“').Replace('\'', '‘').Replace('`', '‘');

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return tinyfd_messageBoxW(title, message, dialogType, iconType, defaultButton);
            }
            else
            {
                message = message.Replace("\\", "\\\\\\\\");
                return tinyfd_messageBox(title, message, dialogType, iconType, defaultButton);
            }
        }

        public static string OpenFileDialog(string title, string defaultPathAndFile, int numOfFilterPatterns, string[] filterPatterns, string singleFilterDescription, int allowMultipleSelects)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Marshal.PtrToStringUni(tinyfd_openFileDialogW(title, defaultPathAndFile, numOfFilterPatterns, filterPatterns, singleFilterDescription, allowMultipleSelects));
            }
            else
            {
                return Marshal.PtrToStringAnsi(tinyfd_openFileDialog(title, defaultPathAndFile, numOfFilterPatterns, filterPatterns, singleFilterDescription, allowMultipleSelects));
            }
        }

        public static string SelectFolderDialog(string title, string defaultPath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Marshal.PtrToStringUni(tinyfd_selectFolderDialogW(title, defaultPath));
            }
            else
            {
                return Marshal.PtrToStringAnsi(tinyfd_selectFolderDialog(title, defaultPath));
            }
        }


        public static bool OpenUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                try
                {
                    // hack because of this: https://github.com/dotnet/corefx/issues/10361
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        url = url.Replace("&", "^&");
                        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        Process.Start("xdg-open", url);
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        Process.Start("open", url);
                    }
                    else
                    {
                        throw;
                    }
                }
                catch
                {
                    return false;
                }
            }

            return true;
        }
    }
}

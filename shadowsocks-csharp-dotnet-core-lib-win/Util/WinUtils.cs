using Microsoft.Win32;
using NLog;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Shadowsocks.Std.Win.Util
{

    public enum WindowsThemeMode { Dark, Light }

    public static class WinUtils
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        #region Windows Theme

        // Support on Windows 10 1903+
        public static WindowsThemeMode GetWindows10SystemThemeSetting(bool isVerbose)
        {
            WindowsThemeMode themeMode = WindowsThemeMode.Dark;
            try
            {
                RegistryKey REG_ThemesPersonalize = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", false);

                if (REG_ThemesPersonalize.GetValue("SystemUsesLightTheme") != null)
                {
                    if ((int)(REG_ThemesPersonalize.GetValue("SystemUsesLightTheme")) == 0) // 0:dark mode, 1:light mode
                        themeMode = WindowsThemeMode.Dark;
                    else
                        themeMode = WindowsThemeMode.Light;
                }
                else
                {
                    throw new Exception("Reg-Value SystemUsesLightTheme not found.");
                }
            }
            catch (Exception)
            {
                if (isVerbose)
                {
                    _logger.Info("Cannot get Windows 10 system theme mode, return default value 0 (dark mode).");
                }
            }
            return themeMode;
        }

        #endregion


        #region Memory

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessWorkingSetSize(IntPtr process, UIntPtr minimumWorkingSetSize, UIntPtr maximumWorkingSetSize);

        public static void ReleaseMemory(bool removePages)
        {
            // release any unused pages
            // making the numbers look good in task manager
            // this is totally nonsense in programming
            // but good for those users who care
            // making them happier with their everyday life
            // which is part of user experience
            GC.Collect(GC.MaxGeneration);
            GC.WaitForPendingFinalizers();
            if (removePages)
            {
                // as some users have pointed out
                // removing pages from working set will cause some IO
                // which lowered user experience for another group of users
                //
                // so we do 2 more things here to satisfy them:
                // 1. only remove pages once when configuration is changed
                // 2. add more comments here to tell users that calling
                //    this function will not be more frequent than
                //    IM apps writing chat logs, or web browsers writing cache files
                //    if they're so concerned about their disk, they should
                //    uninstall all IM apps and web browsers
                //
                // please open an issue if you're worried about anything else in your computer
                // no matter it's GPU performance, monitor contrast, audio fidelity
                // or anything else in the task manager
                // we'll do as much as we can to help you
                //
                // just kidding
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle,
                                         (UIntPtr)0xFFFFFFFF,
                                         (UIntPtr)0xFFFFFFFF);
            }
        }

        #endregion


        #region registry

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<挂起>")]
        public static RegistryKey OpenRegKey(string name, bool writable, RegistryHive hive = RegistryHive.CurrentUser)
        {
            // we are building x86 binary for both x86 and x64, which will
            // cause problem when opening registry key
            // detect operating system instead of CPU
            if (String.IsNullOrEmpty(name)) throw new ArgumentException(nameof(name));
            try
            {
                return RegistryKey.OpenBaseKey(hive, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32).OpenSubKey(name, writable);
            }
            catch (ArgumentException ae)
            {
                // TODO 待实现: 抽出 MessageBox 组件
                // MessageBox.Show("OpenRegKey: " + ae.ToString());
            }
            catch (Exception e)
            {
                LogUsefulException(e);
            }

            return null;
        }

        // See: https://msdn.microsoft.com/en-us/library/hh925568(v=vs.110).aspx
        public static bool IsSupportedRuntimeVersion()
        {
            // TODO 需要重新完善

            /*
             * +-----------------------------------------------------------------+----------------------------+
             * | Version                                                         | Value of the Release DWORD |
             * +-----------------------------------------------------------------+----------------------------+
             * | .NET Framework 4.6.2 installed on Windows 10 Anniversary Update | 394802                     |
             * | .NET Framework 4.6.2 installed on all other Windows OS versions | 394806                     |
             * +-----------------------------------------------------------------+----------------------------+
             */
            const int minSupportedRelease = 394802;

            const string subkey = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\";
            using (var ndpKey = OpenRegKey(subkey, false, RegistryHive.LocalMachine))
            {
                if (ndpKey?.GetValue("Release") != null && (int)ndpKey.GetValue("Release") >= minSupportedRelease)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

using Microsoft.Win32;

using NLog;

using Shadowsocks.Model;
using Shadowsocks.Std.SystemProxy;

namespace Shadowsocks.Std.Win.Util
{

    public enum WindowsThemeMode { Dark, Light }

    public interface IGetApplicationInfo
    {
        public string ExecutablePath();
    }

    public static class WinUtil
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private static string _tempPath = null;

        #region Get Temp Path

        // return path to store temporary files
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<挂起>")]
        public static string GetTempPath(IGetApplicationInfo applicationInfo)
        {
            if (_tempPath == null)
            {
                bool isPortableMode = Configuration.Load().portableMode;
                try
                {
                    if (isPortableMode)
                    {
                        _tempPath = Directory.CreateDirectory("ss_win_temp").FullName;
                        // don't use "/", it will fail when we call explorer /select xxx/ss_win_temp\xxx.log
                    }
                    else
                    {
                        _tempPath = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"Shadowsocks\\ss_win_temp_{applicationInfo.ExecutablePath().GetHashCode()}")).FullName;
                    }
                }
                catch (Exception e)
                {
                    _logger.Error(e);

                    ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                }
            }

            return _tempPath;
        }

        // return a full path with filename combined which pointed to the temporary directory
        public static string GetTempPath(string filename, IGetApplicationInfo applicationInfo) => Path.Combine(GetTempPath(applicationInfo), filename);

        #endregion


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


        #region Log

        public static void LogUsefulException(Exception e)
        {
            // just log useful exceptions, not all of them
            if (e is SocketException se)
            {
                if (se.SocketErrorCode == SocketError.ConnectionAborted)
                {
                    // closed by browser when sending
                    // normally happens when download is canceled or a tab is closed before page is loaded
                }
                else if (se.SocketErrorCode == SocketError.ConnectionReset)
                {
                    // received rst
                }
                else if (se.SocketErrorCode == SocketError.NotConnected)
                {
                    // The application tried to send or receive data, and the System.Net.Sockets.Socket is not connected.
                }
                else if (se.SocketErrorCode == SocketError.HostUnreachable)
                {
                    // There is no network route to the specified host.
                }
                else if (se.SocketErrorCode == SocketError.TimedOut)
                {
                    // The connection attempt timed out, or the connected host has failed to respond.
                }
                else
                {
                    _logger.Info(e);
                }
            }
            else if (e is ObjectDisposedException)
            {
            }
            else if (e is Win32Exception winex)
            {
                // Win32Exception (0x80004005): A 32 bit processes cannot access modules of a 64 bit process.
                if ((uint)winex.ErrorCode != 0x80004005)
                {
                    _logger.Info(e);
                }
            }
            else if (e is ProxyException pe)
            {
                switch (pe.Type)
                {
                    case ProxyExceptionType.FailToRun:
                    case ProxyExceptionType.QueryReturnMalformed:
                    case ProxyExceptionType.SysproxyExitError:
                        _logger.Error($"sysproxy - {pe.Type.ToString()}:{pe.Message}");
                        break;
                    case ProxyExceptionType.QueryReturnEmpty:
                    case ProxyExceptionType.Unspecific:
                        _logger.Error($"sysproxy - {pe.Type.ToString()}");
                        break;
                }
            }
            else
            {
                _logger.Info(e);
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

using NLog;
using Shadowsocks.Std.Model;
using Shadowsocks.Std.SystemProxy;
using System;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Runtime.ExceptionServices;
using System.Text;

namespace Shadowsocks.Std.Util
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "<挂起>")]
    public struct BandwidthScaleInfo
    {
        public float value;
        public long unit;
        public string unitName;

        public BandwidthScaleInfo(float value, string unitName, long unit)
        {
            this.value = value;
            this.unitName = unitName;
            this.unit = unit;
        }
    }

    public interface IGetApplicationInfo
    {
        public string ExecutablePath();
    }

    public static class Utils
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


        #region Bandwidth

        public static string FormatBandwidth(long n)
        {
            var result = GetBandwidthScale(n);
            return $"{result.value:0.##}{result.unitName}";
        }

        public static string FormatBytes(long bytes)
        {
            const long K = 1024L;
            const long M = K * 1024L;
            const long G = M * 1024L;
            const long T = G * 1024L;
            const long P = T * 1024L;
            const long E = P * 1024L;

            if (bytes >= P * 990)
                return (bytes / (double)E).ToString("F5") + "EiB";
            if (bytes >= T * 990)
                return (bytes / (double)P).ToString("F5") + "PiB";
            if (bytes >= G * 990)
                return (bytes / (double)T).ToString("F5") + "TiB";
            if (bytes >= M * 990)
            {
                return (bytes / (double)G).ToString("F4") + "GiB";
            }
            if (bytes >= M * 100)
            {
                return (bytes / (double)M).ToString("F1") + "MiB";
            }
            if (bytes >= M * 10)
            {
                return (bytes / (double)M).ToString("F2") + "MiB";
            }
            if (bytes >= K * 990)
            {
                return (bytes / (double)M).ToString("F3") + "MiB";
            }
            if (bytes > K * 2)
            {
                return (bytes / (double)K).ToString("F1") + "KiB";
            }
            return bytes.ToString() + "B";
        }

        /// <summary>
        /// Return scaled bandwidth
        /// </summary>
        /// <param name="n">Raw bandwidth</param>
        /// <returns>
        /// The BandwidthScaleInfo struct
        /// </returns>
        public static BandwidthScaleInfo GetBandwidthScale(long n)
        {
            long scale = 1;
            float f = n;
            string unit = "B";
            if (f > 1024)
            {
                f /= 1024;
                scale <<= 10;
                unit = "KiB";
            }
            if (f > 1024)
            {
                f /= 1024;
                scale <<= 10;
                unit = "MiB";
            }
            if (f > 1024)
            {
                f /= 1024;
                scale <<= 10;
                unit = "GiB";
            }
            if (f > 1024)
            {
                f /= 1024;
                scale <<= 10;
                unit = "TiB";
            }
            return new BandwidthScaleInfo(f, unit, scale);
        }

        #endregion


        #region Gzip

        public static string UnGzip(byte[] bufBytes)
        {
            byte[] buffer = new byte[1024];
            int n;
            using MemoryStream ms = new MemoryStream();
            using (GZipStream input = new GZipStream(new MemoryStream(bufBytes), CompressionMode.Decompress, false))
            {
                while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, n);
                }
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }

        #endregion

        public static bool IsWinVistaOrHigher() => Environment.OSVersion.Version.Major > 5;
    }
}

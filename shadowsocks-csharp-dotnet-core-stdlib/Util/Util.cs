using System;
using System.IO;
using System.IO.Compression;
using System.Text;

using NLog;

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

    public static class Utils
    {
        // private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

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

        public static bool IsWinVistaOrHigher() => Environment.OSVersion.Version.Major > 5;

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
    }
}

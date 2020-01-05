using System;
using System.Net;
using System.Net.Sockets;

namespace Shadowsocks.Std.Util.Sockets
{
    public static class SocketUtil
    {
        private class DnsEndPoint2 : DnsEndPoint
        {
            public DnsEndPoint2(string host, int port) : base(host, port) { }

            public DnsEndPoint2(string host, int port, AddressFamily addressFamily) : base(host, port, addressFamily) { }

            public override string ToString() => $"{this.Host}:{this.Port}";
        }

        public static EndPoint GetEndPoint(string host, int port)
        {
            bool parsed = IPAddress.TryParse(host, out IPAddress ipAddress);
            if (parsed)
            {
                return new IPEndPoint(ipAddress, port);
            }

            // maybe is a domain name
            return new DnsEndPoint2(host, port);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "<挂起>")]
        public static void FullClose(this System.Net.Sockets.Socket s)
        {
            try
            {
                s.Shutdown(SocketShutdown.Both);
            }
            catch (Exception)
            { }

            try
            {
                s.Disconnect(false);
            }
            catch (Exception)
            { }

            try
            {
                s.Close();
            }
            catch (Exception)
            { }
        }
        
    }
}

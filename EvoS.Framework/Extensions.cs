using System;
using System.IO;
using System.Net;
using System.Text;
using EvoS.Framework.Constants.Enums;

namespace EvoS.Framework
{
    public static class Extensions
    {
        public static MemoryStream ReadStream(this Stream source)
        {
            var ms = new MemoryStream();

            source.CopyTo(ms);
            ms.Position = 0;

            return ms;
        }

        public static string ReadBinary(this Stream source)
        {
            if (source == null)
                return "NULL";

            try
            {
                var message = source.ReadStream();
                var buffer = new byte[message.Length];

                message.Read(buffer, 0, buffer.Length);
                message.Dispose();

                return BitConverter.ToString(buffer).Replace("-", " ");
            }
            catch
            {
                return "NULL";
            }
        }

        public static string ReadText(this Stream source)
        {
            if (source == null)
                return "NULL";

            try
            {
                var message = source.ReadStream();
                var buffer = new byte[message.Length];

                message.Read(buffer, 0, buffer.Length);
                message.Dispose();

                return Encoding.UTF8.GetString(buffer);
            }
            catch
            {
                return "NULL";
            }
        }
        
        public static PlayerGameResult ToPlayerGameResult(this GameResult gameResult, Team team)
        {
            return gameResult switch
            {
                GameResult.NoResult => PlayerGameResult.NoResult,
                GameResult.TieGame => PlayerGameResult.Tie,
                GameResult.TeamAWon => team == Team.TeamA ? PlayerGameResult.Win : PlayerGameResult.Lose,
                GameResult.TeamBWon => team == Team.TeamB ? PlayerGameResult.Win : PlayerGameResult.Lose,
                _ => PlayerGameResult.NoResult
            };
        }
        
        public static IPAddress GetSubnet(this IPAddress address, int subnet)
        {
            byte[] ipAddressBytes = address.GetAddressBytes();
            if (subnet == 0)
            {
                return new IPAddress(ipAddressBytes);
            }
            
            if (subnet > 32)
            {
                throw new ArgumentException("Bad IP address mask");
            }

            byte[] broadcastAddress = new byte[ipAddressBytes.Length];
            int maskPow = subnet;
            for (int i = broadcastAddress.Length - 1; i >= 0; i--)
            {
                int maskBytePow = Math.Max(0, Math.Min(maskPow, 8));
                maskPow -= 8;
                byte maskByte = (byte)((1 << maskBytePow) - 1);
                broadcastAddress[i] = (byte)(ipAddressBytes[i] | maskByte);
            }
            return new IPAddress(broadcastAddress);
        }
        
        public static bool IsSameSubnet(this IPAddress address, IPAddress otherAddress, int subnet)
        {
            return address.GetSubnet(subnet).Equals(otherAddress.GetSubnet(subnet));
        }
        
        // from websocket-sharp
        public static bool MaybeUri(this string value)
        {
            if (value == null || value.Length == 0)
                return false;
            int length = value.IndexOf(':');
            return length != -1 && length < 10 && value.Substring(0, length).IsPredefinedScheme();
        }
        
        // from websocket-sharp
        public static bool IsPredefinedScheme(this string value)
        {
            if (value == null || value.Length < 2)
                return false;
            char ch = value[0];
            switch (ch)
            {
                case 'f':
                    return value == "file" || value == "ftp";
                case 'h':
                    return value == "http" || value == "https";
                case 'n':
                    return value[1] == 'e' ? value == "news" || value == "net.pipe" || value == "net.tcp" : value == "nntp";
                case 'w':
                    return value == "ws" || value == "wss";
                default:
                    return ch == 'g' && value == "gopher" || ch == 'm' && value == "mailto";
            }
        }
    }
}

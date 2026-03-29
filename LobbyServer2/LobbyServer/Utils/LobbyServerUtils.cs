using System;
using System.Linq;
using System.Net;
using CentralServer.Proxy;
using EvoS.Framework;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Static;
using log4net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using WebSocketSharp.Net.WebSockets;

namespace CentralServer.LobbyServer.Utils
{
    public static class LobbyServerUtils
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(LobbyServerUtils));
        
        public static long ResolveAccountId(long accountId, string handle)
        {
            if (accountId != 0)
            {
                return accountId;
            }

            return ResolveAccountId(handle);
        }

        public static long ResolveAccountId(string handle)
        {
            int hashPos = handle.IndexOf('#');
            string username = hashPos >= 0
                ? handle.Substring(0, hashPos)
                : handle;
            
            LoginDao.LoginEntry loginEntry = DB.Get().LoginDao.Find(username.ToLower());
            return loginEntry?.AccountId ?? 0;
        }

        public static string GetHandle(long accountId)
        {
            return DB.Get().AccountDao.GetAccount(accountId)?.Handle ?? "UNKNOWN";
        }

        public static string GetUserName(long accountId)
        {
            return DB.Get().AccountDao.GetAccount(accountId)?.UserName ?? "UNKNOWN";
        }

        public static string GetHandleForLog(long accountId)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
            return account is not null ? $"{account.UserName}#{accountId}" : "UNKNOWN";
        }

        /**
         * Get the actual ip address of the client, taking all configured proxy servers into account
         */
        public static IPAddress GetActualClientIpAddress(HttpContext context)
        {
            var proxies = ProxyConfiguration.GetProxies()?.Keys;
            IPAddress ipAddress = GetRealRemoteIpAddress(context);
            if (ipAddress is null)
            {
                log.Error("Remote IP address is not detected!");
                return null;
            }

            if (proxies is not null && proxies.Contains(ipAddress))
            {
                if (!context.Request.Headers.TryGetValue("X-Forwarded-For", out StringValues forwardedFor))
                {
                    log.Error($"Proxy {ipAddress} has not set forwarded-for header!");
                    return ipAddress;
                }
                string forwardedForString = forwardedFor.ToString();
                if (string.IsNullOrWhiteSpace(forwardedForString))
                {
                    log.Error($"Proxy {ipAddress} has not set forwarded-for header!");
                    return ipAddress;
                }
                log.Debug($"Proxy {ipAddress} has set forwarded-for header to {forwardedForString}");

                string clientIpString = forwardedForString
                    .Split(',')
                    .Select(s => s.Trim())
                    .LastOrDefault();
                if (!IPAddress.TryParse(clientIpString, out ipAddress))
                {
                    log.Error($"Proxy {ipAddress} has set invalid forwarded-for header: {forwardedForString}");
                }
            }

            return ipAddress;
        }
        
        /**
         * Get the ip address request is coming from, according to local reverse proxy (if it exists)
         */
        private static IPAddress GetRealRemoteIpAddress(Func<string, string> requestHeaders, IPAddress requestAddress)
        {
            var headerName = EvosConfiguration.GetClientIpHeader();
            if (!headerName.IsNullOrEmpty())
            {
                var headerValue = requestHeaders.Invoke(headerName);
                if (headerValue is null)
                {
                    log.Error($"Expected header {headerName} not present!");
                }
                else if (!IPAddress.TryParse(headerValue, out var ipAddress))
                {
                    log.Error($"Header {headerName} has incorrect value {headerValue}!");
                }
                else
                {
                    return ipAddress;
                }
            }

            return requestAddress;
        }

        private static IPAddress GetRealRemoteIpAddress(HttpContext context)
        {
            return GetRealRemoteIpAddress(
                key => context.Request.Headers[key],
                context.Connection.RemoteIpAddress);
        }

        public static ProxyConfiguration.Proxy DetectProxyWs(WebSocketContext context)
        {
            IPAddress clientIpAddress = GetRealRemoteIpAddress(context.Headers.Get, context.UserEndPoint.Address);
            return DetectProxy(clientIpAddress);
        }

        public static ProxyConfiguration.Proxy DetectProxyHttp(HttpContext context)
        {
            return DetectProxy(GetRealRemoteIpAddress(context));
        }

        private static ProxyConfiguration.Proxy DetectProxy(IPAddress clientIpAddress)
        {
            log.Info($"Connecting from {clientIpAddress}");
            
            ProxyConfiguration.Proxy proxy = null;
            if (clientIpAddress is not null)
            {
                ProxyConfiguration.GetProxies()?.TryGetValue(clientIpAddress, out proxy);
            }

            return proxy;
        }
    }
}
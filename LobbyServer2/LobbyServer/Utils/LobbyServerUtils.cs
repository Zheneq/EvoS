using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using EvoS.Framework;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using EvoS.Framework.Network.Static;
using log4net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

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

        public static string GameIdString(LobbyGameInfo gameInfo)
        {
            return gameInfo != null
                ? $"{new DateTime(gameInfo.CreateTimestamp):yyyy_MM_dd__HH_mm_ss}"
                : "N/A";
        }
        
        public static IPAddress GetIpAddress(HttpContext context, ICollection<IPAddress> proxies)
        {
            IPAddress ipAddress = context.Connection.RemoteIpAddress;
                
            if (proxies.Contains(ipAddress))
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

        public static IPAddress GetIpAddress(HttpContext context)
        {
            HashSet<IPAddress> proxies =
                EvosConfiguration.GetProxies().Select(IPAddress.Parse)
                .Concat(EvosConfiguration.GetLightProxies().Select(IPAddress.Parse))
                .ToHashSet();
            return GetIpAddress(context, proxies);
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CentralServer.ApiServer;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Stats;
using CentralServer.LobbyServer.Utils;
using EvoS.DirectoryServer.Account;
using EvoS.DirectoryServer.Inventory;
using EvoS.Framework;
using EvoS.Framework.Auth;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace EvoS.DirectoryServer
{
    public static class Program
    {
        private static IWebHost host;

        public static void Main(string[] args = null)
        {
            host = WebHost.CreateDefaultBuilder()
                .SuppressStatusMessages(true)
                .UseKestrel(koptions => koptions.Listen(IPAddress.Parse("0.0.0.0"), EvosConfiguration.GetDirectoryServerPort()))
                .UseStartup<DirectoryServer>()
                .ConfigureLogging((hostingContext, logging) =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(new Log4NetProvider(new Log4NetProviderOptions("log4net.xml")
                    {
                        LogLevelTranslator = new ApiServer.CustomLogLevelTranslator(),
                    }));
                })
                .Build();

            Console.CancelKeyPress += async (_, _) => { await Stop(); };

            host.Run();
        }

        public static async Task Stop()
        {
            await host.StopAsync();
            host.Dispose();
        }
    }

    public class DirectoryServer
    {
        public const string SUPPORTED_PROTO_VERSION = "b486c83d8a8950340936d040e1953493";
        public const string BUILD_VERSION = "STABLE-122-100";
        public const string ERROR_INVALID_PROTOCOL_VERSION = "INVALID_PROTOCOL_VERSION";

        private static readonly ILog log = LogManager.GetLogger(typeof(DirectoryServer));
        private static long _nextTmpAccountId = 1;
        private static HashSet<IPAddress> _fullProxies;
        private static HashSet<IPAddress> _allProxies;

        public void Configure(IApplicationBuilder app)
        {
            _fullProxies = EvosConfiguration.GetProxies().Select(IPAddress.Parse).ToHashSet();
            _allProxies = _fullProxies.Concat(EvosConfiguration.GetLightProxies().Select(IPAddress.Parse)).ToHashSet();
            log.Info($"Configured proxies: {(_allProxies.Count > 0
                ? string.Join(", ", _allProxies.Select(
                    p => _fullProxies.Contains(p)
                        ? p.ToString()
                        : $"{p}*"))
                : "none")}");
            var serverAddressesFeature = app.ServerFeatures.Get<IServerAddressesFeature>();
            log.Info($"Started DirectoryServer on '0.0.0.0:{EvosConfiguration.GetDirectoryServerPort()}'");

            app.Run(async (context) =>
            {
                var syncIOFeature = context.Features.Get<IHttpBodyControlFeature>();
                if (syncIOFeature != null)
                {
                    syncIOFeature.AllowSynchronousIO = true;
                }

                context.Response.ContentType = "application/json";
                MemoryStream ms = new MemoryStream();
                context.Request.Body.CopyTo(ms);
                ms.Position = 0;
                string requestBody = new StreamReader(ms).ReadToEnd();
                ms.Dispose();

                AssignGameClientRequest request = JsonConvert.DeserializeObject<AssignGameClientRequest>(requestBody);
                log.Debug($"< {request.GetType().Name} {DefaultJsonSerializer.Serialize(request)}");
                AssignGameClientResponse response;
                try
                {
                    response = ProcessRequest(request, context);
                }
                catch (Exception e)
                {
                    response = Fail(request, "Unexpected server error. Please, restart the game.");
                    log.Error("Fail during login", e);
                }
                log.Debug($"> {response.GetType().Name} {DefaultJsonSerializer.Serialize(response)}");
                await context.Response.WriteAsync(JsonConvert.SerializeObject(response));
            });
        }

        private static AssignGameClientResponse ProcessRequest(AssignGameClientRequest request, HttpContext context)
        {
            if (request.SessionInfo.ProtocolVersion != SUPPORTED_PROTO_VERSION)
            {
                return Fail(request, ERROR_INVALID_PROTOCOL_VERSION);
            }

            string ticket = request.AuthInfo.GetTicket();

            if (!ticket.IsNullOrEmpty())
            {
                // If we received a valid TicketData, it means we were previously logged on (reconnection)
                SessionTicketData ticketData = SessionTicketData.FromString(ticket, out bool isSessionTicket);
                if (ticketData != null)
                {
                    request.SessionInfo.AccountId = ticketData.AccountID;
                    request.SessionInfo.SessionToken = ticketData.SessionToken;
                    request.SessionInfo.ReconnectSessionToken = ticketData.ReconnectionSessionToken;

                    AssignGameClientResponse resp = HandleReconnection(request, context);
                    if (resp != null)
                    {
                        return resp;
                    }
                }

                if (!isSessionTicket && EvosConfiguration.GetAllowTicketAuth())
                {
                    AuthTicket authTicket = AuthTicket.TryParse(ticket);
                    if (authTicket != null)
                    {
                        return HandleConnectionWithToken(authTicket.AccountId, authTicket.Token, request, context);
                    }
                }
            }

            string username = request.AuthInfo.UserName;
            string password = request.AuthInfo._Password;
            return HandleConnectionWithPassword(username, password, request, context);
        }

        private static AssignGameClientResponse HandleConnectionWithToken(long accountId, string token, AssignGameClientRequest request, HttpContext context)
        {
            if (token == null)
            {
                return Fail(request, "No credentials provided");
            }

            EvosAuth.TokenData tokenData;
            try
            {
                tokenData = EvosAuth.ValidateToken(EvosAuth.Context.TICKET_AUTH, token);
            }
            // specific token exceptions
            catch (SecurityTokenInvalidLifetimeException e)
            {
                log.Info("Obsolete ticket", e);
                return Fail(request, "Failed to log in");
            }
            // generic exceptions
            catch (SecurityTokenException e)
            {
                log.Info("Invalid ticket", e);
                return Fail(request, "Failed to log in");
            }
            catch (Exception e)
            {
                log.Error("Error while validating ticket", e);
                return Fail(request, "Failed to log in");
            }

            if (tokenData is null)
            {
                return Fail(request, AuthTicket.TICKET_CORRUPT);
            }

            if (accountId != tokenData.AccountId)
            {
                log.Info($"Account id mismatch on auth: {accountId} vs {tokenData.AccountId}");
                return Fail(request, "Failed to log in");
            }

            if (!EvosAuth.ValidateTokenData(context, tokenData, EvosAuth.Context.TICKET_AUTH) &&
                (context.Connection.RemoteIpAddress is null
                 || !IPAddress.IsLoopback(tokenData.IpAddress)
                 || !IPAddress.IsLoopback(context.Connection.RemoteIpAddress)))
            {
                log.Info($"IP address mismatch on auth {accountId}: {context.Connection.RemoteIpAddress} vs {tokenData.IpAddress}");
                return Fail(request, AuthTicket.INVALID_IP_ADDRESS);
            }

            return HandleConnection(tokenData.AccountId, request, context);
        }

        private static AssignGameClientResponse HandleConnectionWithPassword(string username, string password, AssignGameClientRequest request, HttpContext context)
        {
            if (username == null || password == null)
            {
                return Fail(request, "No credentials provided");
            }

            if (!EvosConfiguration.GetAllowUsernamePasswordAuth())
            {
                return Fail(request, "Username and password auth is not allowed");
            }

            long accountId;
            try
            {
                accountId = LoginManager.RegisterOrLogin(request.AuthInfo);
            }
            catch (Exception e) when (e is ArgumentException or ApplicationException or EvosException or ConflictException)
            {
                log.Warn($"Failed login attempt from {GetIpAddress(context)}");
                return Fail(request, e.Message);
            }

            return HandleConnection(accountId, request, context);
        }

        private static AssignGameClientResponse HandleConnection(long accountId, AssignGameClientRequest request, HttpContext context)
        {
            AssignGameClientResponse response = new AssignGameClientResponse
            {
                ResponseId = request.RequestId,
                Success = true,
                ErrorMessage = ""
            };

            if (SessionManager.GetSessionInfo(accountId) != null)
            {
                log.Info($"Concurrent login: {accountId}");
                return Fail(request, "This account is already logged in");
            }

            PersistedAccountData account;
            try
            {
                account = DB.Get().AccountDao.GetAccount(accountId);
                if (account == null)
                {
                    log.Error($"Player {accountId} does not exist");
                    string username = request.AuthInfo.UserName;
                    account = LoginManager.CreateAccount(accountId, username);
                }
                else
                {
                    log.Info($"Player {account.Handle}/{account.AccountId} logged in");
                }
            }
            catch (Exception e)
            {
                long tmpAccId = Interlocked.Increment(ref _nextTmpAccountId);
                account = AccountManager.CreateAccount(tmpAccId, "temp_user#" + tmpAccId);
                log.Error($"Temp user {account.Handle}/{account.AccountId}: {e}");
            }

            // Someday we'll make a db migration tool but not today
            if (PatchAccountData(account))
            {
                account = StatsApi.GetMentorStatus(account);
                DB.Get().AccountDao.UpdateAccount(account);
            }

            response.SessionInfo = SessionManager.CreateSession(accountId, request.SessionInfo, GetIpAddress(context));
            response.LobbyServerAddress = GetLobbyServerAddress(accountId, context);

            LobbyGameClientProxyInfo proxyInfo = new LobbyGameClientProxyInfo
            {
                AccountId = response.SessionInfo.AccountId,
                SessionToken = request.SessionInfo.SessionToken,
                AssignmentTime = DateTimeOffset.Now.ToUnixTimeSeconds(),
                Handle = request.SessionInfo.Handle,
                Status = ClientProxyStatus.Assigned
            };

            response.ProxyInfo = proxyInfo;
            return response;
        }

        private static string GetLobbyServerAddress(long accountId, HttpContext context)
        {
            if (_fullProxies is not null
                && context.Connection.RemoteIpAddress is not null
                && _fullProxies.Contains(context.Connection.RemoteIpAddress))
            {
                log.Info($"{LobbyServerUtils.GetHandle(accountId)} is connecting via proxy {context.Connection.RemoteIpAddress}");
                return context.Connection.RemoteIpAddress.ToString();
            }
            return EvosConfiguration.GetLobbyServerAddress();
        }

        private static AssignGameClientResponse HandleReconnection(AssignGameClientRequest request, HttpContext context)
        {
            LobbySessionInfo session = SessionManager.GetDisconnectedSessionInfo(request.SessionInfo.AccountId)
                                       ?? SessionManager.GetSessionInfo(request.SessionInfo.AccountId);

            if (session == null)
            {
                return null;
            }

            if (session.SessionToken != request.SessionInfo.SessionToken)
            {
                return Fail(request, "ReconnectionError: SessionToken invalid");
            }

            if (session.ReconnectSessionToken != request.SessionInfo.ReconnectSessionToken)
            {
                return Fail(request, "ReconnectionError: ReconnectSessionToken invalid");
            }

            // TODO extra security? Reconnect tokens are possible to hijack

            // SessionManager.CleanSessionAfterReconnect(request.SessionInfo.AccountId);
            session = SessionManager.CreateSession(request.SessionInfo.AccountId, request.SessionInfo, GetIpAddress(context));

            return new AssignGameClientResponse
            {
                Success = true,
                ResponseId = request.RequestId,
                LobbyServerAddress = GetLobbyServerAddress(session.AccountId, context),
                SessionInfo = session,
                ProxyInfo = new LobbyGameClientProxyInfo
                {
                    AccountId = session.AccountId,
                    SessionToken = session.SessionToken,
                    AssignmentTime = DateTimeOffset.Now.ToUnixTimeSeconds(),
                    Handle = session.Handle,
                    Status = ClientProxyStatus.Assigned
                }
            };
        }

        private static bool PatchAccountData(PersistedAccountData account)
        {

#if DEBUG
            account.AccountComponent.SetIsDev(true);
#endif

            // Check if WillFill is missing in CharacterData, if it is add it
            account.CharacterData.TryGetValue(CharacterType.PendingWillFill, out PersistedCharacterData willFill);
            if (willFill == null)
            {
                account.CharacterData.TryAdd(CharacterType.PendingWillFill, new PersistedCharacterData(CharacterType.PendingWillFill));
                willFill = account.CharacterData[CharacterType.PendingWillFill];
            }

            // PATCH Make sure PendingWillFill has default CharacterLoadouts
            if (willFill.CharacterComponent.CharacterLoadouts.Count == 0)
            {
                willFill.CharacterComponent.CharacterLoadouts = new List<CharacterLoadout>()
                {
                    new CharacterLoadout
                    (
                        new CharacterModInfo() { ModForAbility0 = 0, ModForAbility1 = 0, ModForAbility2 = 0, ModForAbility3 = 0, ModForAbility4 = 0 },
                        new CharacterAbilityVfxSwapInfo() { VfxSwapForAbility0 = 0, VfxSwapForAbility1 = 0, VfxSwapForAbility2 = 0, VfxSwapForAbility3 = 0, VfxSwapForAbility4 = 0 },
                        "Default",
                        ModStrictness.AllModes
                    )
                };
                willFill.CharacterComponent.LastSelectedLoadout = 0;
            }

            // These are used in Draft in random fill for subcategory
            // Check if TestFreelancer1 is missing in CharacterData, if it is add it
            account.CharacterData.TryGetValue(CharacterType.TestFreelancer1, out PersistedCharacterData testFreelancer1);
            if (testFreelancer1 == null)
            {
                account.CharacterData.TryAdd(CharacterType.TestFreelancer1, new PersistedCharacterData(CharacterType.TestFreelancer1));
                testFreelancer1 = account.CharacterData[CharacterType.TestFreelancer1];
            }

            // PATCH Make sure testFreelancer1 has default CharacterLoadouts
            if (testFreelancer1.CharacterComponent.CharacterLoadouts.Count == 0)
            {
                testFreelancer1.CharacterComponent.CharacterLoadouts = new List<CharacterLoadout>()
                {
                    new CharacterLoadout
                    (
                        new CharacterModInfo() { ModForAbility0 = 0, ModForAbility1 = 0, ModForAbility2 = 0, ModForAbility3 = 0, ModForAbility4 = 0 },
                        new CharacterAbilityVfxSwapInfo() { VfxSwapForAbility0 = 0, VfxSwapForAbility1 = 0, VfxSwapForAbility2 = 0, VfxSwapForAbility3 = 0, VfxSwapForAbility4 = 0 },
                        "Default",
                        ModStrictness.AllModes
                    )
                };
                testFreelancer1.CharacterComponent.LastSelectedLoadout = 0;
            }

            // Check if TestFreelancer1 is missing in CharacterData, if it is add it
            account.CharacterData.TryGetValue(CharacterType.TestFreelancer2, out PersistedCharacterData testFreelancer2);
            if (testFreelancer2 == null)
            {
                account.CharacterData.TryAdd(CharacterType.TestFreelancer2, new PersistedCharacterData(CharacterType.TestFreelancer2));
                testFreelancer2 = account.CharacterData[CharacterType.TestFreelancer2];
            }

            // PATCH Make sure testFreelancer1 has default CharacterLoadouts
            if (testFreelancer2.CharacterComponent.CharacterLoadouts.Count == 0)
            {
                testFreelancer2.CharacterComponent.CharacterLoadouts = new List<CharacterLoadout>()
                {
                    new CharacterLoadout
                    (
                        new CharacterModInfo() { ModForAbility0 = 0, ModForAbility1 = 0, ModForAbility2 = 0, ModForAbility3 = 0, ModForAbility4 = 0 },
                        new CharacterAbilityVfxSwapInfo() { VfxSwapForAbility0 = 0, VfxSwapForAbility1 = 0, VfxSwapForAbility2 = 0, VfxSwapForAbility3 = 0, VfxSwapForAbility4 = 0 },
                        "Default",
                        ModStrictness.AllModes
                    )
                };
                testFreelancer2.CharacterComponent.LastSelectedLoadout = 0;
            }

            foreach (PersistedCharacterData persistedCharacterData in account.CharacterData.Values)
            {
                persistedCharacterData.CharacterComponent.UnlockSkinsAndTaunts(persistedCharacterData.CharacterType);
                if (EvosStoreConfiguration.AreAllCharactersForFree()) persistedCharacterData.ExperienceComponent.Level = EvosStoreConfiguration.GetStartingCharactersLevel();
                persistedCharacterData.CharacterComponent.UnlockVFX(persistedCharacterData.CharacterType);
            }

            if (EvosStoreConfiguration.AreEmojisFree()) account.AccountComponent.UnlockedEmojiIDs = InventoryManager.GetUnlockedEmojiIDs(account.AccountId);
            if (EvosStoreConfiguration.AreLoadingScreenBackgroundFree())
            {
                Dictionary<int, bool> activatedBgs = account.AccountComponent.UnlockedLoadingScreenBackgroundIdsToActivatedState;
                if (!CompilerExtensions.IsNullOrEmpty(activatedBgs) && activatedBgs.Values.Any(x => !x))
                {
                    // preserve which backgrounds are activated
                    account.AccountComponent.UnlockedLoadingScreenBackgroundIdsToActivatedState = InventoryManager.GetActivatedLoadingScreenBackgroundIds(account.AccountId, false);
                    foreach ((int bgId, bool activated) in activatedBgs)
                    {
                        account.AccountComponent.UnlockedLoadingScreenBackgroundIdsToActivatedState[bgId] = activated;
                    }
                }
                else
                {
                    account.AccountComponent.UnlockedLoadingScreenBackgroundIdsToActivatedState = InventoryManager.GetActivatedLoadingScreenBackgroundIds(account.AccountId, true);
                }
            }
            if (EvosStoreConfiguration.AreOverconsFree()) account.AccountComponent.UnlockedOverconIDs = InventoryManager.GetUnlockedOverconIDs(account.AccountId);
            if (EvosStoreConfiguration.AreTitlesFree()) account.AccountComponent.UnlockedTitleIDs = InventoryManager.GetUnlockedTitleIDs(account.AccountId);
            if (EvosStoreConfiguration.AreBannersFree()) account.AccountComponent.UnlockedBannerIDs = InventoryManager.GetUnlockedBannerIDs(account.AccountId);

            if (LobbyConfiguration.IsTrustWarEnabled())
            {
                account.AccountComponent.UnlockedRibbonIDs = InventoryManager.GetUnlockedRibbonIDs(account.AccountId);
                // initialize faction competition data with id 0
                account.AccountComponent.FactionCompetitionData.TryAdd(0, new PlayerFactionCompetitionData()
                {
                    CompetitionID = 1,
                    Factions = new Dictionary<int, FactionPlayerData>()
                                {
                                    { 0, new FactionPlayerData() { FactionID = 1, TotalXP = 0 } },
                                    { 1, new FactionPlayerData() { FactionID = 2, TotalXP = 0 } },
                                    { 2, new FactionPlayerData() { FactionID = 3, TotalXP = 0 } }
                                }
                }
);
            }
            else
            {
                account.AccountComponent.UnlockedRibbonIDs = new List<int>();
            }

            if (account.AccountComponent.IsDev())
            {
                //Give developers access to the Developer title
                account.AccountComponent.UnlockedTitleIDs.Add(26);
            }

            if (account.SocialComponent.BlockedAccounts == null)
            {
                account.SocialComponent.BlockedAccounts = new HashSet<long>();
            }

            return true;
        }

        private static AssignGameClientResponse Fail(AssignGameClientRequest request, string reason)
        {
            return new AssignGameClientResponse
            {
                RequestId = 0,
                ResponseId = request.RequestId,
                Success = false,
                ErrorMessage = reason
            };
        }

        public static IPAddress GetIpAddress(HttpContext context)
        {
            return LobbyServerUtils.GetIpAddress(context, _allProxies);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CentralServer.ApiServer;
using CentralServer.LobbyServer.Character;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using CentralServer.Proxy;
using EvoS.DirectoryServer.Account;
using EvoS.DirectoryServer.Inventory;
using EvoS.Framework;
using EvoS.Framework.Auth;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Misc;
using EvoS.Framework.Network;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace EvoS.DirectoryServer
{
    public static class Program
    {
        private static IWebHost host;

        public static void Main(string[] args = null)
        {
            EvosConfiguration.ValidateConfiguration();
            LoginManager.ValidateConfiguration();
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
        public const string BUILD_VERSION = "STABLE-122-100";
        public const string ERROR_INVALID_PROTOCOL_VERSION = "INVALID_PROTOCOL_VERSION";

        private static readonly ILog log = LogManager.GetLogger(typeof(DirectoryServer));
        private static long _nextTmpAccountId = 1;
        private static readonly TimeSpan PreviousSessionPollInterval = TimeSpan.FromMilliseconds(250);

        public void Configure(IApplicationBuilder app)
        {
            var serverAddressesFeature = app.ServerFeatures.Get<IServerAddressesFeature>();
            log.Info($"Started DirectoryServer on '0.0.0.0:{EvosConfiguration.GetDirectoryServerPort()}'");

            app.Run(async context =>
            {
                context.Response.ContentType = "application/json";
                MemoryStream ms = new MemoryStream();
                await context.Request.Body.CopyToAsync(ms);
                ms.Position = 0;
                string requestBody = await new StreamReader(ms).ReadToEndAsync();
                await ms.DisposeAsync();

                AssignGameClientRequest request = JsonConvert.DeserializeObject<AssignGameClientRequest>(requestBody);
                log.Debug($"< AssignGameClientRequest {DefaultJsonSerializer.Serialize(request)}");
                AssignGameClientResponse response;
                if (request?.SessionInfo is null || request.AuthInfo is null)
                {
                    response = Fail(new AssignGameClientRequest(), "Network error. Please, restart the game.");
                    log.Warn($"Fail during login (bad request) userAgent=\"{context.Request.Headers.UserAgent}\" body=\"{LogRedaction.MaskSensitiveJsonFields(requestBody)}\"");
                }
                else
                {
                    try
                    {
                        response = await ProcessRequest(request, context);
                    }
                    catch (Exception e)
                    {
                        response = Fail(request, "Unexpected server error. Please, restart the game.");
                        log.Error("Fail during login", e);
                    }
                }
                log.Debug($"> {response.GetType().Name} {DefaultJsonSerializer.Serialize(response)}");
                await context.Response.WriteAsync(JsonConvert.SerializeObject(response));
            });
        }

        private static async Task<AssignGameClientResponse> ProcessRequest(AssignGameClientRequest request, HttpContext context)
        {
            if (!ProtocolVersion.SUPPORTED_PROTO_VERSIONS.Contains(request.SessionInfo.ProtocolVersion))
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
                        return await HandleConnectionWithToken(authTicket.AccountId, authTicket.Token, request, context);
                    }
                }
            }

            string username = request.AuthInfo.UserName;
            string password = request.AuthInfo._Password;
            return await HandleConnectionWithPassword(username, password, request, context);
        }

        private static async Task<AssignGameClientResponse> HandleConnectionWithToken(long accountId, string token, AssignGameClientRequest request, HttpContext context)
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

            return await HandleConnection(tokenData.AccountId, request, context);
        }

        private static async Task<AssignGameClientResponse> HandleConnectionWithPassword(string username, string password, AssignGameClientRequest request, HttpContext context)
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
                log.Warn($"Failed login attempt from {LobbyServerUtils.GetActualClientIpAddress(context)}");
                return Fail(request, e.Message);
            }

            return await HandleConnection(accountId, request, context);
        }

        private static async Task<AssignGameClientResponse> HandleConnection(long accountId, AssignGameClientRequest request, HttpContext context)
        {
            AssignGameClientResponse response = new AssignGameClientResponse
            {
                ResponseId = request.RequestId,
                Success = true,
                ErrorMessage = ""
            };

            // Done before any account work. The authoritative, race-free check happens atomically inside
            // CreateSession below (rejectIfActive).
            if (!await WaitForPreviousSessionToEnd(accountId, context.RequestAborted))
            {
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
                DB.Get().AccountDao.UpdateAccount(account);
            }

            try
            {
                response.SessionInfo = SessionManager.CreateSession(
                    accountId, request.SessionInfo, LobbyServerUtils.GetActualClientIpAddress(context), rejectIfActive: true);
            }
            catch (ConflictException)
            {
                // e.g. two instances waited out the same dead session, and the other one got in first
                log.Info($"Concurrent login (race): {accountId}");
                return Fail(request, "This account is already logged in");
            }
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

        /// <summary>
        /// If the account is still online, the previous client may have crashed: its connection gets dropped once it
        /// stops answering keep-alive pings, so wait for that (up to ConcurrentLoginWaitPeriod) before rejecting this
        /// login as a second instance of the game. Gives up if the client abandons the request.
        /// Returns whether the account is free.
        /// </summary>
        private static async Task<bool> WaitForPreviousSessionToEnd(long accountId, CancellationToken cancellationToken)
        {
            if (!SessionManager.HasActiveSession(accountId))
            {
                return true;
            }

            TimeSpan waitPeriod = EvosConfiguration.GetConcurrentLoginWaitPeriod();
            log.Info($"Concurrent login: {accountId}, waiting up to {waitPeriod} for the previous session to end");
            DateTime deadline = DateTime.UtcNow + waitPeriod;
            try
            {
                while (SessionManager.HasActiveSession(accountId))
                {
                    TimeSpan remaining = deadline - DateTime.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                    {
                        log.Info($"Concurrent login: {accountId}, the previous session is still active");
                        return false;
                    }
                    await Task.Delay(remaining < PreviousSessionPollInterval ? remaining : PreviousSessionPollInterval, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                log.Info($"Concurrent login: {accountId}, abandoned while waiting for the previous session to end");
                return false;
            }

            log.Info($"Concurrent login: {accountId}, the previous session has ended");
            return true;
        }

        private static string GetLobbyServerAddress(long accountId, HttpContext context)
        {
            ProxyConfiguration.Proxy proxy = LobbyServerUtils.DetectProxy(context);

            if (proxy != null)
            {
                log.Info($"{LobbyServerUtils.GetHandle(accountId)} is connecting via proxy {proxy.GetName()}");
                return proxy.LobbyAddress;
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
            session = SessionManager.CreateSession(request.SessionInfo.AccountId, request.SessionInfo, LobbyServerUtils.GetActualClientIpAddress(context));

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

        // Someday we'll make a db migration tool but not today
        internal static bool PatchAccountData(PersistedAccountData account)
        {
            string before = Snapshot(account);
            ApplyAccountDataPatches(account);
            string after = Snapshot(account);
            // If a snapshot failed we cannot prove the account is unchanged, so persist it as before.
            return before == null || after == null || before != after;
        }

        private static List<int> WithoutDuplicates(List<int> ids)
        {
            return ids?.Distinct().ToList();
        }

        private static string Snapshot(PersistedAccountData account)
        {
            try
            {
                return DefaultJsonSerializer.Serialize(account);
            }
            catch (Exception e)
            {
                log.Warn($"Failed to snapshot account {account.AccountId} for change detection", e);
                return null;
            }
        }

        private static void EnsureCharacterWithDefaultLoadout(PersistedAccountData account, CharacterType characterType)
        {
            account.CharacterData.TryGetValue(characterType, out PersistedCharacterData characterData);
            if (characterData == null)
            {
                account.CharacterData.TryAdd(characterType, new PersistedCharacterData(characterType));
                characterData = account.CharacterData[characterType];
            }

            if (characterData.CharacterComponent.CharacterLoadouts.Count == 0)
            {
                characterData.CharacterComponent.CharacterLoadouts = new List<CharacterLoadout>()
                {
                    new CharacterLoadout
                    (
                        new CharacterModInfo() { ModForAbility0 = 0, ModForAbility1 = 0, ModForAbility2 = 0, ModForAbility3 = 0, ModForAbility4 = 0 },
                        new CharacterAbilityVfxSwapInfo() { VfxSwapForAbility0 = 0, VfxSwapForAbility1 = 0, VfxSwapForAbility2 = 0, VfxSwapForAbility3 = 0, VfxSwapForAbility4 = 0 },
                        "Default",
                        ModStrictness.AllModes
                    )
                };
                characterData.CharacterComponent.LastSelectedLoadout = 0;
            }
        }

        private static void ApplyAccountDataPatches(PersistedAccountData account)
        {

#if DEBUG
            if (EvosConfiguration.GetDevMode())
            {
                account.AccountComponent.SetIsDev(true);
            }
#endif

            foreach (CharacterType characterType in CharacterConfigs.Characters.Keys)
            {
                EnsureCharacterWithDefaultLoadout(account, characterType);
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

            // Heal duplicates that older versions of the unlock lists and the dev-title patch
            // have baked into stored accounts.
            AccountComponent accountComponent = account.AccountComponent;
            accountComponent.UnlockedEmojiIDs = WithoutDuplicates(accountComponent.UnlockedEmojiIDs);
            accountComponent.UnlockedOverconIDs = WithoutDuplicates(accountComponent.UnlockedOverconIDs);
            accountComponent.UnlockedTitleIDs = WithoutDuplicates(accountComponent.UnlockedTitleIDs);
            accountComponent.UnlockedBannerIDs = WithoutDuplicates(accountComponent.UnlockedBannerIDs);

            if (account.AccountComponent.IsDev() && !account.AccountComponent.UnlockedTitleIDs.Contains(26))
            {
                //Give developers access to the Developer title
                account.AccountComponent.UnlockedTitleIDs.Add(26);
            }

            if (account.SocialComponent.BlockedAccounts == null)
            {
                account.SocialComponent.BlockedAccounts = new HashSet<long>();
            }
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
    }
}

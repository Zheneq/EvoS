using System;
using System.IO;
using System.Net;
using System.Threading;
using EvoS.DirectoryServer.Account;
using EvoS.DirectoryServer.Inventory;
using EvoS.Framework;
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
using Newtonsoft.Json;

namespace EvoS.DirectoryServer
{
    public class Program
    {
        public static void Main(string[] args = null)
        {
            var host = WebHost.CreateDefaultBuilder()
                .SuppressStatusMessages(true)
                .UseKestrel(koptions => koptions.Listen(IPAddress.Parse("0.0.0.0"), EvosConfiguration.GetDirectoryServerPort()))
                .UseStartup<DirectoryServer>()
                .Build();

            Console.CancelKeyPress += async (sender, @event) =>
            {
                await host.StopAsync();
                host.Dispose();
            };

            host.Run();
        }
    }

    public class DirectoryServer
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DirectoryServer));
        private static long _nextTmpAccountId = 1;
        
        public void Configure(IApplicationBuilder app)
        {
            var serverAddressesFeature = app.ServerFeatures.Get<IServerAddressesFeature>();
            log.Info($"Started DirectoryServer on '0.0.0.0:{EvosConfiguration.GetDirectoryServerPort()}'");

            app.Run((context) =>
            {
                context.Response.ContentType = "application/json";
                MemoryStream ms = new MemoryStream();
                context.Request.Body.CopyTo(ms);
                ms.Position = 0;
                string requestBody = new StreamReader(ms).ReadToEnd();
                ms.Dispose();

                AssignGameClientRequest request = JsonConvert.DeserializeObject<AssignGameClientRequest>(requestBody);
                log.Debug($"< {request.GetType().Name} {DefaultJsonSerializer.Serialize(request)}");
                AssignGameClientResponse response = ProcessRequest(request);
                log.Debug($"> {response.GetType().Name} {DefaultJsonSerializer.Serialize(response)}");
                return context.Response.WriteAsync(JsonConvert.SerializeObject(response));
            });
        }

        private static AssignGameClientResponse ProcessRequest(AssignGameClientRequest request)
        {
            AssignGameClientResponse response = new AssignGameClientResponse
                {
                    RequestId = request.RequestId,
                    ResponseId = request.ResponseId,
                    Success = true,
                    ErrorMessage = ""
                };

            string username = request.AuthInfo.UserName;
            string password = request.AuthInfo._Password;

            if (username == null || password == null)
            {
                return Fail(request, "No credentials provided");
            }

            long accountId;
            try
            {
                accountId = LoginManager.RegisterOrLogin(request.AuthInfo);
            }
            catch (ArgumentException e)
            {
                return Fail(request, e.Message);
            }
            catch (ApplicationException e)
            {
                return Fail(request, e.Message);
            }

            PersistedAccountData account;
            try
            {
                account = DB.Get().AccountDao.GetAccount(accountId);
                if (account == null)
                {
                    log.Info($"Player {accountId} does not exist");
                    DB.Get().AccountDao.CreateAccount(AccountManager.CreateAccount(accountId, username));
                    account = DB.Get().AccountDao.GetAccount(accountId);
                    if (account != null)
                    {
                        log.Info($"Successfully Registered {account.Handle}/{account.AccountId}");
                    }
                    else
                    {
                        log.Error($"Error creating a new account for player '{username}'/{accountId}");
                        account = AccountManager.CreateAccount(accountId, username);
                        log.Error($"Temp user {account.Handle}/{account.AccountId}");
                    }
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

            request.SessionInfo.SessionToken = 0;

            response.SessionInfo = request.SessionInfo;
            response.SessionInfo.AccountId = account.AccountId;
            response.SessionInfo.Handle = account.Handle;
            response.SessionInfo.ConnectionAddress = "127.0.0.1";
            response.SessionInfo.ProcessCode = "";
            response.SessionInfo.FakeEntitlements = "";
            response.SessionInfo.LanguageCode = "EN"; // Needs to be uppercase

            response.LobbyServerAddress = EvosConfiguration.GetLobbyServerAddress();

            LobbyGameClientProxyInfo proxyInfo = new LobbyGameClientProxyInfo
            {
                AccountId = response.SessionInfo.AccountId,
                SessionToken = request.SessionInfo.SessionToken,
                AssignmentTime = 1565574095,
                Handle = request.SessionInfo.Handle,
                Status = ClientProxyStatus.Assigned
            };

            response.ProxyInfo = proxyInfo;
            return response;
        }

        private static bool PatchAccountData(PersistedAccountData account)
        {
            foreach (PersistedCharacterData persistedCharacterData in account.CharacterData.Values)
            {
                persistedCharacterData.CharacterComponent.UnlockSkinsAndTaunts(persistedCharacterData.CharacterType);
                if (EvosStoreConfiguration.AreAllCharactersForFree()) persistedCharacterData.ExperienceComponent.Level = EvosStoreConfiguration.GetStartingCharactersLevel();
                persistedCharacterData.CharacterComponent.UnlockVFX(persistedCharacterData.CharacterType);
            }

            if (EvosStoreConfiguration.AreEmojisFree()) account.AccountComponent.UnlockedEmojiIDs = InventoryManager.GetUnlockedEmojiIDs(account.AccountId);
            if (EvosStoreConfiguration.AreLoadingScreenBackgroundFree()) account.AccountComponent.UnlockedLoadingScreenBackgroundIdsToActivatedState = InventoryManager.GetActivatedLoadingScreenBackgroundIds(account.AccountId);
            if (EvosStoreConfiguration.AreOverconsFree()) account.AccountComponent.UnlockedOverconIDs = InventoryManager.GetUnlockedOverconIDs(account.AccountId);
            if (EvosStoreConfiguration.AreTitlesFree()) account.AccountComponent.UnlockedTitleIDs = InventoryManager.GetUnlockedTitleIDs(account.AccountId);
            if (EvosStoreConfiguration.AreBannersFree()) account.AccountComponent.UnlockedBannerIDs = InventoryManager.GetUnlockedBannerIDs(account.AccountId);

            return true;
        }

        private static AssignGameClientResponse Fail(AssignGameClientRequest request, string reason)
        {
            return new AssignGameClientResponse
            {
                RequestId = request.RequestId,
                ResponseId = request.ResponseId,
                Success = false,
                ErrorMessage = reason
            };
        }
    }
}

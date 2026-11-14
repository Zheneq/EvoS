using System;
using System.Threading;
using System.Threading.Tasks;
using CentralServer.LobbyServer.Chat;
using CentralServer.LobbyServer.Session;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.LobbyServer
{
    class AdminManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AdminManager));
        
        public event Action<long, AdminComponent.AdminActionRecord> OnAdminAction = delegate {};
        public event Action<long, long, string> OnAdminMessage = delegate {};
        
        private readonly CancellationTokenSource cancelTokenSource = new CancellationTokenSource();

        private static AdminManager _instance;

        public static AdminManager Get()
        {
            return _instance ??= new AdminManager();
        }
        
        public void Start()
        {
            _ = UpdateMutedLoop(cancelTokenSource.Token);
        }

        public void UpdatePenalties(long accountId)
        {
            UpdateBanned(accountId);
            UpdateMuted(accountId);
        }

        private async Task UpdateMutedLoop(CancellationToken cancelToken)
        {
            while (true)
            {
                if (cancelToken.IsCancellationRequested) return;
                UpdateMuted();
                await Task.Delay(300_000, cancelToken);
            }
        }

        private void UpdateMuted()
        {
            foreach (long accountId in SessionManager.GetOnlinePlayers())
            {
                UpdateMuted(accountId);
            }
        }

        private static void UpdateMuted(long accountId)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
            if (account == null)
            {
                log.Warn($"Cannot update muted status: account {accountId} not found");
                return;
            }

            if (account.AdminComponent.Muted && account.AdminComponent.MutedUntil <= DateTime.UtcNow)
            {
                log.Info($"UNMUTE {account.Handle}: time out");
                account.AdminComponent.Muted = false;
                DB.Get().AccountDao.UpdateAdminComponent(account);
            }
        }

        public bool Mute(long accountId, TimeSpan duration, string adminUsername, string description)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
            if (account == null)
            {
                log.Error($"Cannot mute: account {accountId} not found");
                return false;
            }
            
            DateTime time = DateTime.UtcNow;
            bool muted = duration.Ticks > 0;
            AdminComponent.AdminActionRecord record = new AdminComponent.AdminActionRecord
            {
                AdminUsername = adminUsername,
                ActionType = muted ? AdminComponent.AdminActionType.Mute : AdminComponent.AdminActionType.Unmute,
                Duration = duration,
                Description = description,
                Time = time,
            };
            account.AdminComponent.AdminActions.Add(record);
            account.AdminComponent.MutedUntil = time + duration;
            account.AdminComponent.Muted = muted;
            DB.Get().AccountDao.UpdateAdminComponent(account);
            
            string logString = muted
                ? $"MUTE {account.Handle} for {duration}"
                : $"UNMUTE {account.Handle}";
            log.Info($"{logString} by {adminUsername}: {description}");
            OnAdminAction(accountId, record);
            return true;
        }

        private static void UpdateBanned(long accountId)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
            if (account == null)
            {
                log.Warn($"Cannot update banned status: account {accountId} not found");
                return;
            }
            
            if (account.AdminComponent.Locked && account.AdminComponent.LockedUntil <= DateTime.UtcNow)
            {
                log.Info($"UNBAN {account.Handle}: time out");
                account.AdminComponent.Locked = false;
                DB.Get().AccountDao.UpdateAdminComponent(account);
            }
        }
        
        public bool Ban(long accountId, TimeSpan duration, string adminUsername, string description)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
            if (account == null)
            {
                log.Error($"Cannot ban: account {accountId} not found");
                return false;
            }
            
            DateTime time = DateTime.UtcNow;
            bool banned = duration.Ticks > 0;
            AdminComponent.AdminActionRecord record = new AdminComponent.AdminActionRecord
            {
                AdminUsername = adminUsername,
                ActionType = banned ? AdminComponent.AdminActionType.Lock : AdminComponent.AdminActionType.Unlock,
                Duration = duration,
                Description = description,
                Time = time,
            };
            account.AdminComponent.AdminActions.Add(record);
            account.AdminComponent.LockedUntil = time + duration;
            account.AdminComponent.Locked = banned;
            DB.Get().AccountDao.UpdateAdminComponent(account);
            
            string logString = banned
                ? $"BAN {account.Handle} for {duration}"
                : $"UNBAN {account.Handle}";
            log.Info($"{logString} by {adminUsername}: {description}");
            OnAdminAction(accountId, record);

            IClientConnection conn = SessionManager.GetClientConnection(accountId);
            if (conn != null)
            {
                log.Info($"KICK {account.Handle} by {adminUsername}: {description}");
                conn.CloseConnection();
            }
            else
            {
                log.Info($"{account.Handle} is currently offline");
            }

            return true;
        }

        public bool SendAdminMessage(long accountId, long adminAccountId, string msg)
        {
            PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
            if (account == null)
            {
                log.Error($"Cannot send admin message: account {accountId} not found");
                return false;
            }
            
            AdminMessageManager.SendAdminMessage(accountId, adminAccountId, msg);
            
            OnAdminMessage(accountId, adminAccountId, msg);
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CentralServer.LobbyServer.Discord;
using EvoS.Framework;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using log4net;

namespace CentralServer.BridgeServer
{
    /// <summary>
    /// Owns the persisted game-server key store (approve/decline/revoke) and the in-memory registry
    /// of connections held open while awaiting admin approval. See <see cref="BridgeServerProtocol"/>
    /// for the handshake that feeds this manager.
    /// </summary>
    public static class GameServerKeyManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(GameServerKeyManager));

        // fingerprint -> connection awaiting an admin decision
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, BridgeServerProtocol> Pending = new();

        /// <summary>
        /// Records a connecting server's key (creating a Pending record on first sight) and returns the
        /// effective status the caller should act on. In dev mode, unknown/pending keys are auto-approved.
        /// </summary>
        public static GameServerKeyStatus RegisterConnection(string fingerprint, string publicKey, string address, string buildVersion)
        {
            GameServerKeyDao.GameServerKey key = DB.Get().GameServerKeyDao.Find(fingerprint);
            bool isNew = key == null;
            if (isNew)
            {
                key = new GameServerKeyDao.GameServerKey
                {
                    Fingerprint = fingerprint,
                    PublicKey = publicKey,
                    Status = GameServerKeyStatus.Pending,
                    FirstSeenAt = DateTime.UtcNow,
                };
            }

            key.LastConnectedAt = DateTime.UtcNow;
            key.LastAddress = address;
            key.LastBuildVersion = buildVersion;

            if (EvosConfiguration.GetDevMode() && key.Status == GameServerKeyStatus.Pending)
            {
                log.Warn($"Dev mode: auto-approving game server key {fingerprint}");
                key.Status = GameServerKeyStatus.Approved;
                key.ApprovedAt = DateTime.UtcNow;
            }

            DB.Get().GameServerKeyDao.Save(key);

            if (isNew && key.Status == GameServerKeyStatus.Pending)
            {
                log.Info($"New game server key pending approval: {fingerprint} ({address})");
                DiscordManager.Get().SendAdminLogMessageAsync(
                    $"A new game server is awaiting approval: `{fingerprint}` ({address}). Approve it in the admin panel.");
            }

            return key.Status;
        }

        public static void AddPending(string fingerprint, BridgeServerProtocol connection)
        {
            Pending[fingerprint] = connection;
            TimeSpan timeout = EvosConfiguration.GetBridgeAuthPendingTimeout();
            Task.Delay(timeout).ContinueWith(_ =>
            {
                if (Pending.TryGetValue(fingerprint, out BridgeServerProtocol c) && c == connection)
                {
                    Pending.TryRemove(new KeyValuePair<string, BridgeServerProtocol>(fingerprint, connection));
                    log.Info($"Pending game server {fingerprint} timed out awaiting approval");
                    if (connection.IsConnected)
                    {
                        connection.RejectRegistration();
                    }
                }
            });
        }

        public static void RemovePending(string fingerprint, BridgeServerProtocol connection)
        {
            if (fingerprint == null)
            {
                return;
            }
            Pending.TryRemove(new KeyValuePair<string, BridgeServerProtocol>(fingerprint, connection));
        }

        public static List<GameServerKeyDao.GameServerKey> GetAll()
        {
            return DB.Get().GameServerKeyDao.FindAll();
        }

        /// <summary>Approves a key. If a server is currently held pending on this key, it goes live immediately.</summary>
        public static bool Approve(string fingerprint, long adminAccountId, string name = null)
        {
            GameServerKeyDao.GameServerKey key = DB.Get().GameServerKeyDao.Find(fingerprint);
            if (key == null)
            {
                return false;
            }

            key.Status = GameServerKeyStatus.Approved;
            key.ApprovedAt = DateTime.UtcNow;
            key.ApprovedByAccountId = adminAccountId;
            if (!string.IsNullOrWhiteSpace(name))
            {
                key.Name = name;
            }
            DB.Get().GameServerKeyDao.Save(key);
            log.Info($"Game server key {fingerprint} approved by {adminAccountId}");

            if (Pending.TryRemove(fingerprint, out BridgeServerProtocol connection) && connection.IsConnected)
            {
                connection.CompleteRegistration();
            }
            return true;
        }

        /// <summary>
        /// Declines a pending key or revokes an approved one. Closes a held-pending connection and
        /// disconnects any live server currently using this key.
        /// </summary>
        public static bool Reject(string fingerprint, long adminAccountId)
        {
            GameServerKeyDao.GameServerKey key = DB.Get().GameServerKeyDao.Find(fingerprint);
            if (key == null)
            {
                return false;
            }

            bool wasApproved = key.Status == GameServerKeyStatus.Approved;
            key.Status = wasApproved ? GameServerKeyStatus.Revoked : GameServerKeyStatus.Declined;
            DB.Get().GameServerKeyDao.Save(key);
            log.Info($"Game server key {fingerprint} {(wasApproved ? "revoked" : "declined")} by {adminAccountId}");

            if (Pending.TryRemove(fingerprint, out BridgeServerProtocol connection) && connection.IsConnected)
            {
                connection.RejectRegistration();
            }
            ServerManager.DisconnectByFingerprint(fingerprint);
            return true;
        }
    }
}

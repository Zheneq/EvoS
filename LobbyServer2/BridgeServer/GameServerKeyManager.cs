using System;
using System.Collections.Generic;
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

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<IGameServerConnection, string> Pending = new();

        /// <summary>
        /// Strict one-connection-per-key: a key identifies a single server. Returns true if another
        /// connection is already using this key (awaiting approval, or live in the pool under a
        /// different process code). A same-process-code reconnection is allowed (handled by ServerManager).
        /// </summary>
        public static bool IsKeyInUse(string fingerprint, string processCode)
        {
            foreach (string pendingFingerprint in Pending.Values)
            {
                if (pendingFingerprint == fingerprint)
                {
                    return true;
                }
            }
            return ServerManager.HasOtherServerWithFingerprint(fingerprint, processCode);
        }

        /// <summary>
        /// True if an approved key is connecting from a source address other than the one it was pinned
        /// to at approval — i.e. the server "moved". Used to re-pend the key for admin review.
        /// </summary>
        public static bool IsAddressMismatch(GameServerKeyDao.GameServerKey key, string actualAddress)
        {
            return key.Status == GameServerKeyStatus.Approved
                   && key.ApprovedActualAddress != null
                   && !string.Equals(key.ApprovedActualAddress, actualAddress);
        }

        /// <summary>
        /// Records a connecting server's key (creating a Pending record on first sight) and returns the
        /// effective status the caller should act on. An approved key connecting from an unexpected source
        /// address is re-pended for admin re-approval. In dev mode, pending keys are auto-approved.
        /// </summary>
        public static GameServerKeyStatus RegisterConnection(
            string fingerprint,
            string publicKey,
            string connectionAddress,
            string actualAddress,
            string buildVersion,
            string reportedName)
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
            key.LastConnectionAddress = connectionAddress;
            key.LastActualAddress = actualAddress;
            key.LastBuildVersion = buildVersion;
            key.LastName = reportedName;

            bool movedAddress = IsAddressMismatch(key, actualAddress);
            string pinnedActualAddress = key.ApprovedActualAddress;
            if (movedAddress)
            {
                log.Warn($"Approved game server key {fingerprint} connected from unexpected address " +
                         $"{actualAddress} (pinned {pinnedActualAddress}); re-pending for admin approval");
                key.Status = GameServerKeyStatus.Pending;
                key.ApprovedAt = null;
                key.ApprovedByAccountId = null;
            }

            if (EvosConfiguration.GetDevMode() && key.Status == GameServerKeyStatus.Pending)
            {
                log.Warn($"Dev mode: auto-approving game server key {fingerprint}");
                key.Status = GameServerKeyStatus.Approved;
                key.ApprovedAt = DateTime.UtcNow;
                key.ApprovedActualAddress = actualAddress;
            }

            DB.Get().GameServerKeyDao.Save(key);

            if (isNew && key.Status == GameServerKeyStatus.Pending)
            {
                log.Info($"New game server key pending approval: {fingerprint} ({connectionAddress})");
                DiscordManager.Get().SendAdminLogMessageAsync(
                    $"A new game server is awaiting approval: `{fingerprint}` ({connectionAddress}). Approve it in the admin panel.");
            }
            else if (movedAddress && key.Status == GameServerKeyStatus.Pending)
            {
                DiscordManager.Get().SendAdminLogMessageAsync(
                    $"Approved game server key `{fingerprint}` connected from a NEW source address " +
                    $"({pinnedActualAddress} -> {actualAddress}) and was held pending re-approval. Verify this is expected before approving.");
            }

            return key.Status;
        }

        public static void AddPending(string fingerprint, IGameServerConnection connection)
        {
            Pending[connection] = fingerprint;
        }

        public static void RemovePending(IGameServerConnection connection)
        {
            Pending.TryRemove(connection, out _);
        }

        // Atomically removes and returns all connections currently pending on the given key.
        // Strict mode keeps this to at most one, but revocation still sweeps defensively.
        private static List<IGameServerConnection> TakePendingFor(string fingerprint)
        {
            List<IGameServerConnection> taken = new List<IGameServerConnection>();
            foreach (KeyValuePair<IGameServerConnection, string> entry in Pending)
            {
                if (entry.Value == fingerprint && Pending.TryRemove(entry.Key, out _))
                {
                    taken.Add(entry.Key);
                }
            }
            return taken;
        }

        public static List<GameServerKeyDao.GameServerKey> GetAll()
        {
            return DB.Get().GameServerKeyDao.FindAll();
        }

        /// <summary>Approves a key. If a server is currently held pending on this key, it goes live immediately.</summary>
        public static bool Approve(string fingerprint, long adminAccountId)
        {
            GameServerKeyDao.GameServerKey key = DB.Get().GameServerKeyDao.Find(fingerprint);
            if (key == null)
            {
                return false;
            }

            key.Status = GameServerKeyStatus.Approved;
            key.ApprovedAt = DateTime.UtcNow;
            key.ApprovedByAccountId = adminAccountId;
            // Pin the key to the source address it is currently connecting from; a later connection from
            // a different address will re-pend it for review.
            key.ApprovedActualAddress = key.LastActualAddress;
            DB.Get().GameServerKeyDao.Save(key);
            log.Info($"Game server key {fingerprint} approved by {adminAccountId}");

            foreach (IGameServerConnection connection in TakePendingFor(fingerprint))
            {
                if (connection.IsConnected)
                {
                    connection.CompleteRegistration();
                }
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

            foreach (IGameServerConnection connection in TakePendingFor(fingerprint))
            {
                if (connection.IsConnected)
                {
                    connection.RejectRegistration();
                }
            }
            ServerManager.DisconnectByFingerprint(fingerprint);
            return true;
        }
    }
}

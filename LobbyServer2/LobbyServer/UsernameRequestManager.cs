using System;
using CentralServer.LobbyServer.Discord;
using EvoS.Framework;
using EvoS.Framework.DataAccess;
using EvoS.Framework.DataAccess.Daos;
using log4net;

namespace CentralServer.LobbyServer
{
    public static class UsernameRequestManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(UsernameRequestManager));

        public enum Result
        {
            Success,
            NotFound,
            UsernameTaken
        }

        public static Result Approve(
            string code,
            long adminAccountId,
            string adminLabel,
            out RegistrationCodeDao.RegistrationCodeEntry entry)
        {
            RegistrationCodeDao dao = DB.Get().RegistrationCodeDao;
            entry = dao.Find(code);
            if (entry is null || entry.State != RegistrationCodeDao.RegistrationState.Requested)
            {
                return Result.NotFound;
            }

            if (DB.Get().LoginDao.Find(entry.IssuedTo) is not null)
            {
                return Result.UsernameTaken;
            }

            log.Info($"CONFIRM USERNAME REQUEST by {adminLabel}: {entry.IssuedTo}");
            entry.State = RegistrationCodeDao.RegistrationState.Issued;
            entry.IssuedBy = adminAccountId;
            entry.IssuedAt = DateTime.UtcNow;
            entry.ExpiresAt = EvosConfiguration.GetRegistrationCodeLifetime().Ticks > 0
                ? DateTime.UtcNow.Add(EvosConfiguration.GetRegistrationCodeLifetime())
                : DateTime.MaxValue;
            dao.Save(entry);

            DiscordManager.Get().Bot?.PingUsernameRequestApproved(entry.DiscordUserId, entry.IssuedTo);
            return Result.Success;
        }

        public static Result Decline(
            string code,
            string reason,
            long adminAccountId,
            string adminLabel,
            out RegistrationCodeDao.RegistrationCodeEntry entry)
        {
            RegistrationCodeDao dao = DB.Get().RegistrationCodeDao;
            entry = dao.Find(code);
            if (entry is null || entry.State != RegistrationCodeDao.RegistrationState.Requested)
            {
                return Result.NotFound;
            }

            log.Info($"DECLINE USERNAME REQUEST by {adminLabel}: {entry.IssuedTo}");
            entry.State = RegistrationCodeDao.RegistrationState.Declined;
            entry.IssuedBy = adminAccountId;
            entry.DeclineReason = reason;
            dao.Save(entry);

            DiscordManager.Get().Bot?.PingUsernameRequestDeclined(entry.DiscordUserId, reason);
            return Result.Success;
        }
    }
}

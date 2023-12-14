using CentralServer.LobbyServer.Session;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework;
using System.Collections.Generic;
using CentralServer.BridgeServer;
using static EvoS.Framework.DataAccess.Daos.MiscDao;

namespace CentralServer.LobbyServer.TrustWar
{
    public class TrustWarManager
    {
        private static readonly object _lock = new object();

        public static int GetTotalXPByFactionID(PersistedAccountData account, int factionID)
        {
            Dictionary<int, FactionPlayerData> factionData = account.AccountComponent.FactionCompetitionData[0].Factions;

            return factionData[factionID]?.TotalXP ?? 0;
        }

        public static TrustWarEntry getTrustWarEntry()
        {
            TrustWarEntry trustWar = DB.Get().MiscDao.GetEntry("TrustWars-Season1") as TrustWarEntry;

            if (trustWar is not null)
            {
                return trustWar;
            }

            return new TrustWarEntry()
            {
                _id = "TrustWars-Season1",
                Points = new long[] { 0, 0, 0 }
            };
        }


        public static void CalculateTrustWar(Game game, LobbyGameSummary gameSummary)
        {
            if (!LobbyConfiguration.IsTrustWarEnabled()) return;

            if (game.GameInfo.GameConfig.GameType != GameType.PvP) return;

            Dictionary<int, long> factionScores = new();
            List<PlayerFactionContributionChangeNotification> notificationsToSend = new();

            lock (_lock)
            {
                TrustWarEntry trustWar = getTrustWarEntry();

                foreach (long accountId in game.GetPlayers())
                {
                    PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
                    if (account.AccountComponent.SelectedRibbonID >= 1 && account.AccountComponent.SelectedRibbonID <= 3)
                    {
                        LobbyServerPlayerInfo player = game.GetPlayerInfo(accountId);

                        bool isTeamAWinner = (gameSummary.GameResult == GameResult.TeamAWon && player.TeamId == Team.TeamA);
                        bool isTeamBWinner = (gameSummary.GameResult == GameResult.TeamBWon && player.TeamId == Team.TeamB);

                        int trustWarPoints = isTeamAWinner || isTeamBWinner ? LobbyConfiguration.GetTrustWarGameWonPoints() : LobbyConfiguration.GetTrustWarGamePlayedPoints();                        
                        int factionId = account.AccountComponent.SelectedRibbonID - 1;

                        trustWar.Points[factionId] += trustWarPoints;

                        int xp = GetTotalXPByFactionID(account, factionId);

                        account.AccountComponent.FactionCompetitionData[0].Factions[factionId].TotalXP = xp + trustWarPoints;

                        notificationsToSend.Add(new PlayerFactionContributionChangeNotification()
                        {
                            CompetitionId = 1,
                            FactionId = factionId,
                            AmountChanged = trustWarPoints,
                            TotalXP = xp + trustWarPoints,
                            AccountID = account.AccountId,
                        });

                        DB.Get().AccountDao.UpdateAccount(account);
                    }
                }

                DB.Get().MiscDao.SaveEntry(trustWar);

                factionScores = new()
                {
                    { 0, trustWar.Points[0] },
                    { 1, trustWar.Points[1] },
                    { 2, trustWar.Points[2] }
                };
            }

            foreach (PlayerFactionContributionChangeNotification notification in notificationsToSend)
            {
                LobbyServerProtocol session = SessionManager.GetClientConnection(notification.AccountID);
                session?.Send(notification);
            }

            foreach (long playerAccountId in SessionManager.GetOnlinePlayers())
            {
                LobbyServerProtocol player = SessionManager.GetClientConnection(playerAccountId);
                player?.Send(new FactionCompetitionNotification { ActiveIndex = 1, Scores = factionScores });
            }
        }
    }
}
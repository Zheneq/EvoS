using CentralServer.LobbyServer.Session;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework;
using System.Collections.Generic;
using static EvoS.Framework.DataAccess.Daos.TrustWarDao;
using CentralServer.BridgeServer;
using System.Linq;
using System;

namespace CentralServer.LobbyServer.TrustWar
{
    public class TrustWarManager
    {
        public static int GetTotalXPByFactionID(PersistedAccountData account, int factionID)
        {
            Dictionary<int, FactionPlayerData> factionData = account.AccountComponent.FactionCompetitionData[0].Factions;

            return factionData[factionID]?.TotalXP ?? 0;
        }

        public static void CalculateTrustWar(Game game, LobbyGameSummary gameSummary)
        {
            if (LobbyConfiguration.IsTrustWarEnabled())
            {
                TrustWarDaoEntry trustWar = DB.Get().TrustWarDao.Find();
                foreach (long accountId in game.GetPlayers())
                {
                    PersistedAccountData account = DB.Get().AccountDao.GetAccount(accountId);
                    if (account.AccountComponent.SelectedRibbonID != -1) { 
                        LobbyServerPlayerInfo player = game.GetPlayerInfo(accountId);

                        bool isTeamAWinner = (gameSummary.GameResult == GameResult.TeamAWon && player.TeamId == Team.TeamA);
                        bool isTeamBWinner = (gameSummary.GameResult == GameResult.TeamBWon && player.TeamId == Team.TeamB);

                        int trustWarPoints = isTeamAWinner || isTeamBWinner ? LobbyConfiguration.GetTrustWarGameWonPoints() : LobbyConfiguration.GetTrustWarGamePlayedPoints();

                        switch (account.AccountComponent.SelectedRibbonID)
                        {
                            case 1:
                                trustWar.Warbotics += trustWarPoints;
                                break;
                            case 2:
                                trustWar.Omni += trustWarPoints;
                                break;
                            case 3:
                                trustWar.Evos += trustWarPoints;
                                break;
                        }

                        // factionId
                        // 0 = Omni, SelectedRibbonID: 1
                        // 1 = Evos, SelectedRibbonID: 2
                        // 2 = Warbotics, SelectedRibbonID: 3

                        int factionId = account.AccountComponent.SelectedRibbonID - 1;
                        int xp = LobbyServerProtocol.GetTotalXPByFactionID(account, factionId);

                        // FactionCompetitionData[0] exists because added in PatchAccountData
                        account.AccountComponent.FactionCompetitionData[0].Factions[factionId].TotalXP = xp + trustWarPoints;

                        LobbyServerProtocol session = SessionManager.GetClientConnection(accountId);

                        session.Send(new PlayerFactionContributionChangeNotification()
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
                DB.Get().TrustWarDao.Save(trustWar);

                Dictionary<int, long> factionScores = new()
                    {
                        { 0, trustWar.Omni },
                        { 1, trustWar.Evos },
                        { 2, trustWar.Warbotics }
                    };

                foreach (long playerAccountId in SessionManager.GetOnlinePlayers())
                {
                    LobbyServerProtocol player = SessionManager.GetClientConnection(playerAccountId);
                    player?.Send(new FactionCompetitionNotification { ActiveIndex = 1, Scores = factionScores });
                }
            }
        }
    }
}

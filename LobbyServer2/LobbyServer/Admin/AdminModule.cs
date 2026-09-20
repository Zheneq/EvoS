using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Session;
using EvoS.Framework;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using LobbyGameClientMessages;
using log4net;

namespace CentralServer.LobbyServer.Admin;

public class AdminModule : ILobbyModule
{
    private static readonly ILog log = LogManager.GetLogger(typeof(AdminModule));
    private readonly IClientConnection _conn;

    public AdminModule(IClientConnection conn)
    {
        _conn = conn;
    }

    public void Register(IHandlerRegistry registry)
    {
        registry.Register<DEBUG_AdminSlashCommandNotification>(HandleDEBUG_AdminSlashCommandNotification);
    }

    private void HandleDEBUG_AdminSlashCommandNotification(DEBUG_AdminSlashCommandNotification notification)
    {
        log.Info($"DEBUG_AdminSlashCommandNotification: {notification.Command}");
        PersistedAccountData account = DB.Get().AccountDao.GetAccount(_conn.AccountId);
        if (account == null)
        {
            return;
        }
        log.Info($"DEBUG_AdminSlashCommandNotification: dev={account.AccountComponent.IsDev()} devMode={EvosConfiguration.GetDevMode()}");
        if (account.AccountComponent.IsDev() || EvosConfiguration.GetDevMode())
        {
            Game game = GameManager.GetGameWithPlayer(_conn.AccountId);
            if (game != null)
            {
                Team team = game.TeamInfo.TeamPlayerInfo.Find(x => x.AccountId == _conn.AccountId).TeamId;
                switch (notification.Command)
                {
                    case "End Game (Win)":
                        game.Server.AdminShutdown(team == Team.TeamA ? GameResult.TeamAWon : GameResult.TeamBWon);
                        break;
                    case "End Game (Loss)":
                        game.Server.AdminShutdown(team == Team.TeamA ? GameResult.TeamBWon : GameResult.TeamAWon);
                        break;
                    case "End Game (No Result)":
                    case "End Game (With Parameters)":
                    case "End Game (Tie)":
                        game.Server.AdminShutdown(GameResult.TieGame);
                        break;
                    case "Cooldowns":
                        game.Server.AdminClearCooldown();
                        break;
                }
            }
            else
            {
                log.Info("DEBUG_AdminSlashCommandNotification: game not found");
            }
        }
    }
}

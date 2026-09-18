using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using log4net;

namespace CentralServer.LobbyServer.GameLifecycle;

public class GameLifecycleModule : ILobbyModule
{
    private static readonly ILog log = LogManager.GetLogger(typeof(GameLifecycleModule));
    private readonly IClientConnection _conn;

    private Game _currentGame;

    public Game CurrentGame
    {
        get => _currentGame;
        private set
        {
            if (_currentGame != value)
            {
                _currentGame = value;
                _conn.BroadcastRefreshFriendList();
                _conn.BroadcastRefreshGroup();
            }
        }
    }

    public bool IsInGame() => CurrentGame != null;

    public bool IsInCharacterSelect() => CurrentGame != null && CurrentGame.GameStatus <= GameStatus.FreelancerSelecting;

    public LobbyServerPlayerInfo PlayerInfo => CurrentGame?.GetPlayerInfo(_conn.AccountId);

    public GameLifecycleModule(IClientConnection conn)
    {
        _conn = conn;
    }

    public void Register(IHandlerRegistry registry)
    {
    }

    public void JoinGame(Game game)
    {
        Game prevServer = CurrentGame;
        CurrentGame = game;
        log.Info($"{LobbyServerUtils.GetHandle(_conn.AccountId)} joined {game?.ProcessCode} (was in {prevServer?.ProcessCode ?? "lobby"})");
    }

    public bool LeaveGame(Game game)
    {
        if (game == null)
        {
            log.Error($"{_conn.AccountId} is asked to leave null server (current server = {CurrentGame?.ProcessCode ?? "null"})");
            return false;
        }
        if (CurrentGame == null)
        {
            log.Debug($"{_conn.AccountId} is asked to leave {game.ProcessCode} while they are not on any server");
            return false;
        }
        if (CurrentGame != game)
        {
            log.Debug($"{_conn.AccountId} is asked to leave {game.ProcessCode} while they are on {CurrentGame.ProcessCode}. Ignoring.");
            return false;
        }

        CurrentGame = null;
        log.Info($"{LobbyServerUtils.GetHandle(_conn.AccountId)} leaves {game.ProcessCode}");

        // forcing catalyst panel update -- otherwise it would show catas for the character from the last game
        _conn.Send(new ForcedCharacterChangeFromServerNotification
        {
            ChararacterInfo = DB.Get().AccountDao.GetAccount(_conn.AccountId).GetCharacterInfo(),
        });

        return true;
    }
}

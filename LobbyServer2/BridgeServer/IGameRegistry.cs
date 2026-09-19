using System.Collections.Generic;
using EvoS.Framework.Constants.Enums;

namespace CentralServer.BridgeServer;

public interface IGameRegistry
{
    PvpGame CreatePvpGame();
    bool RegisterGame(string processCode, Game game);
    bool UnregisterGame(string processCode);
    Game GetGameWithPlayer(long accountId);
    void ReconnectServer(BridgeServerProtocol server);
    List<Game> GetGames();
    Dictionary<string, int> GetRunningGamesNum(GameType gameType);
    int GetRunningGamesNum();
    void StopAllGames();
}

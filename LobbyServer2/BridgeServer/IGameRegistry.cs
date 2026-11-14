using System.Collections.Generic;

namespace CentralServer.BridgeServer;

public interface IGameRegistry
{
    PvpGame CreatePvpGame();
    bool RegisterGame(string processCode, Game game);
    bool UnregisterGame(string processCode);
    Game GetGameWithPlayer(long accountId);
    List<Game> GetGames();
    Dictionary<string, int> GetRunningGamesNum(GameType gameType);
    int GetRunningGamesNum();
    void StopAllGames();
}

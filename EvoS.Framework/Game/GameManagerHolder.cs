using System.Collections.Generic;
using System.Linq;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Misc;
using EvoS.Framework.Network.Game.Messages;
using EvoS.Framework.Network.Static;

namespace EvoS.Framework.Game
{
    public class GameManagerHolder
    {
        private static Dictionary<string, GameManager> _gameManagers = new Dictionary<string, GameManager>();

        public static GameManager? FindGameManager(LoginRequest loginRequest)
        {
            // TODO this is only suitable for solo
            if (!_gameManagers.ContainsKey(loginRequest.AccountId))
            {
                _gameManagers.Add(loginRequest.AccountId, new GameManager());
                var x = _gameManagers[loginRequest.AccountId];
                x.SetTeamInfo(new LobbyServerTeamInfo());
                    x.TeamInfo.TeamPlayerInfo.Add(new LobbyServerPlayerInfo
                    {
                        TeamId = Team.TeamA
                    });
                x.SetGameInfo(new LobbyGameInfo
                {
                    GameConfig = new LobbyGameConfig
                    {
                        Map = "VR_Practice"
//                        Map = "CargoShip_Deathmatch"
//                        Map = "Casino01_Deathmatch"
//                        Map = "EvosLab_Deathmatch"
//                        Map = "Oblivion_Deathmatch"
//                        Map = "Reactor_Deathmatch"
//                        Map = "RobotFactory_Deathmatch"
//                        Map = "Skyway_Deathmatch"
                    }
                });
                x.LaunchGame();
            }

            return _gameManagers[loginRequest.AccountId];
        }

        public static void CreateMatch(LobbyGameInfo gameInfo, LobbyServerTeamInfo teamInfo)
        {
            var game = new GameManager();
            // TODO setup GameManager instance
            game.SetTeamInfo(teamInfo);
            game.SetGameInfo(new LobbyGameInfo
            {
                GameConfig = new LobbyGameConfig
                {
                    Map = "VR_Practice"
                    // Map = "CargoShip_Deathmatch"
                    // Map = "Casino01_Deathmatch"
                    // Map = "EvosLab_Deathmatch"
                    // Map = "Oblivion_Deathmatch"
                    // Map = "Reactor_Deathmatch"
                    // Map = "RobotFactory_Deathmatch"
                    // Map = "Skyway_Deathmatch"
                }
            });
            game.LaunchGame();
            
            foreach (var playerInfo in teamInfo.TeamAPlayerInfo.Union(teamInfo.TeamPlayerInfo))
            {
                
                _gameManagers.Add(playerInfo.AccountId.ToString(), game);
            }
        }
    }
}

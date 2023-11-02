using System;
using System.Collections.Generic;
using System.Linq;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.LobbyServer.CustomGames
{
    public static class CustomGameManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CustomGameManager));

        private static readonly Dictionary<long, CustomGame> Games = new Dictionary<long, CustomGame>();
        private static readonly Dictionary<string, CustomGame> GamesByCode = new Dictionary<string, CustomGame>();
        private static readonly Dictionary<long, LobbyServerProtocol> Subscribers = new Dictionary<long, LobbyServerProtocol>();

        public static Game CreateGame(long accountId, LobbyGameConfig gameConfig)
        {
            CustomGame game;
            lock (Games)
            {
                DeleteGame(accountId);
                try
                {
                    game = new CustomGame(accountId, gameConfig);
                    Games.Add(accountId, game);
                    GamesByCode.Add(game.ProcessCode, game);
                    if (!GameManager.RegisterGame(game.ProcessCode, game))
                    {
                        log.Error("Failed to register a custom game");
                        return null;
                    }
                }
                catch (Exception e)
                {
                    log.Error("Failed to create a custom game", e);
                    return null;
                }
            }
            log.Info($"{LobbyServerUtils.GetHandle(accountId)} created a custom game {game.GameInfo.GameServerProcessCode}");
            NotifyUpdate();
            return game;
        }

        public static void DeleteGame(long accountId)
        {
            lock (Games)
            {
                if (Games.Remove(accountId, out CustomGame oldGame))
                {
                    GamesByCode.Remove(oldGame.GameInfo.GameServerProcessCode);
                    oldGame.Terminate();
                }
            }
            NotifyUpdate();
        }

        private static CustomGame GetGame(string processCode)
        {
            GamesByCode.TryGetValue(processCode, out CustomGame game);
            return game;
        }

        private static CustomGame GetGame(long accountId)
        {
            Games.TryGetValue(accountId, out CustomGame game);
            return game;
        }

        public static List<Game> GetGames()
        {
            return Games.Values.Select(x => (Game)x).ToList();
        }

        public static Game GetMyGame(long accountId)
        {
            Games.TryGetValue(accountId, out CustomGame game);
            return game;
        }

        public static void Subscribe(LobbyServerProtocol client)
        {
            lock (Subscribers)
            {
                Subscribers[client.AccountId] = client;
                client.Send(MakeNotification());
            }
        }

        public static void Unsubscribe(LobbyServerProtocol client)
        {
            lock (Subscribers)
            {
                Subscribers.Remove(client.AccountId);
            }
        }

        public static void NotifyUpdate()
        {
            LobbyCustomGamesNotification notify = MakeNotification();
            List<long> toRemove = new List<long>();
            lock (Subscribers)
            {
                foreach ((long key, LobbyServerProtocol value) in Subscribers)
                {
                    if (value is null || !value.IsConnected)
                    {
                        toRemove.Add(key);
                    }
                    else
                    {
                        value.Send(notify);
                    }
                }
                
                toRemove.ForEach(key => Subscribers.Remove(key));
            }
        }

        private static LobbyCustomGamesNotification MakeNotification()
        {
            return new LobbyCustomGamesNotification
            {
                CustomGameInfos = Games.Values.Select(g => g.GameInfo).ToList()
            };
        }

        public static bool UpdateGameInfo(long accountId, LobbyGameInfo gameInfo, LobbyTeamInfo teamInfo)
        {
            CustomGame game = GetGame(accountId);
            if (game is null) return false;
            try
            {
                game.UpdateGameInfo(gameInfo, teamInfo);
            }
            catch (Exception e)
            {
                log.Error("Failed to update game info", e);
                return false;
            }
            NotifyUpdate();
            return true;
        }

        public static bool BalanceTeams(long accountId, List<BalanceTeamSlot> slots)
        {
            CustomGame game = GetGame(accountId);
            if (game is null) return false;
            try
            {
                game.BalanceTeams(slots);
            }
            catch (Exception e)
            {
                log.Error("Failed to balance teams", e);
                return false;
            }
            return true;
        }

        public static Game JoinGame(long accountId, string processCode, bool asSpectator, out LocalizationPayload localizedFailure)
        {
            CustomGame game = GetGame(processCode);
            if (game == null)
            {
                localizedFailure = LocalizationPayload.Create("UnknownErrorTryAgain@Frontend");
                return null;
            }
            if (asSpectator && game.GameInfo.GameConfig.Spectators == game.TeamInfo.SpectatorInfo.Count())
            {
                localizedFailure = LocalizationPayload.Create("GameCreatorNoLongerHasAGameForYou@Invite");
                return null;
            }
            if (!asSpectator && game.GameInfo.GameConfig.TotalPlayers == (game.TeamInfo.TeamAPlayerInfo.Count() + game.TeamInfo.TeamBPlayerInfo.Count()))
            {
                localizedFailure = LocalizationPayload.Create("GameCreatorNoLongerHasAGameForYou@Invite");
                return null;
            }

            if (game.Join(accountId, asSpectator))
            {
                localizedFailure = null;
                NotifyUpdate();
                return game;
            }
            else
            {
                localizedFailure = LocalizationPayload.Create("UnknownErrorTryAgain@Frontend");
                return null;
            }
        }
    }
}

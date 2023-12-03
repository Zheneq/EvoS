using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using log4net;
using ILog = log4net.ILog;

namespace CentralServer.LobbyServer.CustomGames
{
    public static class CustomGameManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CustomGameManager));

        private static readonly Dictionary<long, CustomGame> Games = new Dictionary<long, CustomGame>();
        private static readonly Dictionary<string, CustomGame> GamesByCode = new Dictionary<string, CustomGame>();
        private static readonly Dictionary<long, LobbyServerProtocol> Subscribers = new Dictionary<long, LobbyServerProtocol>();

        public static async Task<Game> CreateGame(long accountId, LobbyGameConfig gameConfig)
        {
            CustomGame game;
            lock (Games)
            {
                DeleteGame(accountId).Wait();
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
            log.Info($"{LobbyServerUtils.GetHandle(accountId)} created a custom game {game.ProcessCode}");
            await game.InitGame();
            await NotifyUpdate();
            return game;
        }

        public static async Task DeleteGame(long accountId)
        {
            CustomGame oldGame = null;
            lock (Games)
            {
                if (Games.Remove(accountId, out oldGame))
                {
                    GamesByCode.Remove(oldGame.ProcessCode);
                    log.Info($"Removing {oldGame.ProcessCode}");
                }
            }

            if (oldGame is not null)
            {
                await oldGame.Terminate();
            }
            await NotifyUpdate();
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

        public static async Task Subscribe(LobbyServerProtocol client)
        {
            lock (Subscribers)
            {
                Subscribers[client.AccountId] = client;
            }
            await client.Send(MakeNotification());
        }

        public static void Unsubscribe(LobbyServerProtocol client)
        {
            lock (Subscribers)
            {
                Subscribers.Remove(client.AccountId);
            }
        }

        public static async Task NotifyUpdate()
        {
            LobbyCustomGamesNotification notify = MakeNotification();
            List<long> toRemove = new List<long>();
            List<LobbyServerProtocol> toSend = new List<LobbyServerProtocol>();
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
                        toSend.Add(value);
                    }
                }
                
                toRemove.ForEach(key => Subscribers.Remove(key));
            }
            foreach (LobbyServerProtocol conn in toSend)
            {
                await conn.Send(notify);
            }
        }

        private static LobbyCustomGamesNotification MakeNotification()
        {
            return new LobbyCustomGamesNotification
            {
                CustomGameInfos = Games.Values.Select(g => g.GameInfo).ToList()
            };
        }

        public static async Task<bool> UpdateGameInfo(long accountId, LobbyGameInfo gameInfo, LobbyTeamInfo teamInfo)
        {
            CustomGame game = GetGame(accountId);
            if (game is null) return false;
            try
            {
                await game.UpdateGameInfo(gameInfo, teamInfo);
            }
            catch (Exception e)
            {
                log.Error("Failed to update game info", e);
                return false;
            }
            await NotifyUpdate();
            return true;
        }

        public static async Task<bool> BalanceTeams(long accountId, List<BalanceTeamSlot> slots)
        {
            CustomGame game = GetGame(accountId);
            if (game is null) return false;
            try
            {
                await game.BalanceTeams(slots);
            }
            catch (Exception e)
            {
                log.Error("Failed to balance teams", e);
                return false;
            }
            return true;
        }

        public static async Task<(Game, LocalizationPayload)> JoinGame(long accountId, string processCode, bool asSpectator)
        {
            CustomGame game = GetGame(processCode);
            if (game == null)
            {
                return (null, LocalizationPayload.Create("UnknownErrorTryAgain@Frontend"));
            }
            if (asSpectator && game.GameInfo.GameConfig.Spectators == game.TeamInfo.SpectatorInfo.Count())
            {
                return (null, LocalizationPayload.Create("GameCreatorNoLongerHasAGameForYou@Invite"));
            }
            if (!asSpectator && game.GameInfo.GameConfig.TotalPlayers == (game.TeamInfo.TeamAPlayerInfo.Count() + game.TeamInfo.TeamBPlayerInfo.Count()))
            {
                return (null, LocalizationPayload.Create("GameCreatorNoLongerHasAGameForYou@Invite"));
            }

            if (await game.Join(accountId, asSpectator))
            {
                await NotifyUpdate();
                return (game, null);
            }
            else
            {
                return (null, LocalizationPayload.Create("UnknownErrorTryAgain@Frontend"));
            }
        }
    }
}

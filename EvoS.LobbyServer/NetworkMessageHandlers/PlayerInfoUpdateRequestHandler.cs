using System;
using System.Threading.Tasks;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Logging;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.LobbyServer.Utils;
using Newtonsoft.Json;

namespace EvoS.LobbyServer.NetworkMessageHandlers
{
    class PlayerInfoUpdateRequestHandler : IEvosNetworkMessageHandler
    {
        public async Task OnMessage(ClientConnection connection, object requestData)
        {
            /*
             * The clients sends this request everytime something important to loadout configuration changes
             * apparently it only sends in PlayerInfoUpdate the thing that was modified and everything else is is null
             * 
             * ie: if the client changes freelancer, it only sends the new value of CharacterType and all other values are null
             */
            PlayerInfoUpdateRequest request = (PlayerInfoUpdateRequest) requestData;
            if (request.GameType != null)
                connection.SelectedGameType = request.GameType.Value;



            if (request.PlayerInfoUpdate.ContextualReadyState != null)
            {
                if (request.PlayerInfoUpdate.ContextualReadyState.Value.ReadyState == ReadyState.Ready)
                {
                    var practice = DummyLobbyData.CreatePracticeGameNotification(connection);
                    Log.Print(LogType.Network, $"Responding {JsonConvert.SerializeObject(practice)}");
                    await connection.SendMessage(practice);

                    var practiceGameInfo = DummyLobbyData.CreatePracticeGameInfoNotification(connection);
                    Log.Print(LogType.Network, $"Responding {JsonConvert.SerializeObject(practiceGameInfo)}");
                    await connection.SendMessage(practiceGameInfo);
                    return;
                }
            }

            // Change Selected Freelancer
            else if (request.PlayerInfoUpdate.CharacterType != null)
            {
                connection.SelectedCharacter = request.PlayerInfoUpdate.CharacterType.Value;
                PlayerData.SaveSelectedCharacter(connection.AccountId, (int)connection.SelectedCharacter);

                var accountDataUpdate = new PlayerAccountDataUpdateNotification
                {
                    AccountData = PlayerUtils.GetAccountData(connection)
                };
                Log.Print(LogType.Debug, $"Responding {JsonConvert.SerializeObject(accountDataUpdate)}");
                await connection.SendMessage(accountDataUpdate);
                var response = new PlayerInfoUpdateResponse
                {
                    CharacterInfo = DummyLobbyData.CreateLobbyCharacterInfo(connection.SelectedCharacter),
                    OriginalPlayerInfoUpdate = request.PlayerInfoUpdate,
                    ResponseId = request.RequestId
                };
                await connection.SendMessage(response);
                return;
            }

            else if (request.PlayerInfoUpdate.CharacterSkin != null)
            {
                connection.Loadout.Skin = request.PlayerInfoUpdate.CharacterSkin.Value;
            }
            else if (request.PlayerInfoUpdate.CharacterCards != null)
            {
                connection.Loadout.Cards = request.PlayerInfoUpdate.CharacterCards.Value;
            }

            else if (request.PlayerInfoUpdate.CharacterMods != null)
            {
                connection.Loadout.Mods = request.PlayerInfoUpdate.CharacterMods.Value;
            }

            else if (request.PlayerInfoUpdate.CharacterAbilityVfxSwaps != null)
            {
                connection.Loadout.AbilityVfxSwaps = request.PlayerInfoUpdate.CharacterAbilityVfxSwaps.Value;
            }

            else if (request.PlayerInfoUpdate.CharacterLoadoutChanges != null)
            {
                //
            }

            /*if (request.PlayerInfoUpdate.CharacterType == null)
            {
                Log.Print(LogType.Warning, "CharacterType is null in PlayerInfoUpdateRequest");
                return;
            }/

            var accountDataUpdate = new PlayerAccountDataUpdateNotification
            {
                AccountData = PlayerUtils.GetAccountData(connection)
            };
            accountDataUpdate.AccountData.AccountComponent.LastCharacter = request.PlayerInfoUpdate.CharacterType.Value;
            Log.Print(LogType.Network, $"Responding {JsonConvert.SerializeObject(accountDataUpdate)}");
            await connection.SendMessage(accountDataUpdate);

            var response = new PlayerInfoUpdateResponse
            {
                CharacterInfo = DummyLobbyData.CreateLobbyCharacterInfo(request.PlayerInfoUpdate.CharacterType.Value),
                // PlayerInfo = DummyLobbyData.CreateLobbyPlayerInfo(),
                OriginalPlayerInfoUpdate = request.PlayerInfoUpdate,
                ResponseId = request.RequestId
            };

            if (request.PlayerInfoUpdate.CharacterCards != null)
                response.CharacterInfo.CharacterCards = request.PlayerInfoUpdate.CharacterCards.Value;

            if (request.PlayerInfoUpdate.CharacterMods != null)
                response.CharacterInfo.CharacterMods = request.PlayerInfoUpdate.CharacterMods.Value;

            // response.PlayerInfo.PlayerId = request.PlayerInfoUpdate.PlayerId;
            // response.PlayerInfo.CharacterInfo = response.CharacterInfo;
            Log.Print(LogType.Network, $"Responding {JsonConvert.SerializeObject(response)}");
            await connection.SendMessage(response);
            */
            /*try
            {
                var response = new PlayerInfoUpdateResponse
                {
                    //PlayerInfo = null,
                    CharacterInfo = DummyLobbyData.CreateLobbyCharacterInfo(connection.SelectedCharacter),
                    OriginalPlayerInfoUpdate = request.PlayerInfoUpdate,
                    ResponseId = request.RequestId
                };
                await connection.SendMessage(response);
            }
            catch(Exception e) {
                Log.Print(LogType.Debug, e.Message);
            }
            */
        }
    }
}

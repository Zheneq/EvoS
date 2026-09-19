using System;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.GameLifecycle;
using CentralServer.LobbyServer.Matchmaking;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.DataAccess;
using EvoS.Framework.Network.NetworkMessages;
using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.WebSocket;
using log4net;

namespace CentralServer.LobbyServer.Character;

public class CharacterModule : ILobbyModule
{
    private static readonly ILog log = LogManager.GetLogger(typeof(CharacterModule));
    private readonly IClientConnection _conn;
    private readonly MatchmakingModule _matchmaking;
    private readonly GameLifecycleModule _gameLifecycle;

    public CharacterModule(IClientConnection conn, MatchmakingModule matchmaking, GameLifecycleModule gameLifecycle)
    {
        _conn = conn;
        _matchmaking = matchmaking;
        _gameLifecycle = gameLifecycle;
    }

    public void Register(IHandlerRegistry registry)
    {
        registry.Register<PlayerInfoUpdateRequest>(HandlePlayerInfoUpdateRequest);
        registry.Register<UpdateRemoteCharacterRequest>(HandleUpdateRemoteCharacterRequest);
    }

    private void HandlePlayerInfoUpdateRequest(PlayerInfoUpdateRequest request)
    {
        LobbyPlayerInfoUpdate update = request.PlayerInfoUpdate;
        LobbyServerPlayerInfo playerInfo = update.PlayerId == 0 ? _gameLifecycle.PlayerInfo : _gameLifecycle.CurrentGame?.GetPlayerById(update.PlayerId);
        bool updateSelectedCharacter = playerInfo == null && update.PlayerId == 0;

        PersistedAccountData account;
        if (playerInfo is not null)
        {
            account = DB.Get().AccountDao.GetAccount(playerInfo.AccountId);
        }
        else
        {
            account = DB.Get().AccountDao.GetAccount(_conn.AccountId);
            playerInfo = LobbyServerPlayerInfo.Of(account);
        }

        // TODO validate what player has purchased

        // building character info to validate it for current game
        CharacterType characterType = update.CharacterType ?? playerInfo.CharacterType;

        log.Debug($"HandlePlayerInfoUpdateRequest characterType={characterType} " +
                 $"(update={update.CharacterType} " +
                 $"server={playerInfo.CharacterType} " +
                 $"account={account?.AccountComponent.LastCharacter})");

        bool characterDataUpdate = false;
        CharacterComponent characterComponent;
        LobbyCharacterInfo characterInfo;
        if (account is not null)
        {
            characterComponent = (CharacterComponent)account.CharacterData[characterType].CharacterComponent.Clone();
            characterDataUpdate = ApplyCharacterDataUpdate(characterComponent, update);
            characterInfo = LobbyCharacterInfo.Of(account.CharacterData[characterType], characterComponent);
        }
        else
        {
            characterComponent = EvoS.DirectoryServer.Character.CharacterManager.GetCharacterComponent(0, characterType);
            ApplyCharacterDataUpdate(characterComponent, update);
            characterInfo = LobbyCharacterInfo.Of(new PersistedCharacterData(characterType), characterComponent);
        }

        if (_gameLifecycle.CurrentGame != null)
        {
            if (!_gameLifecycle.CurrentGame.UpdateCharacterInfo(_conn.AccountId, characterInfo, update))
            {
                _conn.Send(new PlayerInfoUpdateResponse
                {
                    Success = false,
                    ResponseId = request.RequestId
                });
                return;
            }
        }
        else
        {
            playerInfo.CharacterInfo = characterInfo;
        }

        // persisting changes
        if (account is not null && account.AccountId == _conn.AccountId)
        {
            if (updateSelectedCharacter && update.CharacterType.HasValue)
            {
                account.AccountComponent.LastCharacter = update.CharacterType.Value;
                DB.Get().AccountDao.UpdateLastCharacter(account);
            }

            if (characterDataUpdate)
            {
                account.CharacterData[characterType].CharacterComponent = characterComponent;
                DB.Get().AccountDao.UpdateCharacterComponent(account, characterType);
            }

            if (GroupManager.GetPlayerGroup(_conn.AccountId).IsSolo() && request.GameType != null && request.GameType.HasValue)
            {
                _matchmaking.SetGameType(request.GameType.Value);
            }

            // Unselect a dublicated remotecharacter to none if we pick it ourself
            // This always runs not sure i can catch the difrence between Coop/PvP and Custom
            // And Deathmatch vs ControlAllBots
            // Before we send PlayerAccountDataUpdateNotification
            int index = account.AccountComponent.LastRemoteCharacters.IndexOf(characterType);

            if (index != -1)
            {
                account.AccountComponent.LastRemoteCharacters[index] = CharacterType.None;
            }

            // without this client instantly resets character type back to what it was
            if (update.CharacterType != null && update.CharacterType.HasValue)
            {
                PlayerAccountDataUpdateNotification updateNotification = new PlayerAccountDataUpdateNotification(account);
                _conn.Send(updateNotification);
            }

            if (update.AllyDifficulty != null && update.AllyDifficulty.HasValue)
                _matchmaking.SetAllyDifficulty(update.AllyDifficulty.Value);
            if (update.ContextualReadyState != null && update.ContextualReadyState.HasValue)
                _matchmaking.SetContextualReadyState(update.ContextualReadyState.Value);
            if (update.EnemyDifficulty != null && update.EnemyDifficulty.HasValue)
                _matchmaking.SetEnemyDifficulty(update.EnemyDifficulty.Value);
        }

        _conn.Send(new PlayerInfoUpdateResponse
        {
            PlayerInfo = LobbyPlayerInfo.FromServer(playerInfo, 0, new MatchmakingQueueConfig()),
            CharacterInfo = account?.AccountId == _conn.AccountId ? playerInfo.CharacterInfo : null,
            OriginalPlayerInfoUpdate = update,
            ResponseId = request.RequestId
        });
        _conn.BroadcastRefreshGroup();
    }

    private static bool ApplyCharacterDataUpdate(
        CharacterComponent characterComponent,
        LobbyPlayerInfoUpdate update)
    {
        bool characterDataUpdate = false;

        if (update.CharacterSkin.HasValue)
        {
            characterComponent.LastSkin = update.CharacterSkin.Value;
            characterDataUpdate = true;
        }
        if (update.CharacterCards.HasValue)
        {
            characterComponent.LastCards = update.CharacterCards.Value;
            characterDataUpdate = true;
        }
        if (update.CharacterMods.HasValue)
        {
            characterComponent.LastMods = update.CharacterMods.Value;
            characterDataUpdate = true;
        }
        if (update.CharacterAbilityVfxSwaps.HasValue)
        {
            characterComponent.LastAbilityVfxSwaps = update.CharacterAbilityVfxSwaps.Value;
            characterDataUpdate = true;
        }
        if (update.CharacterLoadoutChanges.HasValue)
        {
            characterComponent.CharacterLoadouts = update.CharacterLoadoutChanges.Value.CharacterLoadoutChanges;
            characterDataUpdate = true;
        }
        if (update.LastSelectedLoadout.HasValue)
        {
            characterComponent.LastSelectedLoadout = update.LastSelectedLoadout.Value;
            characterDataUpdate = true;
        }

        return characterDataUpdate;
    }

    private void HandleUpdateRemoteCharacterRequest(UpdateRemoteCharacterRequest request)
    {
        UpdateRemoteCharacterResponse response = new UpdateRemoteCharacterResponse
        {
            ResponseId = request.RequestId,
            Success = false,
        };

        if (request.RemoteSlotIndexes.Length == 0 || request.Characters.Length == 0)
        {
            log.Warn("No characters or slots provided in the request.");
            _conn.Send(response);
            return;
        }

        PersistedAccountData account = DB.Get().AccountDao.GetAccount(_conn.AccountId);
        int maxSlots = 3;
        int[] slots = request.RemoteSlotIndexes;
        CharacterType[] characterTypes = request.Characters;
        int totalSlots = Math.Min(slots.Length, characterTypes.Length);

        bool updated = UpdateCharacterSlots(account, slots, characterTypes, totalSlots, maxSlots);

        if (updated)
        {
            DB.Get().AccountDao.UpdateAccount(account);
            response.Success = true;
            PlayerAccountDataUpdateNotification updateNotification = new PlayerAccountDataUpdateNotification(account);
            _conn.Send(updateNotification);
        }

        _conn.Send(response);
    }

    private bool UpdateCharacterSlots(PersistedAccountData account, int[] slots, CharacterType[] characterTypes, int totalSlots, int maxSlots)
    {
        bool updated = false;

        for (int i = 0; i < totalSlots; i++)
        {
            int slotIndex = slots[i];
            CharacterType newCharacter = characterTypes[i];

            // Skip if slot exceeds the maximum allowed slots
            if (slotIndex >= maxSlots)
            {
                log.Warn($"Slot index {slotIndex} exceeds the maximum allowed slots.");
                continue;
            }

            // Never allow these
            if (newCharacter == CharacterType.PendingWillFill
                || newCharacter == CharacterType.TestFreelancer1
                || newCharacter == CharacterType.TestFreelancer2)
            {
                _conn.Send(new ChatNotification
                {
                    ConsoleMessageType = ConsoleMessageType.SystemMessage,
                    Text = "This character is not allowed (for draft purposes only)"
                });
                continue;
            }

            // Expand the LastRemoteCharacters list if necessary
            while (account.AccountComponent.LastRemoteCharacters.Count <= slotIndex)
            {
                account.AccountComponent.LastRemoteCharacters.Add(CharacterType.None);
            }

            // Update if the character in the slot has changed
            if (account.AccountComponent.LastRemoteCharacters[slotIndex] != newCharacter)
            {
                account.AccountComponent.LastRemoteCharacters[slotIndex] = newCharacter;
                updated = true;
                log.Info($"Updated slot {slotIndex} to character {newCharacter} for account {_conn.AccountId}.");
            }
        }

        return updated;
    }
}

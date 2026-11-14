using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CentralServer.LobbyServer.Character;
using EvoS.Framework.Constants.Enums;
using EvoS.Framework.Network.Static;
using log4net;

namespace CentralServer.BridgeServer;

// State of a draft (ranked freelancer selection) and the loop that runs its phases
public class DraftController(IDraftHost host, TimeProvider timeProvider = null)
{
    private static readonly ILog log = LogManager.GetLogger(typeof(DraftController));

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private RankedResolutionPhaseData RankedResolutionPhaseData = new();
    // The current sub-phase: when it started, how long it lasts,
    // and what ends it early (everyone on deck has locked in, or the match is canceled)
    private long _subPhaseStart;
    private TimeSpan _subPhaseTimeout;
    private CancellationTokenSource _subPhaseEnd = new();
    // Whether the current sub-phase still takes bans, lock-ins and trades
    private bool _subPhaseOpen;
    // Guards all draft state, for client requests and the phase loop alike.
    // Never held while canceling the match or across an await.
    private readonly Lock _lock = new();
    private static readonly TimeSpan DraftEndDelay = TimeSpan.FromSeconds(2);
    public FreelancerResolutionPhaseSubType PhaseSubType { get; internal set; } = FreelancerResolutionPhaseSubType.UNDEFINED;
    private int PlayersInDeck { set; get; }

    private TimeSpan TimeLeftInSubPhase
    {
        get
        {
            TimeSpan timeLeft = _subPhaseTimeout - _timeProvider.GetElapsedTime(_subPhaseStart);
            return timeLeft > TimeSpan.Zero ? timeLeft : TimeSpan.Zero;
        }
    }

    private bool IsMatchCancelled => host.CancellationToken.IsCancellationRequested;

    public async Task RunAsync()
    {
        List<KeyValuePair<int, FreelancerResolutionPhaseSubType>> phases;
        lock (_lock)
        {
            // Initialize unselected player states
            var unselectedPlayerStates = host.Roster.Select(player =>
                new RankedResolutionPlayerState
                {
                    PlayerId = player.PlayerId,
                    Intention = CharacterType.None,
                    OnDeckness = RankedResolutionPlayerState.ReadyState.Unselected,
                }).ToList();

            // Initialize ranked resolution phase data
            RankedResolutionPhaseData = new RankedResolutionPhaseData
            {
                TimeLeftInSubPhase = TimeSpan.Zero,
                UnselectedPlayerStates = unselectedPlayerStates,
                PlayersOnDeck = new List<RankedResolutionPlayerState>(),
                FriendlyBans = new List<CharacterType>(),
                EnemyBans = new List<CharacterType>(),
                FriendlyTeamSelections = new Dictionary<int, CharacterType>(),
                EnemyTeamSelections = new Dictionary<int, CharacterType>(),
                TradeActions = new List<RankedTradeData>(),
                PlayerIdByImporance = host.Roster.Select(p => p.PlayerId).ToList(),
            };

            // Define phase order
            // A player controlling all characters of their team has no one to trade with
            phases = GetDraftPhases(host.Roster, host.HasBans, hasTrade: !host.IsControlAllBots);
        }

        bool sendGameInfoNotify = true;

        // Process each phase
        foreach (KeyValuePair<int, FreelancerResolutionPhaseSubType> subPhase in phases)
        {
            if (!host.CheckParticipantsConnected())
            {
                return;
            }

            LobbyServerPlayerInfo player1;
            bool isDeckReady;
            lock (_lock)
            {
                PhaseSubType = subPhase.Value;

                player1 = GetPlayerInfo(subPhase.Key, host.Roster);
                LobbyServerPlayerInfo player2 = GetPlayerInfo(subPhase.Key + 1, host.Roster);

                if (PhaseSubType == FreelancerResolutionPhaseSubType.FREELANCER_TRADE || player1 == null)
                {
                    // Mostly when subPhase.Key is -1 (FREELANCER_TRADE) we still need a player1
                    player1 = new LobbyServerPlayerInfo();
                }

                RankedResolutionPhaseData.PlayersOnDeck.Clear(); // Reset deck (People who can do stuff, ban or select a freelancer those are the PlayersOnDeck)
                PlayersInDeck = 0;
                isDeckReady = subPhase.Key == -1 || AddPlayersToDeckBasedOnPhase(PhaseSubType, player1, player2);
                if (isDeckReady)
                {
                    OpenSubPhase(player1);
                }
            }

            // Canceling the match reaches far beyond the draft, so it is done outside the lock
            if (!isDeckReady)
            {
                host.CancelMatch();
                return;
            }

            if (sendGameInfoNotify)
            {
                host.SendGameInfoNotifications();
                sendGameInfoNotify = false;
            }

            await MonitorSubPhaseAsync(_subPhaseTimeout, player1);
            if (IsMatchCancelled)
            {
                return;
            }

            LobbyServerPlayerInfo afkPlayer;
            lock (_lock)
            {
                afkPlayer = CloseSubPhase();
            }

            if (afkPlayer is not null)
            {
                host.CancelMatch(afkPlayer.Handle, afkPlayer.AccountId);
                return;
            }
        }

        // Let clients play the lock in animation of the last pick before the loadout phase starts
        if (PhaseSubType == FreelancerResolutionPhaseSubType.PICK_FREELANCER1
            || PhaseSubType == FreelancerResolutionPhaseSubType.PICK_FREELANCER2)
        {
            await Task.Delay(DraftEndDelay, _timeProvider, host.CancellationToken)
                .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            if (IsMatchCancelled)
            {
                return;
            }
        }

        lock (_lock)
        {
            // Update TeamInfo with new values from rankedResolutionPhaseData.FriendlyTeamSelections
            UpdateTeamSelection(RankedResolutionPhaseData.FriendlyTeamSelections);

            // Update TeamInfo with new values from rankedResolutionPhaseData.EnemyTeamSelections
            UpdateTeamSelection(RankedResolutionPhaseData.EnemyTeamSelections);
        }
    }

    // Starts the timer of the current sub-phase and opens it for players' requests. Under the lock.
    private void OpenSubPhase(LobbyServerPlayerInfo player)
    {
#if DEBUG
        log.Info($"Starting ranked resolution sub phase {PhaseSubType} for Team {player.TeamId}");
#endif
        _subPhaseTimeout = GetSubPhaseTimeout(PhaseSubType);
        _subPhaseStart = _timeProvider.GetTimestamp();
        // Not disposed: cancellation runs its callbacks asynchronously and may still be using it.
        // All it holds is a registration on the match's token, which goes away with the game.
        _subPhaseEnd = CancellationTokenSource.CreateLinkedTokenSource(host.CancellationToken);
        _subPhaseOpen = true;
        SendRankedResolutionSubPhase();
    }

    // Puts the draft in the given sub-phase with the given state, open for requests, as if the phase loop had opened it
    internal void OpenSubPhaseForTest(FreelancerResolutionPhaseSubType phase, RankedResolutionPhaseData data)
    {
        lock (_lock)
        {
            PhaseSubType = phase;
            RankedResolutionPhaseData = data;
            PlayersInDeck = data.PlayersOnDeck.Count(p => p.OnDeckness == RankedResolutionPlayerState.ReadyState.Selected);
            _subPhaseOpen = true;
        }
    }

    // Closes the current sub-phase to players' requests and locks in for those who did not lock in themselves.
    // Returns a player who had to pick and did not even hover a character: the match has to be canceled then.
    // Under the lock.
    private LobbyServerPlayerInfo CloseSubPhase()
    {
        _subPhaseOpen = false;

        HashSet<CharacterType> usedCharacterTypes = GetUsedCharacterTypes();
        bool isBanPhase = PhaseSubType == FreelancerResolutionPhaseSubType.PICK_BANS1
                          || PhaseSubType == FreelancerResolutionPhaseSubType.PICK_BANS2;
        bool isPickPhase = PhaseSubType == FreelancerResolutionPhaseSubType.PICK_FREELANCER1
                           || PhaseSubType == FreelancerResolutionPhaseSubType.PICK_FREELANCER2;

        // Decide what to lock in for those who did not lock in themselves
        List<int> autoSelectedDeckIndexes = new();
        bool isIntentionChanged = false;
        for (int deckIndex = 0; deckIndex < RankedResolutionPhaseData.PlayersOnDeck.Count; deckIndex++)
        {
            RankedResolutionPlayerState playersInDeck = RankedResolutionPhaseData.PlayersOnDeck[deckIndex];
            // Selected is when did not lock in or clicked ban button
            if (playersInDeck.OnDeckness != RankedResolutionPlayerState.ReadyState.Selected)
            {
                continue;
            }

            LobbyServerPlayerInfo player = GetPlayerInfo(playersInDeck.PlayerId - 1, host.Roster);
            CharacterType characterType = playersInDeck.Intention;
            // Let bans be random if they do not select a freelancer (or it was taken in the meantime)
            if (isBanPhase && (characterType == CharacterType.None || usedCharacterTypes.Contains(characterType)))
            {
                characterType = AssignRandomCharacterForDraft(player, usedCharacterTypes);
            }
            else if (characterType == CharacterType.None)
            {
                // Cancel Match AFK Player
                return player;
            }
            //Player selected fill but did not lock in, or edge case where "MAYBE" we still have fill
            else if (characterType == CharacterType.PendingWillFill
                     || characterType == CharacterType.TestFreelancer1
                     || characterType == CharacterType.TestFreelancer2)
            {
                characterType = AssignRandomCharacterForDraft(player, usedCharacterTypes, characterType);
            }
            // Hovered a character that was taken in the meantime, e.g. by a teammate on deck at the same time
            else if (usedCharacterTypes.Contains(characterType))
            {
                characterType = isPickPhase && player.IsAIControlled
                    ? PickDraftCharacterForBot(player, usedCharacterTypes)
                    : AssignRandomCharacterForDraft(player, usedCharacterTypes);
            }

            // Several players can be on deck at the same time
            usedCharacterTypes.Add(characterType);
            autoSelectedDeckIndexes.Add(deckIndex);
            if (characterType != playersInDeck.Intention)
            {
                playersInDeck.Intention = characterType;
                RankedResolutionPhaseData.PlayersOnDeck[deckIndex] = playersInDeck;
                isIntentionChanged = true;
            }
        }

        // Show random characters as if they were hovered before locking them in,
        // clients take the character for the lock in animation from the hover
        if (isIntentionChanged)
        {
            SendRankedResolutionSubPhase();
        }

        foreach (int deckIndex in autoSelectedDeckIndexes)
        {
            RankedResolutionPlayerState playersInDeck = RankedResolutionPhaseData.PlayersOnDeck[deckIndex];
            LobbyServerPlayerInfo player = GetPlayerInfo(playersInDeck.PlayerId - 1, host.Roster);
            if (isBanPhase)
            {
                AddToTeamBanSelection(player, playersInDeck.Intention);
                // Same as a manual ban
                playersInDeck.Intention = CharacterType.None;
            }
            else if (isPickPhase)
            {
                AddToTeamSelection(player, playersInDeck.Intention);
            }

            // Same as a manual lock in
            playersInDeck.OnDeckness = RankedResolutionPlayerState.ReadyState.Unselected;
            RankedResolutionPhaseData.PlayersOnDeck[deckIndex] = playersInDeck;
        }

        // Like after a manual lock in, show the selection before moving on.
        // Otherwise, clients never see it if it is the last sub phase.
        if (autoSelectedDeckIndexes.Count > 0)
        {
            SendRankedResolutionSubPhase();
        }

        // Update any players who have a character selected, but that character is already selected or banned, reset their selection
        usedCharacterTypes = GetUsedCharacterTypes();
        for (int i = 0; i < RankedResolutionPhaseData.UnselectedPlayerStates.Count; i++)
        {
            RankedResolutionPlayerState player = RankedResolutionPhaseData.UnselectedPlayerStates[i];
            if (usedCharacterTypes.Contains(player.Intention) && player.OnDeckness == RankedResolutionPlayerState.ReadyState.Unselected)
            {
                player.Intention = CharacterType.None;
                RankedResolutionPhaseData.UnselectedPlayerStates[i] = player;
            }
        }

        return null;
    }

    // Teams NEED to be min 8 and max 8 only
    private static readonly List<KeyValuePair<int, FreelancerResolutionPhaseSubType>> DraftPhases = new()
    {
        // Playerid (-1)                           Phase
        new(0, FreelancerResolutionPhaseSubType.PICK_BANS1),    // TeamA First Person
        new(4, FreelancerResolutionPhaseSubType.PICK_BANS1),    // TeamB First Person
        new(0, FreelancerResolutionPhaseSubType.PICK_FREELANCER1),  // TeamA First Person
        new(4, FreelancerResolutionPhaseSubType.PICK_FREELANCER2),  // TeamB * 2 First and Second Person
        new(1, FreelancerResolutionPhaseSubType.PICK_FREELANCER1),  // TeamA Second Person
        new(5, FreelancerResolutionPhaseSubType.PICK_BANS2),    // TeamB Second Person
        new(1, FreelancerResolutionPhaseSubType.PICK_BANS2),    // TeamA Second Person
        new(6, FreelancerResolutionPhaseSubType.PICK_FREELANCER1),  // TeamB Third Person
        new(2, FreelancerResolutionPhaseSubType.PICK_FREELANCER2),  // TeamA * 2 Third and Fourth Person
        new(7, FreelancerResolutionPhaseSubType.PICK_FREELANCER1),  // TeamB Fourth Person
    };

    // -1 for phases where we don't need the player (trade)
    internal static List<KeyValuePair<int, FreelancerResolutionPhaseSubType>> GetDraftPhases(
        IReadOnlyList<LobbyServerPlayerInfo> teamPlayerInfo,
        bool hasBans,
        bool hasTrade)
    {
        List<KeyValuePair<int, FreelancerResolutionPhaseSubType>> phases = new();
        foreach (KeyValuePair<int, FreelancerResolutionPhaseSubType> phase in DraftPhases)
        {
            // Bans can be disabled. If they are disabled, client won't show ban windows
            // (Due to poll of favoring bans add a setting to Custom to disable bans only there)
            if (!hasBans
                && phase.Value is FreelancerResolutionPhaseSubType.PICK_BANS1 or FreelancerResolutionPhaseSubType.PICK_BANS2)
            {
                continue;
            }

            // An account controlling several characters (proxies) acts on one slot at a time (see GetDraftActingSlot),
            // so it picks for them one by one
            if (phase.Value == FreelancerResolutionPhaseSubType.PICK_FREELANCER2
                && IsSameController(
                    GetPlayerInfo(phase.Key, teamPlayerInfo),
                    GetPlayerInfo(phase.Key + 1, teamPlayerInfo)))
            {
                phases.Add(new(phase.Key, FreelancerResolutionPhaseSubType.PICK_FREELANCER1));
                phases.Add(new(phase.Key + 1, FreelancerResolutionPhaseSubType.PICK_FREELANCER1));
                continue;
            }

            phases.Add(phase);
        }

        if (hasTrade)
        {
            phases.Add(new(-1, FreelancerResolutionPhaseSubType.FREELANCER_TRADE));
        }

        return phases;
    }

    private static bool IsSameController(LobbyServerPlayerInfo player1, LobbyServerPlayerInfo player2)
    {
        return player1 is not null
               && player2 is not null
               && !player1.IsNPCBot
               && !player2.IsNPCBot
               && player1.AccountId == player2.AccountId;
    }

    private HashSet<CharacterType> GetUsedCharacterTypes()
    {
        // Build a list of all used character types for use with Fill
        return RankedResolutionPhaseData.FriendlyBans
            .Concat(RankedResolutionPhaseData.EnemyBans)
            .Concat(RankedResolutionPhaseData.FriendlyTeamSelections.Values)
            .Concat(RankedResolutionPhaseData.EnemyTeamSelections.Values)
            .ToHashSet();
    }

    // Like outside draft, a bot picks a role its team does not have yet.
    // Hovers of teammate bots on deck at the same time count too, so that they do not go for the same role.
    private CharacterType PickDraftCharacterForBot(LobbyServerPlayerInfo bot, HashSet<CharacterType> usedCharacterTypes)
    {
        Dictionary<int, CharacterType> teamSelections = bot.TeamId == Team.TeamA
            ? RankedResolutionPhaseData.FriendlyTeamSelections
            : RankedResolutionPhaseData.EnemyTeamSelections;
        HashSet<CharacterType> teammateCharacters = teamSelections.Values.ToHashSet();
        foreach (RankedResolutionPlayerState state in RankedResolutionPhaseData.PlayersOnDeck)
        {
            LobbyServerPlayerInfo deckMate = host.GetPlayerById(state.PlayerId);
            if (state.PlayerId != bot.PlayerId
                && state.Intention != CharacterType.None
                && deckMate is { IsAIControlled: true }
                && deckMate.TeamId == bot.TeamId)
            {
                teammateCharacters.Add(state.Intention);
            }
        }

        CharacterType characterType = Game.PickCharacter(
            teammateCharacters,
            usedCharacterTypes.Union(teammateCharacters).ToHashSet(),
            forBot: true);

        log.Info($"Selected character {characterType} for bot {bot.Handle}, " +
                 $"teammates: {string.Join(", ", teammateCharacters)}, " +
                 $"used characters: {string.Join(", ", usedCharacterTypes)}");

        return characterType;
    }

    private void UpdateTeamSelection(Dictionary<int, CharacterType> selections)
    {
        foreach (KeyValuePair<int, CharacterType> selection in selections)
        {
            LobbyServerPlayerInfo playerInfo = host.GetPlayerById(selection.Key);
            if (playerInfo.IsAIControlled)
            {
                // Update bots handle
                playerInfo.Handle = selection.Value.ToString();
            }
            playerInfo.CharacterInfo = host.GetCharacterInfo(playerInfo, selection.Value);
        }
    }
    
    // Returns false if the player is a bot that the draft does not know
    private bool AddPlayerToDeck(LobbyServerPlayerInfo player)
    {
        CharacterType characterType = CharacterType.None;

        HashSet<CharacterType> usedCharacterTypes = GetUsedCharacterTypes();

        // Let AI Randomize
        if (player.IsAIControlled)
        {
            characterType = PhaseSubType is FreelancerResolutionPhaseSubType.PICK_FREELANCER1
                or FreelancerResolutionPhaseSubType.PICK_FREELANCER2
                ? PickDraftCharacterForBot(player, usedCharacterTypes)
                : AssignRandomCharacterForDraft(player, usedCharacterTypes);
            if (PhaseSubType == FreelancerResolutionPhaseSubType.PICK_BANS1
                || PhaseSubType == FreelancerResolutionPhaseSubType.PICK_BANS2
                || PhaseSubType == FreelancerResolutionPhaseSubType.PICK_FREELANCER1
                || PhaseSubType == FreelancerResolutionPhaseSubType.PICK_FREELANCER2)
            {
                int index = RankedResolutionPhaseData.UnselectedPlayerStates.FindIndex(p => p.PlayerId == player.PlayerId);
                if (index < 0)
                {
                    log.Error($"Failed to find bot #{player.PlayerId}! Cancelling match");
                    return false;
                }

                RankedResolutionPlayerState state = RankedResolutionPhaseData.UnselectedPlayerStates[index];
                state.Intention = characterType;
                RankedResolutionPhaseData.UnselectedPlayerStates[index] = state;
                // TODO Update bot name when it picks a character to play (not to ban)
            }
        }
#if DEBUG
        log.Info($"Adding {player.PlayerId} {characterType} on deck ");
#endif
        // Get there intent they whant to play from UnselectedPlayerStates
        RankedResolutionPlayerState rankedResolutionPlayerState = RankedResolutionPhaseData.UnselectedPlayerStates.Find(p => p.PlayerId == player.PlayerId);

        // Check if this character is already used or banned if so set CharacterType.None
        // We "should" never get to this point as we reset UnselectedPlayerStates.Intend after a phase is finished and character is unavaible, but just in case.
        characterType = usedCharacterTypes.Contains(rankedResolutionPlayerState.Intention) ? CharacterType.None : rankedResolutionPlayerState.Intention;

        // Add player to the deck with the appropriate state
        RankedResolutionPhaseData.PlayersOnDeck.Add(new RankedResolutionPlayerState
        {
            PlayerId = player.PlayerId,
            Intention = characterType,
            OnDeckness = RankedResolutionPlayerState.ReadyState.Selected,
        });

        // Increment the count of players in the deck (To know when people locked in we can have 2 so we cant just skip a phase if one is locked in and the other is not)
        PlayersInDeck++;
        return true;
    }

    private static LobbyServerPlayerInfo GetPlayerInfo(int index, IReadOnlyList<LobbyServerPlayerInfo> teamPlayerInfo)
    {
        return index >= 0 && index < teamPlayerInfo.Count ? teamPlayerInfo[index] : null;
    }

    private void AddToTeamSelection(LobbyServerPlayerInfo player, CharacterType characterType)
    {
        Dictionary<int, CharacterType> selections = player.TeamId == Team.TeamA
            ? RankedResolutionPhaseData.FriendlyTeamSelections
            : RankedResolutionPhaseData.EnemyTeamSelections;

        selections.Add(player.PlayerId, characterType);
    }

    private void AddToTeamBanSelection(LobbyServerPlayerInfo player, CharacterType characterType)
    {
        List<CharacterType> selections = player.TeamId == Team.TeamA
            ? RankedResolutionPhaseData.FriendlyBans
            : RankedResolutionPhaseData.EnemyBans;

        selections.Add(characterType);
    }

    // Method to add players to the deck based on the phase type.
    // Returns false if a bot could not be put on deck, in which case the match has to be canceled.
    internal bool AddPlayersToDeckBasedOnPhase(FreelancerResolutionPhaseSubType phase, LobbyServerPlayerInfo player1, LobbyServerPlayerInfo player2)
    {
        switch (phase)
        {
            case FreelancerResolutionPhaseSubType.PICK_BANS1:
            case FreelancerResolutionPhaseSubType.PICK_BANS2:
            case FreelancerResolutionPhaseSubType.PICK_FREELANCER1:
            {
                return AddPlayerToDeck(player1);
            }
            case FreelancerResolutionPhaseSubType.PICK_FREELANCER2:
            {
                // player2 can be null if we are at the last Freelancer selection
                return AddPlayerToDeck(player1) && (player2 == null || AddPlayerToDeck(player2));
            }
        }

        return true;
    }

    private TimeSpan GetSubPhaseTimeout(FreelancerResolutionPhaseSubType subPhase) =>
        subPhase switch
        {
            FreelancerResolutionPhaseSubType.PICK_BANS1 => host.Timeouts.Ban1,
            FreelancerResolutionPhaseSubType.PICK_BANS2 => host.Timeouts.Ban2,
            FreelancerResolutionPhaseSubType.PICK_FREELANCER1 => host.Timeouts.Pick,
            FreelancerResolutionPhaseSubType.PICK_FREELANCER2 => host.Timeouts.Pick,
            FreelancerResolutionPhaseSubType.FREELANCER_TRADE => host.Timeouts.Trade,
            _ => throw new ArgumentOutOfRangeException(nameof(subPhase), subPhase, "Invalid sub phase type")
        };

    private async Task MonitorSubPhaseAsync(TimeSpan timeLeftInSubPhase, LobbyServerPlayerInfo player)
    {
        TimeSpan botCheckInterval = TimeSpan.FromSeconds(5);
        TimeSpan waitTime = !player.IsHumanControlled && botCheckInterval < timeLeftInSubPhase
            ? botCheckInterval
            : timeLeftInSubPhase;

        CancellationToken subPhaseEnd = _subPhaseEnd.Token;
        await Task.Delay(waitTime, _timeProvider, subPhaseEnd)
            .ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

#if DEBUG
        if (subPhaseEnd.IsCancellationRequested)
        {
            log.Info($"Cancellation requested. Exiting sub-phase monitoring for player {player.PlayerId}.");
        }
        else if (waitTime < timeLeftInSubPhase)
        {
            log.Info($"AI-controlled player {player.PlayerId} has been checked after {botCheckInterval.TotalSeconds} seconds. Exiting sub-phase early.");
        }
        else
        {
            log.Info($"Sub-phase monitoring complete for player {player.PlayerId} after {waitTime.TotalSeconds} seconds.");
        }
#endif
    }

    // When a player locks in, kill the timer
    private void SkipRankedResolutionSubPhase()
    {
        // Asynchronously, so that the draft does not move on to the next sub-phase on the thread of the request
        _ = _subPhaseEnd.CancelAsync();
    }

    internal void SetRankedResolutionPhaseData(RankedResolutionPhaseData newRankedResolutionPhaseData)
    {
        RankedResolutionPhaseData = newRankedResolutionPhaseData;
    }

    internal RankedResolutionPhaseData GetRankedResolutionPhaseData()
    {
        return RankedResolutionPhaseData;
    }

    private void UpdatePlayersInDeck()
    {
        PlayersInDeck--;
    }

    private void SendRankedResolutionSubPhase()
    {
        RankedResolutionPhaseData.TimeLeftInSubPhase = TimeLeftInSubPhase;
        host.SendDraftSnapshot(PhaseSubType, RankedResolutionPhaseData);
    }

    // Requests from clients. The caller checks that the game is drafting.
    // Each runs under the lock as a whole, so it happens either entirely before or entirely after a step
    // of the phase loop. Bans, lock-ins and trades are rejected once their sub-phase has ended.

    public DraftRequestResult Hover(long accountId, CharacterType selection)
    {
        lock (_lock)
        {
            RankedResolutionPhaseData rankedResolutionPhaseData = GetRankedResolutionPhaseData();
            LobbyServerPlayerInfo player = GetDraftActingSlot(accountId);
            if (player is null)
            {
                return DraftRequestResult.Failed();
            }

            List<RankedResolutionPlayerState> unselectedPlayerStates = rankedResolutionPhaseData.UnselectedPlayerStates;
            int stateIndex = unselectedPlayerStates.FindIndex(p => p.PlayerId == player.PlayerId);

            if (stateIndex >= 0)
            {
                RankedResolutionPlayerState existingUnselectedPlayerStates = unselectedPlayerStates[stateIndex];
                existingUnselectedPlayerStates.Intention = selection;
                unselectedPlayerStates[stateIndex] = existingUnselectedPlayerStates;

                List<RankedResolutionPlayerState> playersOnDeck = rankedResolutionPhaseData.PlayersOnDeck;
                int deckIndex = playersOnDeck.FindIndex(p => p.PlayerId == player.PlayerId);

                if (deckIndex >= 0)
                {
                    RankedResolutionPlayerState existingPlayersOnDeck = playersOnDeck[deckIndex];
                    existingPlayersOnDeck.Intention = selection;
                    playersOnDeck[deckIndex] = existingPlayersOnDeck;
                }

                SetRankedResolutionPhaseData(rankedResolutionPhaseData);
                SendRankedResolutionSubPhase();

                return DraftRequestResult.Succeeded;
            }
            else
            {
                // TODO: error message?
                return DraftRequestResult.Failed();
            }
        }
    }

    public DraftRequestResult Ban(long accountId, CharacterType selection)
    {
        lock (_lock)
        {
            if (!_subPhaseOpen
                || (PhaseSubType != FreelancerResolutionPhaseSubType.PICK_BANS1
                    && PhaseSubType != FreelancerResolutionPhaseSubType.PICK_BANS2))
            {
                return DraftRequestResult.Failed();
            }

            RankedResolutionPhaseData rankedResolutionPhaseData = GetRankedResolutionPhaseData();
            LobbyServerPlayerInfo player = GetDraftActingSlot(accountId);
            if (player is null)
            {
                return DraftRequestResult.Failed();
            }

            List<RankedResolutionPlayerState> playersOnDeck = rankedResolutionPhaseData.PlayersOnDeck;
            int deckIndex = playersOnDeck.FindIndex(p => p.PlayerId == player.PlayerId);

            HashSet<CharacterType> usedCharacterTypes = GetUsedCharacterTypes();
            CharacterType characterType = selection;
            if (deckIndex >= 0 && !usedCharacterTypes.Contains(characterType)) // TODO you can still lock in a duplicate by timing out
            {

                if (characterType == CharacterType.PendingWillFill
                    || characterType == CharacterType.TestFreelancer1
                    || characterType == CharacterType.TestFreelancer2)
                {
                    characterType = AssignRandomCharacterForDraft(player, usedCharacterTypes, characterType);
                }

                RankedResolutionPlayerState existingPlayersOnDeck = playersOnDeck[deckIndex];
                if (characterType != selection)
                {
                    // Show the random character as if it was hovered before locking it in,
                    // clients take the character for the lock in animation from the hover
                    existingPlayersOnDeck.Intention = characterType;
                    playersOnDeck[deckIndex] = existingPlayersOnDeck;
                    SetRankedResolutionPhaseData(rankedResolutionPhaseData);
                    SendRankedResolutionSubPhase();
                }

                if (player.TeamId == Team.TeamA)
                {
                    rankedResolutionPhaseData.FriendlyBans.Add(characterType);
                }
                else
                {
                    rankedResolutionPhaseData.EnemyBans.Add(characterType);
                }

                // A locked in ban is not a hover anymore, otherwise clients show it in the next ban slot
                existingPlayersOnDeck.Intention = CharacterType.None;
                existingPlayersOnDeck.OnDeckness = RankedResolutionPlayerState.ReadyState.Unselected;
                playersOnDeck[deckIndex] = existingPlayersOnDeck;

                SetRankedResolutionPhaseData(rankedResolutionPhaseData);
                SendRankedResolutionSubPhase();

                SkipRankedResolutionSubPhase();

                return DraftRequestResult.Succeeded;
            }
            else
            {
                // TODO: error message?
                return DraftRequestResult.Failed();
            }
        }
    }

    public DraftRequestResult Select(long accountId, CharacterType selection)
    {
        lock (_lock)
        {
            if (!_subPhaseOpen
                || (PhaseSubType != FreelancerResolutionPhaseSubType.PICK_FREELANCER1
                    && PhaseSubType != FreelancerResolutionPhaseSubType.PICK_FREELANCER2))
            {
                log.Warn($"Player {GetHandle(accountId)} attempted to lock in draft selection in incorrect phase {PhaseSubType}"
                         + (_subPhaseOpen ? "" : " (sub-phase ended)"));
                return DraftRequestResult.Failed(LocalizationPayload.Create("CannotReady@Global"));
            }

            RankedResolutionPhaseData rankedResolutionPhaseData = GetRankedResolutionPhaseData();
            LobbyServerPlayerInfo player = GetDraftActingSlot(accountId);
            if (player is null)
            {
                return DraftRequestResult.Failed();
            }

            Dictionary<int, CharacterType> teamSelections = player.TeamId == Team.TeamA
                ? rankedResolutionPhaseData.FriendlyTeamSelections
                : rankedResolutionPhaseData.EnemyTeamSelections;

            CharacterType characterType = selection;

            if (teamSelections.ContainsKey(player.PlayerId))
            {
                log.Warn($"Player {player.PlayerId} {GetHandle(accountId)} attempted to lock in draft selection twice");
                return DraftRequestResult.Failed(LocalizationPayload.Create("CannotChangeCharactersOnceReadied@MonitorServer"));
            }

            HashSet<CharacterType> usedCharacterTypes = GetUsedCharacterTypes();
            if (characterType == CharacterType.PendingWillFill
                || characterType == CharacterType.TestFreelancer1
                || characterType == CharacterType.TestFreelancer2)
            {
                characterType = AssignRandomCharacterForDraft(player, usedCharacterTypes, characterType);
            }

            List<RankedResolutionPlayerState> unselectedPlayerStates = rankedResolutionPhaseData.UnselectedPlayerStates;
            int stateIndex = unselectedPlayerStates.FindIndex(p => p.PlayerId == player.PlayerId);

            if (stateIndex >= 0)
            {
                RankedResolutionPlayerState existingUnselectedPlayerStates = unselectedPlayerStates[stateIndex];
                existingUnselectedPlayerStates.Intention = characterType;
                existingUnselectedPlayerStates.OnDeckness = RankedResolutionPlayerState.ReadyState.Selected;
                unselectedPlayerStates[stateIndex] = existingUnselectedPlayerStates;
            }

            List<RankedResolutionPlayerState> playersOnDeck = rankedResolutionPhaseData.PlayersOnDeck;
            int deckIndex = playersOnDeck.FindIndex(p => p.PlayerId == player.PlayerId);

            if (deckIndex >= 0 && !usedCharacterTypes.Contains(characterType))
            {
                RankedResolutionPlayerState existingPlayersOnDeck = playersOnDeck[deckIndex];
                if (characterType != selection)
                {
                    // Show the random character as if it was hovered before locking it in,
                    // clients take the character for the lock in animation from the hover
                    existingPlayersOnDeck.Intention = characterType;
                    playersOnDeck[deckIndex] = existingPlayersOnDeck;
                    SetRankedResolutionPhaseData(rankedResolutionPhaseData);
                    SendRankedResolutionSubPhase();
                }

                existingPlayersOnDeck.Intention = characterType;
                existingPlayersOnDeck.OnDeckness = RankedResolutionPlayerState.ReadyState.Unselected;
                playersOnDeck[deckIndex] = existingPlayersOnDeck;

                teamSelections.Add(player.PlayerId, characterType);

                UpdatePlayersInDeck();

                SetRankedResolutionPhaseData(rankedResolutionPhaseData);
                SendRankedResolutionSubPhase();

                if (PlayersInDeck == 0)
                {
                    SkipRankedResolutionSubPhase();
                }

                return DraftRequestResult.Succeeded;
            }
            else
            {
                return DraftRequestResult.Failed(LocalizationPayload.Create(
                    "CharacterTypeNotAllowed",
                    "Global",
                    LocalizationArg_Freelancer.Create(characterType)));
            }
        }
    }

    public DraftRequestResult Trade(long accountId, RankedTradeData trade)
    {
        lock (_lock)
        {
            if (!_subPhaseOpen || PhaseSubType != FreelancerResolutionPhaseSubType.FREELANCER_TRADE)
            {
                // TODO: error message?
                return DraftRequestResult.Failed();
            }

            RankedResolutionPhaseData rankedResolutionPhaseData = GetRankedResolutionPhaseData();
            LobbyServerPlayerInfo player = GetDraftTradingSlot(accountId, trade.OfferingPlayerId);
            if (player is null)
            {
                log.Warn($"Player {GetHandle(accountId)} attempted to trade from slot {trade.OfferingPlayerId} they do not control");
                return DraftRequestResult.Failed();
            }

            Dictionary<int, CharacterType> teamSelections = player.TeamId == Team.TeamA
                ? rankedResolutionPhaseData.FriendlyTeamSelections
                : rankedResolutionPhaseData.EnemyTeamSelections;

            int tradeActionId = rankedResolutionPhaseData.TradeActions.FindIndex(
                p => p.OfferedCharacter == trade.DesiredCharacter
                     && p.AskedPlayerId == player.PlayerId);
            switch (trade.TradeAction)
            {
                case RankedTradeData.TradeActionType.Reject:
                {
                    if (tradeActionId >= 0)
                    {
                        rankedResolutionPhaseData.TradeActions.RemoveAt(tradeActionId);
                    }
                    else
                    {
                        // TODO: error message?
                        return DraftRequestResult.Failed();
                    }

                    break;
                }
                case RankedTradeData.TradeActionType.AcceptOrOffer:
                {
                    if (tradeActionId >= 0)
                    {
                        var existingTradeAction = rankedResolutionPhaseData.TradeActions[tradeActionId];
                        // Update the existing trade action to accepted
                        teamSelections[existingTradeAction.OfferingPlayerId] = existingTradeAction.DesiredCharacter;
                        teamSelections[existingTradeAction.AskedPlayerId] = existingTradeAction.OfferedCharacter;
                        rankedResolutionPhaseData.TradeActions.RemoveAll(p =>
                            p.OfferingPlayerId == player.PlayerId
                            || p.AskedPlayerId == player.PlayerId
                            || p.OfferingPlayerId == existingTradeAction.OfferingPlayerId
                            || p.AskedPlayerId == existingTradeAction.OfferingPlayerId);
                    }
                    else
                    {
                        // Handle the case where the trade request does not exist
                        CharacterType currentCharacterType = teamSelections[player.PlayerId];
                        CharacterType wantedCharacterType = trade.DesiredCharacter;
                        int playerThatHasCharacter = teamSelections
                            .Where(item => item.Value == wantedCharacterType)
                            .Select(item => item.Key)
                            .FirstOrDefault();

                        LobbyServerPlayerInfo checkIsBot = host.GetPlayerById(playerThatHasCharacter);

                        // Trading between slots controlled by the same account is not supported
                        if (checkIsBot is null || checkIsBot.AccountId == accountId)
                        {
                            log.Warn($"Player {GetHandle(accountId)} attempted to trade {player.PlayerId} for {wantedCharacterType} "
                                     + $"(slot {playerThatHasCharacter})");
                            return DraftRequestResult.Failed();
                        }

                        if (checkIsBot.IsAIControlled)
                        {
                            //automatic accept trade
                            teamSelections[playerThatHasCharacter] = currentCharacterType;
                            teamSelections[player.PlayerId] = wantedCharacterType;
                        }
                        else
                        {
                            RankedTradeData newTradeData = new RankedTradeData()
                            {
                                AskedPlayerId = playerThatHasCharacter,
                                TradeAction = RankedTradeData.TradeActionType.AcceptOrOffer,
                                DesiredCharacter = teamSelections[playerThatHasCharacter],
                                OfferedCharacter = currentCharacterType,
                                OfferingPlayerId = player.PlayerId
                            };
                            rankedResolutionPhaseData.TradeActions.Add(newTradeData);
                        }
                    }

                    break;
                }
            }

            SetRankedResolutionPhaseData(rankedResolutionPhaseData);
            SendRankedResolutionSubPhase();

            return DraftRequestResult.Succeeded;
        }
    }

    // For logging
    private string GetHandle(long accountId)
    {
        return host.GetMainSlot(accountId)?.Handle ?? $"#{accountId}";
    }

    // During draft, an account may control multiple slots (asymmetric / ControlAllBots).
    // Resolve the slot this account is currently expected to act on (the one on deck),
    // falling back to the account's main slot for normal (single-character) drafts.
    // Under the lock.
    internal LobbyServerPlayerInfo GetDraftActingSlot(long accountId)
    {
        foreach (RankedResolutionPlayerState state in RankedResolutionPhaseData.PlayersOnDeck)
        {
            if (state.OnDeckness != RankedResolutionPlayerState.ReadyState.Selected)
            {
                continue;
            }
            LobbyServerPlayerInfo slot = host.GetPlayerById(state.PlayerId);
            if (slot != null && !slot.IsAIControlled && slot.AccountId == accountId)
            {
                return slot;
            }
        }
        return host.GetMainSlot(accountId);
    }

    // During the trade sub-phase, an account controlling multiple slots trades from the one it specifies.
    // 0 means the account's main slot (that is what clients controlling a single character send).
    internal LobbyServerPlayerInfo GetDraftTradingSlot(long accountId, int playerId)
    {
        LobbyServerPlayerInfo slot = playerId == 0 ? host.GetMainSlot(accountId) : host.GetPlayerById(playerId);
        if (slot is null
            || slot.AccountId != accountId
            || slot.TeamId is not (Team.TeamA or Team.TeamB))
        {
            return null;
        }

        return slot;
    }

    private CharacterType AssignRandomCharacterForDraft(
        LobbyServerPlayerInfo playerInfo,
        HashSet<CharacterType> unavailableCharacters,
        CharacterType preferedCat = CharacterType.Gryd)
    {
        CharacterRole characterRole = CharacterRole.None;

        if (preferedCat != CharacterType.Gryd)
        {
            switch (preferedCat)
            {
                case CharacterType.PendingWillFill:
                    characterRole = CharacterRole.Assassin;
                    break;
                case CharacterType.TestFreelancer1:
                    characterRole = CharacterRole.Tank;
                    break;
                case CharacterType.TestFreelancer2:
                    characterRole = CharacterRole.Support;
                    break;
            }
        }

        List<CharacterType> availableTypes;

        if (preferedCat != CharacterType.Gryd)
        {
            // Select only characters that match the specified characterRole (from preferedCat)
            availableTypes = CharacterConfigs.Characters
                .Where(cc =>
                    cc.Value.AllowForPlayers
                    && cc.Value.CharacterRole == characterRole
                    && !unavailableCharacters.Contains(cc.Key))
                .Select(cc => cc.Key)
                .ToList();
        }
        else
        {
            // Select all available characters (ignoring the preferedCat)
            availableTypes = CharacterConfigs.Characters
                .Where(cc =>
                    cc.Value.AllowForPlayers
                    && cc.Value.CharacterRole != CharacterRole.None
                    && !unavailableCharacters.Contains(cc.Key))
                .Select(cc => cc.Key)
                .ToList();
        }

        // Shuffle the availableTypes list
        Random rand = new Random();
        CharacterType randomType = availableTypes[rand.Next(availableTypes.Count)];

        log.Info($"Selected random character {randomType} for {playerInfo.Handle} " +
                 $"(was {playerInfo.CharacterType}), options were {string.Join(", ", availableTypes)}, " +
                 $"used characters: {string.Join(", ", unavailableCharacters)})");

        return randomType;
    }
}

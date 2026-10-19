using CentralServer.LobbyServer.Gamemode;

namespace CentralServer.LobbyServer.Character;

public static class CharacterRules
{
    /// <summary>
    /// Returns the reason a player cannot use the character in the game type, or null if they can.
    /// </summary>
    public static LocalizationPayload CheckCharacter(CharacterType characterType, GameType gameType)
    {
        if (characterType == CharacterType.PendingWillFill)
        {
            return GameModeManager.GetGameTypeAvailability(gameType)?.MaxWillFillPerTeam > 0
                ? null
                : LocalizationPayload.Create("QueueDoesNotAllowWillFill", "Matchmaking", LocalizationArg_GameType.Create(gameType));
        }

        if (!CharacterConfigs.Characters.TryGetValue(characterType, out CharacterConfig config)
            || !config.AllowForPlayers
            || config.GameTypesProhibitedFrom?.Contains(gameType) == true
            // TestFreelancer1/2 only stand for "random tank/support" in ranked draft
            || config.CharacterRole == CharacterRole.None)
        {
            return LocalizationPayload.Create("CannotReady@Global");
        }

        return null;
    }
}

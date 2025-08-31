using System;
using System.Globalization;
using EvoS.Framework.Network.Static;

namespace EvoS.Framework.Misc;

public static class GameUtils
{
    public static string GameIdString(LobbyGameInfo gameInfo)
    {
        return gameInfo != null
            ? $"{new DateTime(gameInfo.CreateTimestamp):yyyy_MM_dd__HH_mm_ss}"
            : "N/A";
    }

    public static DateTime? ParseGameId(string gameId)
    {
        bool parsed = DateTime.TryParseExact(
            gameId,
            "yyyy_MM_dd__HH_mm_ss",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var dateTime);
        return parsed ? dateTime : null;
    }
}
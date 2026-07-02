using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using CentralServer.LobbyServer.Group;
using CentralServer.LobbyServer.Session;
using CentralServer.LobbyServer.Utils;
using EvoS.Framework.Network.NetworkMessages;
using log4net;

namespace CentralServer.LobbyServer.Chat;

public class MapPickBanSession
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(MapPickBanSession));
    private const string BotHandle = "Maps";

    public record MapOption(string Id, string DisplayName);

    private enum StepAction { Ban, Pick }
    private record Step(int CaptainIndex, StepAction Action);
    private record ParseResult(bool IsValid, MapOption Map = null, string ErrorMessage = null);

    private readonly List<MapOption> _remaining;
    private readonly List<(MapOption Map, int CaptainIndex)> _picks;
    private readonly int _pickCount;
    private readonly List<Step> _steps;
    private readonly long[] _captains;
    private readonly Action<List<MapOption>> _callback;
    private readonly Lock _lock = new();
    private int _currentStep;
    private bool _finished;

    public static MapPickBanSession Start(
        List<MapOption> mapPool,
        int pickCount,
        long captainAAccountId,
        long captainBAccountId,
        Action<List<MapOption>> callback)
    {
        if (mapPool == null || mapPool.Count != 7)
            throw new ArgumentException("Map pool must contain exactly 7 maps.", nameof(mapPool));
        if (mapPool.Select(m => m.Id).Distinct().Count() != 7)
            throw new ArgumentException("Map pool must not contain duplicate map IDs.", nameof(mapPool));
        if (pickCount != 1 && pickCount != 3 && pickCount != 5)
            throw new ArgumentException("pickCount must be 1, 3, or 5.", nameof(pickCount));
        if (captainAAccountId == captainBAccountId)
            throw new ArgumentException("Captains must be different accounts.");
        if (callback == null)
            throw new ArgumentNullException(nameof(callback));

        var session = new MapPickBanSession(mapPool, pickCount, captainAAccountId, captainBAccountId, callback);
        session.Begin();
        return session;
    }

    private MapPickBanSession(
        List<MapOption> mapPool,
        int pickCount,
        long captainAAccountId,
        long captainBAccountId,
        Action<List<MapOption>> callback)
    {
        _remaining = new List<MapOption>(mapPool);
        _picks = [];
        _steps = BuildSteps(pickCount);
        _pickCount = pickCount;
        _captains = [captainAAccountId, captainBAccountId];
        _callback = callback;
    }

    private static List<Step> BuildSteps(int pickCount) => pickCount switch
    {
        1 => [Ban(0), Ban(1), Ban(0), Ban(1), Ban(0), Ban(1)],
        3 => [Ban(0), Ban(1), Pick(0), Pick(1), Ban(0), Ban(1)],
        5 => [Ban(0), Ban(1), Pick(0), Pick(1), Pick(0), Pick(1)],
        _ => throw new ArgumentOutOfRangeException(nameof(pickCount))
    };

    private static Step Ban(int captainIndex) => new Step(captainIndex, StepAction.Ban);
    private static Step Pick(int captainIndex) => new Step(captainIndex, StepAction.Pick);

    private void Begin()
    {
        ChatManager.Get().RegisterWhisperHandler(_captains[0], BotHandle, OnResponse);
        ChatManager.Get().RegisterWhisperHandler(_captains[1], BotHandle, OnResponse);
        Log.Info("Map pick/ban session started: "
                 + $"captainA={LobbyServerUtils.GetHandle(_captains[0])} "
                 + $"captainB={LobbyServerUtils.GetHandle(_captains[1])} "
                 + $"steps={_steps.Count}");
        string count = _pickCount == 1 ? "1 map" : $"{_pickCount} maps";
        BroadcastToGroupMembers(
            $"{LobbyServerUtils.GetHandle(_captains[0])} and {LobbyServerUtils.GetHandle(_captains[1])} "
            + $"are picking {count} for this match",
            true);
        SendPromptToCurrentCaptain();
    }

    private void OnResponse(ChatNotification notification)
    {
        List<MapOption> resultMaps = null;

        lock (_lock)
        {
            if (_finished) return;

            Step step = _steps[_currentStep];
            long expectedCaptain = _captains[step.CaptainIndex];

            if (notification.SenderAccountId != expectedCaptain) return;

            ParseResult parsed = TryParseInput(notification.Text, _remaining);
            if (!parsed.IsValid)
            {
                string errorMsg = $"Invalid choice. {parsed.ErrorMessage}\n\n{BuildPromptText(step)}";
                ChatManager.Get().SendSystemWhisper(BotHandle, expectedCaptain, errorMsg);
                Log.Warn($"Captain {LobbyServerUtils.GetHandle(expectedCaptain)} made invalid choice: '{notification.Text.Trim()}'");
                return;
            }

            MapOption chosen = parsed.Map;
            _remaining.Remove(chosen);

            string action = step.Action == StepAction.Ban ? "bans" : "picks";
            string captainHandle = LobbyServerUtils.GetHandle(expectedCaptain);
            Log.Info($"Captain {captainHandle} ({(step.CaptainIndex == 0 ? "A" : "B")}) {action} {chosen.DisplayName}");
            BroadcastToGroupMembers($"{captainHandle} {action} {chosen.DisplayName}", true);

            if (step.Action == StepAction.Pick)
                _picks.Add((chosen, step.CaptainIndex));

            _currentStep++;

            if (_currentStep == _steps.Count)
            {
                resultMaps = BuildResultList();
                BroadcastToGroupMembers(BuildResultAnnouncement(), true);
                UnregisterHandlers();
                _finished = true;
            }
            else
            {
                SendPromptToCurrentCaptain();
            }
        }

        if (resultMaps != null)
        {
            Log.Info("Map pick/ban complete. "
                     + $"captainA={LobbyServerUtils.GetHandle(_captains[0])} "
                     + $"captainB={LobbyServerUtils.GetHandle(_captains[1])} "
                     + $"result={string.Join(", ", resultMaps.Select(m => m.DisplayName))}");
            _callback(resultMaps);
        }
    }

    private void SendPromptToCurrentCaptain()
    {
        Step step = _steps[_currentStep];
        ChatManager.Get().SendSystemWhisper(BotHandle, _captains[step.CaptainIndex], BuildPromptText(step));
    }

    private string BuildPromptText(Step step)
    {
        string verb = step.Action == StepAction.Ban ? "Ban" : "Pick";
        var sb = new StringBuilder();
        sb.AppendLine($"{verb} a map:");
        for (int i = 0; i < _remaining.Count; i++)
            sb.AppendLine($"  {i + 1}. {_remaining[i].DisplayName}");
        sb.Append("(Reply with a number or map name)");
        return sb.ToString();
    }

    private string BuildResultAnnouncement()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Map pick/ban complete! Result:");
        int index = 1;
        foreach ((MapOption map, int captainIndex) in _picks)
        {
            string label = captainIndex == 0 ? "A pick" : "B pick";
            sb.AppendLine($"  {index++}. {map.DisplayName} ({label})");
        }
        foreach (MapOption map in _remaining)
        {
            sb.AppendLine($"  {index++}. {map.DisplayName} (final map)");
        }
        return sb.ToString().TrimEnd();
    }

    private List<MapOption> BuildResultList()
    {
        var result = _picks.Select(p => p.Map).ToList();
        result.AddRange(_remaining);
        return result;
    }

    private void BroadcastToGroupMembers(string text, bool includeCaptains = false)
    {
        HashSet<long> sent = [];
        foreach (long captainId in _captains)
        {
            GroupInfo group = GroupManager.GetPlayerGroup(captainId);
            if (group == null) continue;
            foreach (long memberId in group.Members)
            {
                if ((memberId != captainId || includeCaptains) && sent.Add(memberId))
                {
                    SessionManager.GetClientConnection(memberId)?.SendSystemMessage(text);
                }
            }
        }
    }

    private void UnregisterHandlers()
    {
        ChatManager.Get().UnregisterWhisperHandler(_captains[0], BotHandle);
        ChatManager.Get().UnregisterWhisperHandler(_captains[1], BotHandle);
    }

    private static ParseResult TryParseInput(string text, List<MapOption> remaining)
    {
        text = text.Trim();

        if (int.TryParse(text, out int n))
        {
            if (n >= 1 && n <= remaining.Count)
                return new ParseResult(true, remaining[n - 1]);
            return new ParseResult(false, ErrorMessage: $"Number must be between 1 and {remaining.Count}.");
        }

        MapOption match = remaining.FirstOrDefault(m =>
            string.Equals(m.DisplayName, text, StringComparison.OrdinalIgnoreCase))
            ?? remaining.FirstOrDefault(m =>
            string.Equals(m.Id, text, StringComparison.OrdinalIgnoreCase));

        if (match != null)
            return new ParseResult(true, match);

        return new ParseResult(false, ErrorMessage: "No map found. Use a number or the exact map name.");
    }
}

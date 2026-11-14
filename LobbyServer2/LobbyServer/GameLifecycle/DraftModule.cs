using CentralServer.BridgeServer;
using CentralServer.LobbyServer.Session;
using LobbyGameClientMessages;
using log4net;

namespace CentralServer.LobbyServer.GameLifecycle;

// Draft (ranked freelancer selection) requests. The logic is in DraftController.
public class DraftModule(IClientConnection conn) : ILobbyModule
{
    private static readonly ILog log = LogManager.GetLogger(typeof(DraftModule));

    public void Register(IHandlerRegistry registry)
    {
        registry.Register<RankedHoverClickRequest>(HandlePlayerRankedHoverClickRequest);
        registry.Register<RankedBanRequest>(HandlePlayerRankedBanRequest);
        registry.Register<RankedSelectionRequest>(HandleRankedSelectionRequest);
        registry.Register<RankedTradeRequest>(HandleRankedTradeRequest);
    }

    // Null unless the player's game is drafting
    private DraftController GetDraft()
    {
        Game game = conn.CurrentGame;
        return game is { IsDrafting: true } ? game.Draft : null;
    }

    private void HandlePlayerRankedHoverClickRequest(RankedHoverClickRequest request)
    {
        // TODO also check ready state - do not update if already ready
        DraftRequestResult result = GetDraft()?.Hover(conn.AccountId, request.Selection) ?? default;
        conn.Send(new RankedHoverClickResponse
        {
            ResponseId = request.RequestId,
            Success = result.Success,
            LocalizedFailure = result.Failure,
        });
    }

    private void HandlePlayerRankedBanRequest(RankedBanRequest request)
    {
        // TODO also check ready state - do not update if already ready
        DraftRequestResult result = GetDraft()?.Ban(conn.AccountId, request.Selection) ?? default;
        conn.Send(new RankedBanResponse
        {
            ResponseId = request.RequestId,
            Success = result.Success,
            LocalizedFailure = result.Failure,
        });
    }

    private void HandleRankedSelectionRequest(RankedSelectionRequest request)
    {
        // TODO also check ready state - do not update if already ready
        DraftController draft = GetDraft();
        if (draft is null)
        {
            Game game = conn.CurrentGame;
            log.Warn($"Player {conn.Handle} attempted to lock in draft selection in incorrect state "
                     + $"(game = {game}, is drafting = {game?.IsDrafting}, phase = {game?.PhaseSubType}");
            conn.Send(new RankedSelectionResponse
            {
                ResponseId = request.RequestId,
                Success = false,
                LocalizedFailure = LocalizationPayload.Create("CannotReady@Global"),
            });
            return;
        }

        DraftRequestResult result = draft.Select(conn.AccountId, request.Selection);
        conn.Send(new RankedSelectionResponse
        {
            ResponseId = request.RequestId,
            Success = result.Success,
            LocalizedFailure = result.Failure,
        });
    }

    private void HandleRankedTradeRequest(RankedTradeRequest request)
    {
        DraftRequestResult result = GetDraft()?.Trade(conn.AccountId, request.Trade) ?? default;
        conn.Send(new RankedTradeResponse
        {
            ResponseId = request.RequestId,
            Success = result.Success,
            LocalizedFailure = result.Failure,
        });
    }
}

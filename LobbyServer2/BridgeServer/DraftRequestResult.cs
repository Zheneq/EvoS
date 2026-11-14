namespace CentralServer.BridgeServer;

// Outcome of a client's draft request (hover, ban, select, trade).
// WrongPhase: the request was rejected because the draft is in another sub-phase.
public readonly record struct DraftRequestResult(bool Success, LocalizationPayload Failure = null, bool WrongPhase = false)
{
    public static DraftRequestResult Succeeded => new(true);

    public static DraftRequestResult Failed(LocalizationPayload failure = null) => new(false, failure);

    public static DraftRequestResult InWrongPhase(LocalizationPayload failure = null) => new(false, failure, WrongPhase: true);
}

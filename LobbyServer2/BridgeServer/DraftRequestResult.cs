namespace CentralServer.BridgeServer;

// Outcome of a client's draft request (hover, ban, select, trade)
// TODO Is it needed? Or we can go with just LocalizationPayload Failure, Failure == null -> success?
public readonly record struct DraftRequestResult(bool Success, LocalizationPayload Failure = null)
{
    public static DraftRequestResult Succeeded => new(true);

    public static DraftRequestResult Failed(LocalizationPayload failure = null) => new(false, failure);
}

namespace CentralServer.BridgeServer
{
    /// <summary>
    /// The slice of a game-server connection that <see cref="GameServerKeyManager"/> needs while a
    /// server is held pending admin approval. Implemented by <see cref="BridgeServerProtocol"/>;
    /// abstracted so the approval flow can be unit-tested with a fake connection.
    /// </summary>
    public interface IGameServerConnection
    {
        bool IsConnected { get; }
        void CompleteRegistration();
        void RejectRegistration();
    }
}

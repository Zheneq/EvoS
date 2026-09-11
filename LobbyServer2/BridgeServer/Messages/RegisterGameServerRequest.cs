using EvoS.Framework.Network.Static;
using EvoS.Framework.Network.Unity;

// added in rogues
public class RegisterGameServerRequest : AllianceMessageBase
{
    public LobbySessionInfo SessionInfo;
    public bool isPrivate;
    // RSA public key (RSA.ToXmlString(false)) identifying this game server.
    public string PublicKey;
    // Base64 signature over the lobby-issued challenge nonce (RSA SHA-256, PKCS#1).
    public string Signature;

    public override void Serialize(NetworkWriter writer)
    {
        base.Serialize(writer);
        SerializeObject(SessionInfo, writer);
        writer.Write(isPrivate);
        writer.Write(PublicKey ?? "");
        writer.Write(Signature ?? "");
    }

    // custom
    public override void Deserialize(NetworkReader reader)
    {
        base.Deserialize(reader);
        DeserializeObject(out SessionInfo, reader);
        isPrivate = reader.ReadBoolean();
        PublicKey = reader.ReadString();
        Signature = reader.ReadString();
    }
}

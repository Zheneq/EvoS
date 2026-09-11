using System;
using EvoS.Framework.Network.Unity;

// Sent by the lobby to a newly connected game server. The server must sign the (decoded) nonce with
// its RSA private key (SHA-256, PKCS#1) and return the base64 signature + public key in
// RegisterGameServerRequest. Nonce is transmitted base64-encoded (string wire format is known to be
// compatible between the lobby and the Unity/UNET game server; raw byte-array framing is not).
[Serializable]
public class ServerAuthChallengeNotification : AllianceMessageBase
{
    public string Nonce;

    public override void Serialize(NetworkWriter writer)
    {
        base.Serialize(writer);
        writer.Write(Nonce ?? "");
    }

    public override void Deserialize(NetworkReader reader)
    {
        base.Deserialize(reader);
        Nonce = reader.ReadString();
    }
}

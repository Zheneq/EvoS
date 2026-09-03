using System.Security.Cryptography;
using EvoS.Framework.Auth;
using Xunit;

namespace Tests;

public class GameServerAuthTest
{
    private static (string publicKey, RSA rsa) NewKey()
    {
        RSA rsa = RSA.Create(2048);
        return (rsa.ToXmlString(false), rsa);
    }

    private static byte[] Sign(RSA rsa, byte[] data)
    {
        return rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }

    [Fact]
    public void ValidSignaturePasses()
    {
        (string publicKey, RSA rsa) = NewKey();
        byte[] nonce = GameServerAuth.GenerateNonce();
        byte[] signature = Sign(rsa, nonce);

        Assert.True(GameServerAuth.VerifySignature(publicKey, nonce, signature));
    }

    [Fact]
    public void TamperedNonceFails()
    {
        (string publicKey, RSA rsa) = NewKey();
        byte[] nonce = GameServerAuth.GenerateNonce();
        byte[] signature = Sign(rsa, nonce);
        nonce[0] ^= 0xFF;

        Assert.False(GameServerAuth.VerifySignature(publicKey, nonce, signature));
    }

    [Fact]
    public void TamperedSignatureFails()
    {
        (string publicKey, RSA rsa) = NewKey();
        byte[] nonce = GameServerAuth.GenerateNonce();
        byte[] signature = Sign(rsa, nonce);
        signature[0] ^= 0xFF;

        Assert.False(GameServerAuth.VerifySignature(publicKey, nonce, signature));
    }

    [Fact]
    public void WrongKeyFails()
    {
        (_, RSA signer) = NewKey();
        (string otherPublicKey, _) = NewKey();
        byte[] nonce = GameServerAuth.GenerateNonce();
        byte[] signature = Sign(signer, nonce);

        Assert.False(GameServerAuth.VerifySignature(otherPublicKey, nonce, signature));
    }

    [Fact]
    public void MalformedInputsFailGracefully()
    {
        (string publicKey, RSA rsa) = NewKey();
        byte[] nonce = GameServerAuth.GenerateNonce();
        byte[] signature = Sign(rsa, nonce);

        Assert.False(GameServerAuth.VerifySignature(null, nonce, signature));
        Assert.False(GameServerAuth.VerifySignature("not-xml", nonce, signature));
        Assert.False(GameServerAuth.VerifySignature(publicKey, nonce, null));
    }

    [Fact]
    public void FingerprintIsStableAndDistinct()
    {
        (string publicKey, RSA rsa) = NewKey();
        (string otherPublicKey, _) = NewKey();

        string fp1 = GameServerAuth.ComputeFingerprint(publicKey);
        string fp2 = GameServerAuth.ComputeFingerprint(publicKey);
        string other = GameServerAuth.ComputeFingerprint(otherPublicKey);

        Assert.NotNull(fp1);
        Assert.Equal(fp1, fp2);
        Assert.NotEqual(fp1, other);
        // Public-only and full (private) key exports of the same key share a fingerprint.
        Assert.Equal(fp1, GameServerAuth.ComputeFingerprint(rsa.ToXmlString(true)));
    }

    [Fact]
    public void FingerprintOfBadKeyIsNull()
    {
        Assert.Null(GameServerAuth.ComputeFingerprint("not-xml"));
    }
}

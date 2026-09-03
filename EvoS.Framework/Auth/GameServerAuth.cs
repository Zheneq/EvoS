using System;
using System.Security.Cryptography;

namespace EvoS.Framework.Auth;

/// <summary>
/// Asymmetric (RSA) challenge-response authentication for game servers connecting to /BridgeServer.
/// The game server holds an RSA private key and signs a lobby-issued nonce; the lobby verifies the
/// signature against the presented public key and identifies the server by the key fingerprint.
/// RSA + SHA-256 PKCS#1 is used because it is the only asymmetric scheme available on both the lobby
/// (.NET 9) and the game server (Unity Mono / .NET Framework 2.0).
/// </summary>
public static class GameServerAuth
{
    public const int NonceLength = 32;

    public static byte[] GenerateNonce()
    {
        return RandomNumberGenerator.GetBytes(NonceLength);
    }

    /// <summary>
    /// Canonical SHA-256 fingerprint of an RSA public key (as <c>RSA.ToXmlString(false)</c>).
    /// Computed lobby-side only; the game server never needs to compute it.
    /// Returns lowercase hex, or null if the key cannot be parsed.
    /// </summary>
    public static string ComputeFingerprint(string publicKeyXml)
    {
        try
        {
            using RSA rsa = RSA.Create();
            rsa.FromXmlString(publicKeyXml);
            RSAParameters p = rsa.ExportParameters(false);
            byte[] material = new byte[p.Modulus.Length + p.Exponent.Length];
            Buffer.BlockCopy(p.Modulus, 0, material, 0, p.Modulus.Length);
            Buffer.BlockCopy(p.Exponent, 0, material, p.Modulus.Length, p.Exponent.Length);
            return Convert.ToHexString(SHA256.HashData(material)).ToLowerInvariant();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Verifies that <paramref name="signature"/> is a valid RSA PKCS#1 SHA-256 signature over
    /// <paramref name="nonce"/> made with the private key matching <paramref name="publicKeyXml"/>.
    /// </summary>
    public static bool VerifySignature(string publicKeyXml, byte[] nonce, byte[] signature)
    {
        if (string.IsNullOrEmpty(publicKeyXml) || nonce == null || signature == null)
        {
            return false;
        }
        try
        {
            using RSA rsa = RSA.Create();
            rsa.FromXmlString(publicKeyXml);
            return rsa.VerifyData(nonce, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (Exception)
        {
            return false;
        }
    }
}

using System;
using System.Security.Cryptography;
using System.Text;

namespace SLSKDONET.Utils;

/// <summary>
/// Helper utilities for PKCE (Proof Key for Code Exchange) OAuth flow.
/// Implements RFC 7636 for secure authorization without client secrets.
/// </summary>
public static class PKCEHelper
{
    /// <summary>
    /// Generates a cryptographically random code verifier.
    /// Must be 43-128 characters long, base64url encoded.
    /// </summary>
    /// <returns>Base64url-encoded code verifier (128 characters)</returns>
    public static string GenerateCodeVerifier()
    {
        // Generate 96 random bytes (will be 128 chars when base64url encoded)
        var bytes = new byte[96];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }
        
        return Base64UrlEncode(bytes);
    }

    /// <summary>
    /// Generates a code challenge from a code verifier using SHA256.
    /// </summary>
    /// <param name="codeVerifier">The code verifier to hash</param>
    /// <returns>Base64url-encoded SHA256 hash of the verifier</returns>
    public static string GenerateCodeChallenge(string codeVerifier)
    {
        if (string.IsNullOrWhiteSpace(codeVerifier))
            throw new ArgumentException("Code verifier cannot be null or empty", nameof(codeVerifier));

        var bytes = Encoding.UTF8.GetBytes(codeVerifier);
        var hash = SHA256.HashData(bytes);
        
        return Base64UrlEncode(hash);
    }

    /// <summary>
    /// Encodes bytes to base64url format (RFC 4648 Section 5).
    /// Replaces '+' with '-', '/' with '_', and removes padding '='.
    /// </summary>
    private static string Base64UrlEncode(byte[] bytes)
    {
        var base64 = Convert.ToBase64String(bytes);
        
        // Convert to base64url format
        return base64
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>
    /// Validates that a code verifier meets PKCE requirements.
    /// </summary>
    /// <param name="codeVerifier">The code verifier to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    public static bool IsValidCodeVerifier(string codeVerifier)
    {
        if (string.IsNullOrWhiteSpace(codeVerifier))
            return false;

        // Must be 43-128 characters
        if (codeVerifier.Length < 43 || codeVerifier.Length > 128)
            return false;

        // Must contain only unreserved characters: [A-Z] / [a-z] / [0-9] / "-" / "." / "_" / "~"
        foreach (var c in codeVerifier)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '.' && c != '_' && c != '~')
                return false;
        }

        return true;
    }
}

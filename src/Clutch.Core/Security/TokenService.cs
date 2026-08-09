namespace Clutch.Core.Security
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Issues and validates opaque session tokens. A token is the AES-256-CBC ciphertext of a JSON
    /// payload, with a random IV per token prepended to the ciphertext, base64-encoded.
    /// </summary>
    public class TokenService
    {
        #region Private-Members

        private readonly byte[] _Key;
        private readonly string _Issuer;
        private readonly int _LifetimeMinutes;
        private static readonly JsonSerializerOptions _JsonOptions = BuildJsonOptions();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="signingKey">Signing key material. A 256-bit key is derived from its SHA-256 hash.</param>
        /// <param name="issuer">Issuer identifier embedded in tokens.</param>
        /// <param name="lifetimeMinutes">Default token lifetime in minutes.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public TokenService(string signingKey, string issuer, int lifetimeMinutes)
        {
            if (string.IsNullOrEmpty(signingKey)) throw new ArgumentNullException(nameof(signingKey));
            if (string.IsNullOrEmpty(issuer)) throw new ArgumentNullException(nameof(issuer));

            _Key = SHA256.HashData(Encoding.UTF8.GetBytes(signingKey));
            _Issuer = issuer;
            _LifetimeMinutes = lifetimeMinutes < 1 ? 1 : lifetimeMinutes;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// The default token lifetime in minutes.
        /// </summary>
        public int LifetimeMinutes
        {
            get
            {
                return _LifetimeMinutes;
            }
        }

        /// <summary>
        /// Issue a token for the given payload. The payload's issuer is set, and expiry is set if unset.
        /// </summary>
        /// <param name="payload">Token payload.</param>
        /// <returns>The opaque token string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when payload is null.</exception>
        public string Issue(TokenPayload payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            payload.Issuer = _Issuer;
            if (payload.IssuedUtc == default) payload.IssuedUtc = DateTime.UtcNow;

            string json = JsonSerializer.Serialize(payload, _JsonOptions);
            byte[] plaintext = Encoding.UTF8.GetBytes(json);

            using (Aes aes = Aes.Create())
            {
                aes.Key = _Key;
                aes.GenerateIV();
                byte[] iv = aes.IV;

                using (ICryptoTransform encryptor = aes.CreateEncryptor())
                using (MemoryStream output = new MemoryStream())
                {
                    output.Write(iv, 0, iv.Length);
                    using (CryptoStream crypto = new CryptoStream(output, encryptor, CryptoStreamMode.Write))
                    {
                        crypto.Write(plaintext, 0, plaintext.Length);
                        crypto.FlushFinalBlock();
                    }
                    return Convert.ToBase64String(output.ToArray());
                }
            }
        }

        /// <summary>
        /// Validate and decrypt a token. Returns null if the token cannot be decrypted or has expired.
        /// </summary>
        /// <param name="token">The opaque token string.</param>
        /// <returns>The decrypted payload, or null.</returns>
        public TokenPayload? Validate(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;

            try
            {
                byte[] combined = Convert.FromBase64String(token);
                if (combined.Length <= 16) return null;

                byte[] iv = new byte[16];
                Array.Copy(combined, 0, iv, 0, 16);
                byte[] cipher = new byte[combined.Length - 16];
                Array.Copy(combined, 16, cipher, 0, cipher.Length);

                using (Aes aes = Aes.Create())
                {
                    aes.Key = _Key;
                    aes.IV = iv;
                    using (ICryptoTransform decryptor = aes.CreateDecryptor())
                    using (MemoryStream input = new MemoryStream(cipher))
                    using (CryptoStream crypto = new CryptoStream(input, decryptor, CryptoStreamMode.Read))
                    using (MemoryStream output = new MemoryStream())
                    {
                        crypto.CopyTo(output);
                        string json = Encoding.UTF8.GetString(output.ToArray());
                        TokenPayload? payload = JsonSerializer.Deserialize<TokenPayload>(json, _JsonOptions);
                        if (payload == null) return null;
                        if (payload.ExpiresUtc < DateTime.UtcNow) return null;
                        return payload;
                    }
                }
            }
            catch (FormatException)
            {
                return null;
            }
            catch (CryptographicException)
            {
                return null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        #endregion

        #region Private-Methods

        private static JsonSerializerOptions BuildJsonOptions()
        {
            JsonSerializerOptions options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        #endregion
    }
}

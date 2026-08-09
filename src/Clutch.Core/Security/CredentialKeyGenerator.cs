namespace Clutch.Core.Security
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Generates access keys and secret keys for credentials, and computes the stored secret verifier.
    /// </summary>
    public static class CredentialKeyGenerator
    {
        #region Private-Members

        private const string _Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Generate a public access key with the "access_" prefix and 32 random characters.
        /// </summary>
        /// <returns>Access key.</returns>
        public static string GenerateAccessKey()
        {
            return "access_" + RandomString(32);
        }

        /// <summary>
        /// Generate a secret key with the "secret_" prefix and 48 random characters.
        /// </summary>
        /// <returns>Secret key.</returns>
        public static string GenerateSecretKey()
        {
            return "secret_" + RandomString(48);
        }

        /// <summary>
        /// Compute the stored verifier for a secret key. The raw secret is never stored; only this
        /// verifier is retained and compared in constant time at authentication.
        /// </summary>
        /// <param name="secretKey">Raw secret key.</param>
        /// <returns>The verifier (hex SHA-256 hash).</returns>
        /// <exception cref="ArgumentNullException">Thrown when secretKey is null.</exception>
        public static string ComputeVerifier(string secretKey)
        {
            if (secretKey == null) throw new ArgumentNullException(nameof(secretKey));
            return PasswordHasher.Hash(secretKey);
        }

        /// <summary>
        /// Return the last four characters of a secret for operator reference.
        /// </summary>
        /// <param name="secretKey">Raw secret key.</param>
        /// <returns>The last four characters, or the whole string if shorter.</returns>
        public static string Last4(string secretKey)
        {
            if (string.IsNullOrEmpty(secretKey)) return string.Empty;
            return secretKey.Length <= 4 ? secretKey : secretKey.Substring(secretKey.Length - 4);
        }

        #endregion

        #region Private-Methods

        private static string RandomString(int length)
        {
            StringBuilder builder = new StringBuilder(length);
            for (int i = 0; i < length; i++)
            {
                int index = RandomNumberGenerator.GetInt32(_Alphabet.Length);
                builder.Append(_Alphabet[index]);
            }
            return builder.ToString();
        }

        #endregion
    }
}

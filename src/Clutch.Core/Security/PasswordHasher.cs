namespace Clutch.Core.Security
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Hashes and verifies password-equivalent material using SHA-256, with constant-time comparison.
    /// </summary>
    public static class PasswordHasher
    {
        #region Public-Methods

        /// <summary>
        /// Compute the lowercase hex SHA-256 hash of the input.
        /// </summary>
        /// <param name="value">Value to hash.</param>
        /// <returns>Lowercase hex hash.</returns>
        /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
        public static string Hash(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));

            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) builder.Append(b.ToString("x2"));
            return builder.ToString();
        }

        /// <summary>
        /// Verify a plaintext value against an expected hex hash in constant time.
        /// </summary>
        /// <param name="value">Plaintext value.</param>
        /// <param name="expectedHash">Expected hex hash.</param>
        /// <returns>True if the value hashes to the expected hash.</returns>
        public static bool Verify(string value, string expectedHash)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(expectedHash)) return false;
            return FixedTimeEquals(Hash(value), expectedHash);
        }

        /// <summary>
        /// Compare two strings in constant time.
        /// </summary>
        /// <param name="a">First value.</param>
        /// <param name="b">Second value.</param>
        /// <returns>True if equal.</returns>
        public static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            byte[] ba = Encoding.UTF8.GetBytes(a);
            byte[] bb = Encoding.UTF8.GetBytes(b);
            if (ba.Length != bb.Length) return false;
            return CryptographicOperations.FixedTimeEquals(ba, bb);
        }

        #endregion
    }
}

namespace Clutch.Core.Security
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>
    /// Generates access keys for credentials. The access key is the sole credential; no secret key exists.
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

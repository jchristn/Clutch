namespace Clutch.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// How a credential (application key) presents its secret when authenticating.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum CredentialAuthModeEnum
    {
        /// <summary>
        /// The access key and secret key are sent directly in request headers over TLS.
        /// </summary>
        DirectHeader,

        /// <summary>
        /// A request signature derived from the secret key is sent; the raw secret stays client-side.
        /// </summary>
        SignedRequest,

        /// <summary>
        /// The credential is exchanged for a short-lived session token.
        /// </summary>
        SessionExchange,

        /// <summary>
        /// More than one of the above modes is permitted.
        /// </summary>
        Hybrid
    }
}

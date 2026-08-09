namespace Clutch.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// The type of authenticated principal behind a request or session.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PrincipalTypeEnum
    {
        /// <summary>
        /// An interactive user authenticated by email and password.
        /// </summary>
        User,

        /// <summary>
        /// A non-interactive application key (credential).
        /// </summary>
        Credential
    }
}

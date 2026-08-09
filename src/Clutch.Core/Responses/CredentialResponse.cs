namespace Clutch.Core.Responses
{
    using System;
    using Clutch.Core.Enums;
    using Clutch.Core.Models;

    /// <summary>
    /// A credential projection safe to return to clients. The secret verifier is never exposed; the raw
    /// secret key is populated only in the response to a create request.
    /// </summary>
    public class CredentialResponse
    {
        #region Public-Members

        /// <summary>
        /// Credential identifier.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Owning user identifier, if any.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Public access key. This is the sole credential; it is presented on connect and there is no
        /// secret key.
        /// </summary>
        public string AccessKey { get; set; } = string.Empty;

        /// <summary>
        /// Authentication mode.
        /// </summary>
        public CredentialAuthModeEnum AuthMode { get; set; } = CredentialAuthModeEnum.DirectHeader;

        /// <summary>
        /// Optional expiration timestamp.
        /// </summary>
        public DateTime? ExpiresUtc { get; set; } = null;

        /// <summary>
        /// Whether the credential is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// UTC creation timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// UTC last-used timestamp, if any.
        /// </summary>
        public DateTime? LastUsedUtc { get; set; } = null;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Project a credential model to a response.
        /// </summary>
        /// <param name="credential">Credential model.</param>
        /// <returns>The response projection.</returns>
        public static CredentialResponse FromModel(Credential credential)
        {
            CredentialResponse response = new CredentialResponse();
            response.Id = credential.Id;
            response.TenantId = credential.TenantId;
            response.UserId = credential.UserId;
            response.Name = credential.Name;
            response.AccessKey = credential.AccessKey;
            response.AuthMode = credential.AuthMode;
            response.ExpiresUtc = credential.ExpiresUtc;
            response.Active = credential.Active;
            response.CreatedUtc = credential.CreatedUtc;
            response.LastUsedUtc = credential.LastUsedUtc;
            return response;
        }

        #endregion
    }
}

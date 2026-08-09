namespace Clutch.Core.Requests
{
    /// <summary>
    /// The body of a REST lock-release request. Mirrors the WebSocket "release" frame: releases a single
    /// holder owned by the given session. Releasing is idempotent.
    /// </summary>
    public class LockReleaseHttpRequest
    {
        #region Public-Members

        /// <summary>
        /// Identifier of the holder to release, as returned by the acquire that created it.
        /// </summary>
        public string? HolderId { get; set; } = null;

        /// <summary>
        /// Owning session identifier returned by the acquire. Only a holder owned by this session is released.
        /// </summary>
        public string? SessionId { get; set; } = null;

        #endregion
    }
}

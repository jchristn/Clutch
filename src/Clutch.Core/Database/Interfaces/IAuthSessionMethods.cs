namespace Clutch.Core.Database.Interfaces
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Models;

    /// <summary>
    /// Authentication session data access methods.
    /// </summary>
    public interface IAuthSessionMethods
    {
        /// <summary>
        /// Create a session.
        /// </summary>
        /// <param name="session">Session to create.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created session.</returns>
        Task<AuthSession> CreateAsync(AuthSession session, CancellationToken token = default);

        /// <summary>
        /// Read a session by identifier.
        /// </summary>
        /// <param name="id">Session identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The session, or null if not found.</returns>
        Task<AuthSession?> ReadAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Read a session by its token identifier.
        /// </summary>
        /// <param name="tokenId">Token identifier embedded in the issued token.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The session, or null if not found.</returns>
        Task<AuthSession?> ReadByTokenIdAsync(string tokenId, CancellationToken token = default);

        /// <summary>
        /// Mark a session revoked.
        /// </summary>
        /// <param name="id">Session identifier.</param>
        /// <param name="reason">Revocation reason.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if a session was revoked.</returns>
        Task<bool> RevokeAsync(string id, string reason, CancellationToken token = default);

        /// <summary>
        /// Update the last-used timestamp for a session.
        /// </summary>
        /// <param name="id">Session identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Awaitable task.</returns>
        Task TouchLastUsedAsync(string id, CancellationToken token = default);

        /// <summary>
        /// Delete sessions that expired before the given cutoff.
        /// </summary>
        /// <param name="olderThanUtc">Cutoff timestamp.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The number of sessions deleted.</returns>
        Task<int> DeleteExpiredAsync(DateTime olderThanUtc, CancellationToken token = default);
    }
}

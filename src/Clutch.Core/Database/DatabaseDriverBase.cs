namespace Clutch.Core.Database
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database.Interfaces;
    using Clutch.Core.Enums;

    /// <summary>
    /// Abstract base for database drivers. Exposes domain-specific, tenant-aware method interfaces and a
    /// provider-specific initialization path that applies migrations and seeds defaults.
    /// </summary>
    public abstract class DatabaseDriverBase : IDisposable, IAsyncDisposable
    {
        #region Public-Members

        /// <summary>
        /// The provider type of this driver.
        /// </summary>
        public abstract DatabaseTypeEnum DatabaseType { get; }

        /// <summary>
        /// Tenant methods.
        /// </summary>
        public ITenantMethods Tenants { get; protected set; } = null!;

        /// <summary>
        /// User methods.
        /// </summary>
        public IUserMethods Users { get; protected set; } = null!;

        /// <summary>
        /// Credential methods.
        /// </summary>
        public ICredentialMethods Credentials { get; protected set; } = null!;

        /// <summary>
        /// Authentication session methods.
        /// </summary>
        public IAuthSessionMethods Sessions { get; protected set; } = null!;

        /// <summary>
        /// Lock definition methods.
        /// </summary>
        public ILockDefinitionMethods LockDefinitions { get; protected set; } = null!;

        /// <summary>
        /// Lock holder methods, including the atomic acquire path.
        /// </summary>
        public ILockHolderMethods LockHolders { get; protected set; } = null!;

        /// <summary>
        /// Lock audit methods.
        /// </summary>
        public ILockAuditMethods LockAudit { get; protected set; } = null!;

        /// <summary>
        /// Request history methods.
        /// </summary>
        public IRequestHistoryMethods RequestHistory { get; protected set; } = null!;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Initialize the driver: connect, apply migrations, and seed default records if absent.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Awaitable task.</returns>
        public abstract Task InitializeAsync(CancellationToken token = default);

        /// <summary>
        /// Verify database connectivity.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if the database responded.</returns>
        public abstract Task<bool> PingAsync(CancellationToken token = default);

        /// <summary>
        /// Close the driver and release connections.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Awaitable task.</returns>
        public abstract Task CloseAsync(CancellationToken token = default);

        /// <summary>
        /// Dispose the driver.
        /// </summary>
        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Asynchronously dispose the driver.
        /// </summary>
        /// <returns>A completed value task.</returns>
        public virtual async ValueTask DisposeAsync()
        {
            await CloseAsync().ConfigureAwait(false);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}

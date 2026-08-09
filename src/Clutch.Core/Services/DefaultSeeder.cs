namespace Clutch.Core.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database;
    using Clutch.Core.Enums;
    using Clutch.Core.Models;
    using Clutch.Core.Security;

    /// <summary>
    /// Seeds default records on first boot: a default tenant, a system administrator user, and a default
    /// application key. Each record is created only if it does not already exist, so seeding is idempotent.
    /// </summary>
    public static class DefaultSeeder
    {
        #region Public-Members

        /// <summary>
        /// Default tenant name.
        /// </summary>
        public const string DefaultTenantName = "Default";

        /// <summary>
        /// Default administrator email.
        /// </summary>
        public const string DefaultAdminEmail = "admin@clutch.local";

        /// <summary>
        /// Default administrator password. Change immediately in any non-local deployment.
        /// </summary>
        public const string DefaultAdminPassword = "clutchadmin";

        /// <summary>
        /// Default application key access key.
        /// </summary>
        public const string DefaultAccessKey = "clutch-default-access-key";

        /// <summary>
        /// Default application key secret. Shown here only for local development convenience.
        /// </summary>
        public const string DefaultSecretKey = "clutch-default-secret-key";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Seed default records if they are absent.
        /// </summary>
        /// <param name="database">Initialized database driver.</param>
        /// <param name="log">Optional log callback.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Awaitable task.</returns>
        /// <exception cref="ArgumentNullException">Thrown when database is null.</exception>
        public static async Task SeedAsync(DatabaseDriverBase database, Action<string>? log, CancellationToken token = default)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));

            Tenant? tenant = await database.Tenants.ReadByNameAsync(DefaultTenantName, token).ConfigureAwait(false);
            if (tenant == null)
            {
                tenant = new Tenant();
                tenant.Name = DefaultTenantName;
                tenant.IsProtected = true;
                tenant = await database.Tenants.CreateAsync(tenant, token).ConfigureAwait(false);
                if (log != null) log("[Seed] created default tenant '" + DefaultTenantName + "' (" + tenant.Id + ")");
            }

            User? admin = await database.Users.ReadByEmailAsync(tenant.Id, DefaultAdminEmail, token).ConfigureAwait(false);
            if (admin == null)
            {
                admin = new User();
                admin.TenantId = tenant.Id;
                admin.FirstName = "System";
                admin.LastName = "Administrator";
                admin.Email = DefaultAdminEmail;
                admin.PasswordSha256 = PasswordHasher.Hash(DefaultAdminPassword);
                admin.IsSystemAdmin = true;
                admin.IsProtected = true;
                admin = await database.Users.CreateAsync(admin, token).ConfigureAwait(false);
                if (log != null) log("[Seed] created default system admin '" + DefaultAdminEmail + "' (password: " + DefaultAdminPassword + ")");
            }

            Credential? credential = await database.Credentials.ReadByAccessKeyAsync(DefaultAccessKey, token).ConfigureAwait(false);
            if (credential == null)
            {
                credential = new Credential();
                credential.TenantId = tenant.Id;
                credential.UserId = admin.Id;
                credential.Name = "Default Application Key";
                credential.AccessKey = DefaultAccessKey;
                credential.SecretKeyEncrypted = CredentialKeyGenerator.ComputeVerifier(DefaultSecretKey);
                credential.SecretKeyLast4 = CredentialKeyGenerator.Last4(DefaultSecretKey);
                credential.AuthMode = CredentialAuthModeEnum.DirectHeader;
                credential.IsProtected = true;
                credential = await database.Credentials.CreateAsync(credential, token).ConfigureAwait(false);
                if (log != null) log("[Seed] created default application key access=" + DefaultAccessKey + " secret=" + DefaultSecretKey);
            }
        }

        #endregion
    }
}

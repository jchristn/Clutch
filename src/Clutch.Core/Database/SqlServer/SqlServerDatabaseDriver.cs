namespace Clutch.Core.Database.SqlServer
{
    using System;
    using System.Data.Common;
    using Clutch.Core.Database.Ado;
    using Clutch.Core.Database.Sql;
    using Clutch.Core.Enums;
    using Microsoft.Data.SqlClient;

    /// <summary>
    /// Microsoft SQL Server database driver. Supplies a Microsoft.Data.SqlClient connection and the SQL
    /// Server dialect. Concurrent acquirers serialize via UPDLOCK/ROWLOCK/HOLDLOCK range locks on the
    /// definition row read.
    /// </summary>
    public class SqlServerDatabaseDriver : AdoDatabaseDriver
    {
        #region Public-Members

        /// <inheritdoc />
        public override DatabaseTypeEnum DatabaseType
        {
            get
            {
                return DatabaseTypeEnum.SqlServer;
            }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        public SqlServerDatabaseDriver(DatabaseSettings settings)
            : base(settings, new SqlServerDialect())
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override DbConnection CreateConnection()
        {
            return new SqlConnection(Settings.ToSqlServerConnectionString());
        }

        #endregion
    }
}

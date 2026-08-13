namespace Clutch.Core.Database.Mysql
{
    using System;
    using System.Data.Common;
    using Clutch.Core.Database.Ado;
    using Clutch.Core.Database.Sql;
    using Clutch.Core.Enums;
    using MySqlConnector;

    /// <summary>
    /// MySQL database driver. Supplies a MySqlConnector connection and the MySQL dialect. Concurrent
    /// acquirers serialize via InnoDB's FOR UPDATE gap lock on the unique (tenantid, lockkey) index.
    /// </summary>
    public class MysqlDatabaseDriver : AdoDatabaseDriver
    {
        #region Public-Members

        /// <inheritdoc />
        public override DatabaseTypeEnum DatabaseType
        {
            get
            {
                return DatabaseTypeEnum.Mysql;
            }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when settings is null.</exception>
        public MysqlDatabaseDriver(DatabaseSettings settings)
            : base(settings, new MysqlDialect())
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override DbConnection CreateConnection()
        {
            return new MySqlConnection(Settings.ToMysqlConnectionString());
        }

        #endregion
    }
}

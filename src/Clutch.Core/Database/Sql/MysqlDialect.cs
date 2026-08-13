namespace Clutch.Core.Database.Sql
{
    using System;
    using Clutch.Core.Enums;

    /// <summary>
    /// MySQL SQL dialect. MySQL has no RETURNING/OUTPUT support, so the shared implementations read rows
    /// under the acquire row lock before mutating them.
    /// </summary>
    public class MysqlDialect : SqlDialect
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

        /// <inheritdoc />
        public override string TypeText
        {
            get
            {
                return "TEXT";
            }
        }

        /// <inheritdoc />
        public override string TypeBool
        {
            get
            {
                return "TINYINT(1)";
            }
        }

        /// <inheritdoc />
        public override string TypeDouble
        {
            get
            {
                return "DOUBLE";
            }
        }

        /// <inheritdoc />
        public override string TypeTimestamp
        {
            get
            {
                return "DATETIME(6)";
            }
        }

        /// <inheritdoc />
        public override string TypeJson
        {
            get
            {
                return "JSON";
            }
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override string Quote(string identifier)
        {
            if (String.IsNullOrEmpty(identifier)) throw new ArgumentNullException(nameof(identifier));
            return "`" + identifier.Replace("`", "``") + "`";
        }

        /// <inheritdoc />
        public override string QualifiedReference(string? schema, string rawName)
        {
            // MySQL's "schema" is the database itself; ignore the configured schema and quote the table.
            if (String.IsNullOrEmpty(rawName)) throw new ArgumentNullException(nameof(rawName));
            return Quote(rawName);
        }

        /// <inheritdoc />
        public override string LimitOffsetClause()
        {
            return " LIMIT @max OFFSET @skip";
        }

        /// <inheritdoc />
        public override string BooleanLiteral(bool value)
        {
            return value ? "TRUE" : "FALSE";
        }

        /// <inheritdoc />
        public override string CaseInsensitiveLike(string columnReference, string parameterName)
        {
            return "LOWER(" + columnReference + ") LIKE LOWER(@" + parameterName + ")";
        }

        /// <inheritdoc />
        public override string DefinitionLockSelect(string tableReference)
        {
            return "SELECT * FROM " + tableReference + " WHERE tenantid = @tid AND lockkey = @key FOR UPDATE";
        }

        /// <inheritdoc />
        public override string ExistsSelect(string tableReference, string whereClause)
        {
            return "SELECT 1 FROM " + tableReference + " " + whereClause + " LIMIT 1";
        }

        /// <inheritdoc />
        public override string CreateTable(string? schema, string rawName, string columnsCsv)
        {
            return "CREATE TABLE IF NOT EXISTS " + Quote(rawName) + " (\n" + columnsCsv + "\n);";
        }

        /// <inheritdoc />
        public override string CreateIndex(string indexName, string? schema, string rawTableName, string columnsCsv, bool unique)
        {
            // MySQL does not support CREATE INDEX IF NOT EXISTS; migrations run once via version tracking.
            string keyword = unique ? "CREATE UNIQUE INDEX" : "CREATE INDEX";
            return keyword + " " + Quote(indexName) + " ON " + Quote(rawTableName) + " (" + columnsCsv + ");";
        }

        /// <inheritdoc />
        public override string TableExistsProbe(string? schema, string rawName)
        {
            return "SELECT 1 FROM information_schema.tables WHERE table_schema = DATABASE() AND table_name = @name LIMIT 1;";
        }

        /// <inheritdoc />
        public override bool IsTransientConflict(Exception exception)
        {
            // 1213 ER_LOCK_DEADLOCK, 1205 ER_LOCK_WAIT_TIMEOUT.
            if (exception is MySqlConnector.MySqlException my) return my.Number == 1213 || my.Number == 1205;
            return false;
        }

        #endregion
    }
}

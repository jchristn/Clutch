namespace Clutch.Core.Database.Sql
{
    using System;
    using Clutch.Core.Enums;

    /// <summary>
    /// SQLite SQL dialect. SQLite is single-node; concurrent acquirers are serialized by opening the
    /// acquire transaction in IMMEDIATE mode rather than with a row-lock hint.
    /// </summary>
    public class SqliteDialect : SqlDialect
    {
        #region Public-Members

        /// <inheritdoc />
        public override DatabaseTypeEnum DatabaseType
        {
            get
            {
                return DatabaseTypeEnum.Sqlite;
            }
        }

        /// <inheritdoc />
        public override bool SupportsReturning
        {
            get
            {
                return true;
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
                return "INTEGER";
            }
        }

        /// <inheritdoc />
        public override string TypeDouble
        {
            get
            {
                return "REAL";
            }
        }

        /// <inheritdoc />
        public override string TypeTimestamp
        {
            get
            {
                return "TEXT";
            }
        }

        /// <inheritdoc />
        public override string TypeJson
        {
            get
            {
                return "TEXT";
            }
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override string Quote(string identifier)
        {
            if (String.IsNullOrEmpty(identifier)) throw new ArgumentNullException(nameof(identifier));
            return "\"" + identifier.Replace("\"", "\"\"") + "\"";
        }

        /// <inheritdoc />
        public override string QualifiedReference(string? schema, string rawName)
        {
            // SQLite has no schema namespace in this deployment model; ignore schema.
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
            return value ? "1" : "0";
        }

        /// <inheritdoc />
        public override string CaseInsensitiveLike(string columnReference, string parameterName)
        {
            return "LOWER(" + columnReference + ") LIKE LOWER(@" + parameterName + ")";
        }

        /// <inheritdoc />
        public override string DefinitionLockSelect(string tableReference)
        {
            return "SELECT * FROM " + tableReference + " WHERE tenantid = @tid AND lockkey = @key";
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
            string keyword = unique ? "CREATE UNIQUE INDEX" : "CREATE INDEX";
            return keyword + " IF NOT EXISTS " + Quote(indexName) + " ON " + Quote(rawTableName) + " (" + columnsCsv + ");";
        }

        /// <inheritdoc />
        public override string TableExistsProbe(string? schema, string rawName)
        {
            return "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = @name LIMIT 1;";
        }

        /// <inheritdoc />
        public override bool IsTransientConflict(Exception exception)
        {
            // 5 SQLITE_BUSY, 6 SQLITE_LOCKED.
            if (exception is Microsoft.Data.Sqlite.SqliteException sq) return sq.SqliteErrorCode == 5 || sq.SqliteErrorCode == 6;
            return false;
        }

        #endregion
    }
}

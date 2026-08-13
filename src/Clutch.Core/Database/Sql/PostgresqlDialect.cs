namespace Clutch.Core.Database.Sql
{
    using System;
    using Clutch.Core.Enums;

    /// <summary>
    /// PostgreSQL SQL dialect.
    /// </summary>
    public class PostgresqlDialect : SqlDialect
    {
        #region Public-Members

        /// <inheritdoc />
        public override DatabaseTypeEnum DatabaseType
        {
            get
            {
                return DatabaseTypeEnum.Postgresql;
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
        public override string JsonInsertCast
        {
            get
            {
                return "::jsonb";
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
                return "BOOLEAN";
            }
        }

        /// <inheritdoc />
        public override string TypeDouble
        {
            get
            {
                return "DOUBLE PRECISION";
            }
        }

        /// <inheritdoc />
        public override string TypeTimestamp
        {
            get
            {
                return "TIMESTAMPTZ";
            }
        }

        /// <inheritdoc />
        public override string TypeJson
        {
            get
            {
                return "JSONB";
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
            return columnReference + " ILIKE @" + parameterName;
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
            return "CREATE TABLE IF NOT EXISTS " + QualifiedReference(schema, rawName) + " (\n" + columnsCsv + "\n);";
        }

        /// <inheritdoc />
        public override string CreateIndex(string indexName, string? schema, string rawTableName, string columnsCsv, bool unique)
        {
            string keyword = unique ? "CREATE UNIQUE INDEX" : "CREATE INDEX";
            return keyword + " IF NOT EXISTS " + Quote(indexName) + " ON " + QualifiedReference(schema, rawTableName) + " (" + columnsCsv + ");";
        }

        /// <inheritdoc />
        public override string TableExistsProbe(string? schema, string rawName)
        {
            if (String.IsNullOrEmpty(schema)) return "SELECT 1 FROM information_schema.tables WHERE table_name = @name LIMIT 1;";
            return "SELECT 1 FROM information_schema.tables WHERE table_name = @name AND table_schema = @schema LIMIT 1;";
        }

        /// <inheritdoc />
        public override string? CreateSchemaStatement(string schema)
        {
            if (String.IsNullOrEmpty(schema)) return null;
            return "CREATE SCHEMA IF NOT EXISTS " + Quote(schema) + ";";
        }

        /// <inheritdoc />
        public override bool IsTransientConflict(Exception exception)
        {
            // 40001 serialization_failure, 40P01 deadlock_detected.
            if (exception is Npgsql.PostgresException pg) return pg.SqlState == "40001" || pg.SqlState == "40P01";
            return false;
        }

        #endregion
    }
}

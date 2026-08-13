namespace Clutch.Core.Database.Sql
{
    using System;
    using Clutch.Core.Enums;

    /// <summary>
    /// Microsoft SQL Server dialect. Uses OUTPUT instead of RETURNING, UPDLOCK/HOLDLOCK range locks to
    /// serialize acquirers, BIT for booleans, and OFFSET/FETCH pagination.
    /// </summary>
    public class SqlServerDialect : SqlDialect
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

        /// <inheritdoc />
        public override bool SupportsOutput
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
                return "NVARCHAR(MAX)";
            }
        }

        /// <inheritdoc />
        public override string TypeBool
        {
            get
            {
                return "BIT";
            }
        }

        /// <inheritdoc />
        public override string TypeDouble
        {
            get
            {
                return "FLOAT";
            }
        }

        /// <inheritdoc />
        public override string TypeTimestamp
        {
            get
            {
                return "DATETIME2(7)";
            }
        }

        /// <inheritdoc />
        public override string TypeJson
        {
            get
            {
                return "NVARCHAR(MAX)";
            }
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override string Quote(string identifier)
        {
            if (String.IsNullOrEmpty(identifier)) throw new ArgumentNullException(nameof(identifier));
            return "[" + identifier.Replace("]", "]]") + "]";
        }

        /// <inheritdoc />
        public override string LimitOffsetClause()
        {
            return " OFFSET @skip ROWS FETCH NEXT @max ROWS ONLY";
        }

        /// <inheritdoc />
        public override string BooleanLiteral(bool value)
        {
            return value ? "1" : "0";
        }

        /// <inheritdoc />
        public override string CaseInsensitiveLike(string columnReference, string parameterName)
        {
            return columnReference + " LIKE @" + parameterName + " COLLATE Latin1_General_CI_AS";
        }

        /// <inheritdoc />
        public override string DefinitionLockSelect(string tableReference)
        {
            return "SELECT * FROM " + tableReference + " WITH (UPDLOCK, ROWLOCK, HOLDLOCK) WHERE tenantid = @tid AND lockkey = @key";
        }

        /// <inheritdoc />
        public override string ExistsSelect(string tableReference, string whereClause)
        {
            return "SELECT TOP 1 1 FROM " + tableReference + " " + whereClause;
        }

        /// <inheritdoc />
        public override string CreateTable(string? schema, string rawName, string columnsCsv)
        {
            string reference = QualifiedReference(schema, rawName);
            string objectName = String.IsNullOrEmpty(schema) ? rawName : schema + "." + rawName;
            return
                "IF OBJECT_ID(N'" + objectName.Replace("'", "''") + "', N'U') IS NULL\n" +
                "BEGIN\n" +
                "CREATE TABLE " + reference + " (\n" + columnsCsv + "\n);\n" +
                "END;";
        }

        /// <inheritdoc />
        public override string CreateIndex(string indexName, string? schema, string rawTableName, string columnsCsv, bool unique)
        {
            string reference = QualifiedReference(schema, rawTableName);
            string objectName = String.IsNullOrEmpty(schema) ? rawTableName : schema + "." + rawTableName;
            string keyword = unique ? "CREATE UNIQUE INDEX" : "CREATE INDEX";
            return
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'" + indexName.Replace("'", "''") +
                "' AND object_id = OBJECT_ID(N'" + objectName.Replace("'", "''") + "'))\n" +
                "BEGIN\n" +
                keyword + " " + Quote(indexName) + " ON " + reference + " (" + columnsCsv + ");\n" +
                "END;";
        }

        /// <inheritdoc />
        public override string TableExistsProbe(string? schema, string rawName)
        {
            if (String.IsNullOrEmpty(schema)) return "SELECT TOP 1 1 FROM sys.tables WHERE name = @name;";
            return "SELECT TOP 1 1 FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id WHERE t.name = @name AND s.name = @schema;";
        }

        /// <inheritdoc />
        public override string? CreateSchemaStatement(string schema)
        {
            if (String.IsNullOrEmpty(schema)) return null;
            return
                "IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'" + schema.Replace("'", "''") + "')\n" +
                "EXEC('CREATE SCHEMA " + Quote(schema) + "');";
        }

        /// <inheritdoc />
        public override bool IsTransientConflict(Exception exception)
        {
            // 1205 deadlock victim, 1222 lock request timeout.
            if (exception is Microsoft.Data.SqlClient.SqlException se) return se.Number == 1205 || se.Number == 1222;
            return false;
        }

        #endregion
    }
}

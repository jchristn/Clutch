namespace Clutch.Core.Database
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A versioned, ordered set of schema statements applied idempotently and tracked in the
    /// schema_migrations table.
    /// </summary>
    public class SchemaMigration
    {
        #region Public-Members

        /// <summary>
        /// Monotonic version number. Applied in ascending order.
        /// </summary>
        public int Version { get; set; } = 0;

        /// <summary>
        /// Human-readable description.
        /// </summary>
        public string Description
        {
            get
            {
                return _Description;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Description));
                _Description = value;
            }
        }

        /// <summary>
        /// Ordered SQL statements to execute for this migration.
        /// </summary>
        public List<string> Statements { get; set; } = new List<string>();

        #endregion

        #region Private-Members

        private string _Description = String.Empty;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SchemaMigration()
        {
        }

        /// <summary>
        /// Instantiate with a version, description, and statements.
        /// </summary>
        /// <param name="version">Version number.</param>
        /// <param name="description">Description.</param>
        /// <param name="statements">Ordered statements.</param>
        public SchemaMigration(int version, string description, List<string> statements)
        {
            if (String.IsNullOrEmpty(description)) throw new ArgumentNullException(nameof(description));
            if (statements == null) throw new ArgumentNullException(nameof(statements));

            Version = version;
            Description = description;
            Statements = statements;
        }

        #endregion
    }
}

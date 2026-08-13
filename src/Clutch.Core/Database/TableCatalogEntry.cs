namespace Clutch.Core.Database
{
    using System;

    /// <summary>
    /// One resolved table in the catalog: its purpose, the raw (unquoted) table name, the optional schema,
    /// and the provider-quoted reference used in SQL.
    /// </summary>
    public class TableCatalogEntry
    {
        #region Public-Members

        /// <summary>
        /// Friendly purpose key, e.g. "lockHolders".
        /// </summary>
        public string Purpose { get; }

        /// <summary>
        /// Resolved, unquoted table name, e.g. "clutch_lock_holders".
        /// </summary>
        public string RawName { get; }

        /// <summary>
        /// Optional schema/namespace, or null.
        /// </summary>
        public string? Schema { get; }

        /// <summary>
        /// Provider-quoted, schema-qualified reference used directly in SQL text.
        /// </summary>
        public string Reference { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="purpose">Purpose key.</param>
        /// <param name="rawName">Resolved unquoted table name.</param>
        /// <param name="schema">Optional schema.</param>
        /// <param name="reference">Provider-quoted reference.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null or empty.</exception>
        public TableCatalogEntry(string purpose, string rawName, string? schema, string reference)
        {
            if (String.IsNullOrEmpty(purpose)) throw new ArgumentNullException(nameof(purpose));
            if (String.IsNullOrEmpty(rawName)) throw new ArgumentNullException(nameof(rawName));
            if (String.IsNullOrEmpty(reference)) throw new ArgumentNullException(nameof(reference));

            Purpose = purpose;
            RawName = rawName;
            Schema = schema;
            Reference = reference;
        }

        #endregion
    }
}

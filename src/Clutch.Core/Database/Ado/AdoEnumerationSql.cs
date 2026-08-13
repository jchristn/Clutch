namespace Clutch.Core.Database.Ado
{
    using Clutch.Core.Enumeration;

    /// <summary>
    /// Helpers for translating an <see cref="EnumerationQuery"/> into SQL clauses. Column names are supplied
    /// by the calling data-access method (never from user input).
    /// </summary>
    public static class AdoEnumerationSql
    {
        /// <summary>
        /// Build an ORDER BY clause for the query's ordering, falling back to the created column when the
        /// entity has no natural name column.
        /// </summary>
        /// <param name="query">Enumeration query.</param>
        /// <param name="createdColumn">Creation-timestamp column name.</param>
        /// <param name="nameColumn">Natural name column name, or null when the entity has none.</param>
        /// <returns>An " ORDER BY ..." clause with a leading space.</returns>
        public static string OrderClause(EnumerationQuery query, string createdColumn, string? nameColumn)
        {
            switch (query.Ordering)
            {
                case EnumerationOrderEnum.CreatedAscending:
                    return " ORDER BY " + createdColumn + " ASC";
                case EnumerationOrderEnum.NameAscending:
                    return " ORDER BY " + (nameColumn ?? createdColumn) + " ASC";
                case EnumerationOrderEnum.NameDescending:
                    return " ORDER BY " + (nameColumn ?? createdColumn) + " DESC";
                case EnumerationOrderEnum.CreatedDescending:
                default:
                    return " ORDER BY " + createdColumn + " DESC";
            }
        }
    }
}

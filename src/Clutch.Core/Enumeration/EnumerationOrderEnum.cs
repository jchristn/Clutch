namespace Clutch.Core.Enumeration
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ordering options for paginated enumeration.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum EnumerationOrderEnum
    {
        /// <summary>
        /// Oldest records first.
        /// </summary>
        CreatedAscending,

        /// <summary>
        /// Newest records first (default).
        /// </summary>
        CreatedDescending,

        /// <summary>
        /// Order ascending by the record's natural name column (name, email, or key), where one exists.
        /// </summary>
        NameAscending,

        /// <summary>
        /// Order descending by the record's natural name column (name, email, or key), where one exists.
        /// </summary>
        NameDescending
    }
}

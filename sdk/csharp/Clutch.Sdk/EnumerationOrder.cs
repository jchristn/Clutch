namespace Clutch.Sdk
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ordering options for paginated enumeration endpoints.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum EnumerationOrder
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
        /// Ascending by the record's natural name column (name, email, or key), where one exists.
        /// </summary>
        NameAscending,

        /// <summary>
        /// Descending by the record's natural name column (name, email, or key), where one exists.
        /// </summary>
        NameDescending
    }
}

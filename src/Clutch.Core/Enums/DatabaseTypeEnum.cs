namespace Clutch.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Supported database provider types. Clutch implements Postgresql only; the other values exist so
    /// the provider-neutral abstraction can be extended without a breaking change.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DatabaseTypeEnum
    {
        /// <summary>
        /// PostgreSQL. The only provider implemented by Clutch.
        /// </summary>
        Postgresql,

        /// <summary>
        /// SQLite. Reserved for future implementation.
        /// </summary>
        Sqlite,

        /// <summary>
        /// MySQL. Reserved for future implementation.
        /// </summary>
        Mysql,

        /// <summary>
        /// Microsoft SQL Server. Reserved for future implementation.
        /// </summary>
        SqlServer
    }
}

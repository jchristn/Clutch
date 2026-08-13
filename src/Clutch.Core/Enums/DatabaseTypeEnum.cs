namespace Clutch.Core.Enums
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Supported database provider types. All four are implemented. SQLite is intended for single-node
    /// deployments; PostgreSQL, MySQL, and SQL Server support multi-node clustering.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DatabaseTypeEnum
    {
        /// <summary>
        /// PostgreSQL. Supports multi-node clustering.
        /// </summary>
        Postgresql,

        /// <summary>
        /// SQLite. Single-node, development, and embedded deployments only.
        /// </summary>
        Sqlite,

        /// <summary>
        /// MySQL. Supports multi-node clustering.
        /// </summary>
        Mysql,

        /// <summary>
        /// Microsoft SQL Server. Supports multi-node clustering.
        /// </summary>
        SqlServer
    }
}

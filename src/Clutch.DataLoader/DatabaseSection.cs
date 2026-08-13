namespace Clutch.DataLoader
{
    /// <summary>
    /// Typed view of the connection fields in the <c>Database</c> section of a Clutch settings file. Every
    /// property is nullable so a field absent from the file leaves the corresponding loader option unchanged.
    /// Deserialization is case-insensitive, so both PascalCase and camelCase property names bind here.
    /// </summary>
    public sealed class DatabaseSection
    {
        /// <summary>
        /// Database host, or null when absent.
        /// </summary>
        public string? Host { get; set; } = null;

        /// <summary>
        /// Database port, or null when absent.
        /// </summary>
        public int? Port { get; set; } = null;

        /// <summary>
        /// Database/catalog name, or null when absent.
        /// </summary>
        public string? DatabaseName { get; set; } = null;

        /// <summary>
        /// Login user, or null when absent.
        /// </summary>
        public string? Username { get; set; } = null;

        /// <summary>
        /// Login password, or null when absent.
        /// </summary>
        public string? Password { get; set; } = null;
    }
}

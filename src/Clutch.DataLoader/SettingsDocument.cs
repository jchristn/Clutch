namespace Clutch.DataLoader
{
    /// <summary>
    /// Minimal typed view of a Clutch settings file (clutch.json) for the data loader. Only the fields the
    /// loader needs are declared; any other properties in the file are ignored during deserialization.
    /// </summary>
    public sealed class SettingsDocument
    {
        /// <summary>
        /// The database section of the settings file, or null when absent.
        /// </summary>
        public DatabaseSection? Database { get; set; } = null;
    }
}

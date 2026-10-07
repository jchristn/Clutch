namespace Clutch.Server.Responses
{
    using Clutch.Server.Settings;

    /// <summary>
    /// Response for saving server settings.
    /// </summary>
    public class SettingsSaveResponse
    {
        #region Public-Members

        /// <summary>
        /// True when settings were written.
        /// </summary>
        public bool Saved { get; set; } = true;

        /// <summary>
        /// True when a restart is needed.
        /// </summary>
        public bool RestartRequired { get; set; } = true;

        /// <summary>
        /// Human-readable message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// The saved settings, with secrets redacted.
        /// </summary>
        public ClutchSettings? Settings { get; set; } = null;

        #endregion
    }
}

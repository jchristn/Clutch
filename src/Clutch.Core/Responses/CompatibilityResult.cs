namespace Clutch.Core.Responses
{
    /// <summary>
    /// The result of evaluating whether a requested lock mode is compatible with the current holders.
    /// </summary>
    public class CompatibilityResult
    {
        #region Public-Members

        /// <summary>
        /// Whether the requested mode may be granted.
        /// </summary>
        public bool Compatible { get; set; } = false;

        /// <summary>
        /// Human-readable reason when not compatible.
        /// </summary>
        public string? Reason { get; set; } = null;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create a compatible result.
        /// </summary>
        /// <returns>A compatible result.</returns>
        public static CompatibilityResult Ok()
        {
            CompatibilityResult result = new CompatibilityResult();
            result.Compatible = true;
            return result;
        }

        /// <summary>
        /// Create an incompatible result with a reason.
        /// </summary>
        /// <param name="reason">Reason the mode cannot be granted.</param>
        /// <returns>An incompatible result.</returns>
        public static CompatibilityResult Blocked(string reason)
        {
            CompatibilityResult result = new CompatibilityResult();
            result.Compatible = false;
            result.Reason = reason;
            return result;
        }

        #endregion
    }
}

namespace Clutch.Sdk
{
    /// <summary>
    /// Exception thrown when a lock acquisition is denied, times out, or conflicts with an existing policy.
    /// </summary>
    public class LockDeniedException : ClutchException
    {
        private AcquireResult _Result;
        private string? _Key;

        /// <summary>
        /// The reason the acquisition did not succeed.
        /// </summary>
        public AcquireResult Result
        {
            get
            {
                return _Result;
            }
        }

        /// <summary>
        /// The key that was requested.
        /// </summary>
        public string? Key
        {
            get
            {
                return _Key;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LockDeniedException"/> class.
        /// </summary>
        /// <param name="result">The reason the acquisition did not succeed.</param>
        /// <param name="key">The key that was requested.</param>
        /// <param name="message">A human-readable explanation.</param>
        public LockDeniedException(AcquireResult result, string? key, string message) : base(message)
        {
            _Result = result;
            _Key = key;
        }
    }
}

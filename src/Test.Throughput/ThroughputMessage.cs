namespace Test.Throughput
{
    /// <summary>
    /// Minimal typed view of a Clutch WebSocket frame for throughput measurement. Only the fields the runner
    /// inspects are declared; other properties on the frame are ignored during deserialization.
    /// </summary>
    public sealed class ThroughputMessage
    {
        /// <summary>
        /// Message type discriminator (for example <c>acquired</c> or <c>released</c>), or null when absent.
        /// </summary>
        public string? Type { get; set; } = null;

        /// <summary>
        /// Identifier of the lock holder the frame refers to, or null when absent.
        /// </summary>
        public string? HolderId { get; set; } = null;
    }
}

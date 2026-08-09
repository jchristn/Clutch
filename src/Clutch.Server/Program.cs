namespace Clutch.Server
{
    /// <summary>
    /// Application entry point.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point. Delegates immediately to the bootstrapper.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        public static void Main(string[] args)
        {
            Bootstrapper.Run(args);
        }
    }
}

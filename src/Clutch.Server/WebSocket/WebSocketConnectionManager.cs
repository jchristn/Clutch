namespace Clutch.Server.WebSocket
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    /// <summary>
    /// Tracks active Clutch WebSocket sessions on this node.
    /// </summary>
    public class WebSocketConnectionManager
    {
        #region Private-Members

        private readonly ConcurrentDictionary<string, ClutchWsSession> _Sessions = new ConcurrentDictionary<string, ClutchWsSession>();

        #endregion

        #region Public-Methods

        /// <summary>
        /// The number of active sessions on this node.
        /// </summary>
        public int Count
        {
            get
            {
                return _Sessions.Count;
            }
        }

        /// <summary>
        /// Register a session.
        /// </summary>
        /// <param name="session">Session to add.</param>
        public void Add(ClutchWsSession session)
        {
            if (session == null) return;
            _Sessions[session.SessionId] = session;
        }

        /// <summary>
        /// Remove a session.
        /// </summary>
        /// <param name="sessionId">Session identifier.</param>
        public void Remove(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return;
            _Sessions.TryRemove(sessionId, out _);
        }

        /// <summary>
        /// Get a session by identifier.
        /// </summary>
        /// <param name="sessionId">Session identifier.</param>
        /// <returns>The session, or null.</returns>
        public ClutchWsSession? Get(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return null;
            _Sessions.TryGetValue(sessionId, out ClutchWsSession? session);
            return session;
        }

        /// <summary>
        /// Enumerate all active sessions.
        /// </summary>
        /// <returns>The active sessions.</returns>
        public IReadOnlyCollection<ClutchWsSession> All()
        {
            return new List<ClutchWsSession>(_Sessions.Values);
        }

        #endregion
    }
}

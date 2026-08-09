namespace Clutch.Core.Database.Postgresql.Notifications
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Npgsql;

    /// <summary>
    /// Listens on a PostgreSQL LISTEN/NOTIFY channel on a dedicated connection and raises an event when a
    /// lock on a key is released or expires, so blocked waiters on this node can retry.
    /// </summary>
    public class PostgresNotificationListener : IAsyncDisposable
    {
        #region Public-Members

        /// <summary>
        /// Raised when a key is released or expired. The first argument is the tenant identifier and the
        /// second is the lock key.
        /// </summary>
        public event Action<string, string>? KeyReleased;

        #endregion

        #region Private-Members

        private readonly PostgresqlDatabaseDriver _Driver;
        private readonly string _Channel;
        private NpgsqlConnection? _Connection;
        private CancellationTokenSource? _Cts;
        private Task? _Loop;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="driver">Owning driver.</param>
        /// <param name="channel">Channel name to listen on.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public PostgresNotificationListener(PostgresqlDatabaseDriver driver, string channel)
        {
            _Driver = driver ?? throw new ArgumentNullException(nameof(driver));
            if (String.IsNullOrEmpty(channel)) throw new ArgumentNullException(nameof(channel));
            _Channel = channel;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Open the dedicated connection, issue LISTEN, and begin the wait loop.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Awaitable task.</returns>
        public async Task StartAsync(CancellationToken token = default)
        {
            _Connection = await _Driver.OpenConnectionAsync(token).ConfigureAwait(false);
            _Connection.Notification += OnNotification;

            await using (NpgsqlCommand command = new NpgsqlCommand("LISTEN " + _Channel + ";", _Connection))
            {
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            _Cts = new CancellationTokenSource();
            _Loop = Task.Run(() => WaitLoopAsync(_Cts.Token));
        }

        /// <summary>
        /// Stop listening and release the dedicated connection.
        /// </summary>
        /// <returns>A completed value task.</returns>
        public async ValueTask DisposeAsync()
        {
            if (_Disposed) return;
            _Disposed = true;

            if (_Cts != null) _Cts.Cancel();
            if (_Loop != null)
            {
                try
                {
                    await _Loop.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            if (_Connection != null)
            {
                _Connection.Notification -= OnNotification;
                await _Connection.DisposeAsync().ConfigureAwait(false);
            }

            if (_Cts != null) _Cts.Dispose();
            GC.SuppressFinalize(this);
        }

        #endregion

        #region Private-Methods

        private async Task WaitLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (_Connection == null) break;
                    await _Connection.WaitAsync(token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Transient wait failure; back off briefly and continue.
                    try
                    {
                        await Task.Delay(1000, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private void OnNotification(object sender, NpgsqlNotificationEventArgs args)
        {
            string payload = args.Payload;
            if (String.IsNullOrEmpty(payload)) return;

            int separator = payload.IndexOf('|');
            if (separator <= 0 || separator >= payload.Length - 1) return;

            string tenantId = payload.Substring(0, separator);
            string lockKey = payload.Substring(separator + 1);
            Action<string, string>? handler = KeyReleased;
            if (handler != null) handler(tenantId, lockKey);
        }

        #endregion
    }
}

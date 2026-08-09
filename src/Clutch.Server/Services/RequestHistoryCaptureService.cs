namespace Clutch.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Clutch.Core.Database;
    using Clutch.Core.Models;
    using Clutch.Core.Security;
    using Clutch.Server.Settings;
    using SyslogLogging;
    using WatsonWebserver.Core;

    /// <summary>
    /// Captures a durable record of each HTTP request. The entry is built synchronously from the context
    /// in the PostRouting hook, then inserted on a fire-and-forget task so capture never blocks the
    /// response. Secret-bearing headers are redacted and bodies are truncated to a threshold.
    /// </summary>
    public class RequestHistoryCaptureService
    {
        #region Private-Members

        private readonly DatabaseDriverBase _Database;
        private readonly RequestHistorySettings _Settings;
        private readonly LoggingModule _Logging;

        private static readonly HashSet<string> _RedactedHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "authorization", "proxy-authorization", "cookie", "set-cookie", "x-token", "x-secret-key", "x-clutch-secret-key"
        };

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database driver.</param>
        /// <param name="settings">Request history settings.</param>
        /// <param name="logging">Logging module.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
        public RequestHistoryCaptureService(DatabaseDriverBase database, RequestHistorySettings settings, LoggingModule logging)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Logging = logging ?? throw new ArgumentNullException(nameof(logging));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build a history entry from the context and dispatch the insert without blocking.
        /// </summary>
        /// <param name="context">HTTP context.</param>
        public void Capture(HttpContextBase context)
        {
            try
            {
                RequestHistoryEntry entry = BuildEntry(context);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _Database.RequestHistory.CreateAsync(entry, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception e)
                    {
                        _Logging.Warn("[Clutch.Capture] failed to persist request history: " + e.Message);
                    }
                });
            }
            catch (Exception e)
            {
                _Logging.Warn("[Clutch.Capture] failed to build request history entry: " + e.Message);
            }
        }

        #endregion

        #region Private-Methods

        private RequestHistoryEntry BuildEntry(HttpContextBase context)
        {
            RequestHistoryEntry entry = new RequestHistoryEntry();
            entry.Method = context.Request.Method.ToString();
            entry.Path = context.Request.Url.RawWithoutQuery;
            entry.Url = context.Request.Url.RawWithQuery;
            entry.StatusCode = context.Response.StatusCode;
            entry.DurationMs = context.Timestamp.TotalMs ?? 0;
            entry.CreatedUtc = context.Timestamp.Start;
            entry.CompletedUtc = context.Timestamp.End;
            entry.SourceIp = context.Request.Headers["X-Forwarded-For"];

            if (context.Metadata is RequestContext requestContext && requestContext.IsAuthenticated)
            {
                entry.TenantId = requestContext.TenantId;
                entry.UserId = requestContext.UserId;
                entry.PrincipalName = requestContext.PrincipalName;
            }

            entry.RequestHeaders = CopyHeaders(context.Request.Headers);
            entry.ResponseHeaders = CopyHeaders(context.Response.Headers);

            string requestBody = SafeRequestBody(context);
            AssignBody(requestBody, _Settings.MaxRequestBodyBytes,
                (value, bytes, truncated) => { entry.RequestBody = value; entry.RequestBodyBytes = bytes; entry.RequestBodyTruncated = truncated; });

            string responseBody = SafeResponseBody(context);
            AssignBody(responseBody, _Settings.MaxResponseBodyBytes,
                (value, bytes, truncated) => { entry.ResponseBody = value; entry.ResponseBodyBytes = bytes; entry.ResponseBodyTruncated = truncated; });

            return entry;
        }

        private static Dictionary<string, string> CopyHeaders(System.Collections.Specialized.NameValueCollection headers)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (headers == null) return result;
            foreach (string? key in headers.AllKeys)
            {
                if (string.IsNullOrEmpty(key)) continue;
                string lower = key.ToLowerInvariant();
                if (_RedactedHeaders.Contains(key) || lower.Contains("api-key") || lower.Contains("token") || lower.Contains("secret"))
                {
                    result[key] = "***REDACTED***";
                }
                else
                {
                    result[key] = headers[key] ?? string.Empty;
                }
            }
            return result;
        }

        private static void AssignBody(string body, int maxBytes, Action<string?, long, bool> assign)
        {
            if (string.IsNullOrEmpty(body))
            {
                assign(null, 0, false);
                return;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            if (maxBytes <= 0 || bytes.Length <= maxBytes)
            {
                assign(body, bytes.Length, false);
                return;
            }
            string prefix = Encoding.UTF8.GetString(bytes, 0, maxBytes);
            assign(prefix, bytes.Length, true);
        }

        private static string SafeRequestBody(HttpContextBase context)
        {
            try
            {
                return context.Request.DataAsString ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string SafeResponseBody(HttpContextBase context)
        {
            try
            {
                return context.Response.DataAsString ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        #endregion
    }
}

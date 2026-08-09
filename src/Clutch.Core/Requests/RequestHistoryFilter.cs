namespace Clutch.Core.Requests
{
    using System;

    /// <summary>
    /// Filter for listing and summarizing captured request history.
    /// </summary>
    public class RequestHistoryFilter
    {
        #region Public-Members

        /// <summary>
        /// Tenant scope. A system admin may leave null to span all tenants.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Optional user filter.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Optional exact HTTP method filter.
        /// </summary>
        public string? Method { get; set; } = null;

        /// <summary>
        /// Optional exact status code filter.
        /// </summary>
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// Optional case-insensitive substring match against the path.
        /// </summary>
        public string? PathContains { get; set; } = null;

        /// <summary>
        /// Optional inclusive lower bound on request time.
        /// </summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>
        /// Optional exclusive upper bound on request time.
        /// </summary>
        public DateTime? ToUtc { get; set; } = null;

        /// <summary>
        /// Bucket size in minutes for the summary call. Minimum 1, maximum 1440. Defaults to 15.
        /// </summary>
        public int BucketMinutes
        {
            get
            {
                return _BucketMinutes;
            }
            set
            {
                _BucketMinutes = Math.Clamp(value, 1, 1440);
            }
        }

        /// <summary>
        /// Page number, 1-based. Minimum 1. Defaults to 1.
        /// </summary>
        public int PageNumber
        {
            get
            {
                return _PageNumber;
            }
            set
            {
                _PageNumber = value < 1 ? 1 : value;
            }
        }

        /// <summary>
        /// Page size. Minimum 1, maximum 1000. Defaults to 25.
        /// </summary>
        public int PageSize
        {
            get
            {
                return _PageSize;
            }
            set
            {
                _PageSize = Math.Clamp(value, 1, 1000);
            }
        }

        #endregion

        #region Private-Members

        private int _BucketMinutes = 15;
        private int _PageNumber = 1;
        private int _PageSize = 25;

        #endregion
    }
}

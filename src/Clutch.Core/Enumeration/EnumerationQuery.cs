namespace Clutch.Core.Enumeration
{
    using System;

    /// <summary>
    /// Base pagination query for enumerating a collection. Uses skip/max-results offset pagination and a
    /// stable ordering. Domain-specific filters extend this class with their own fields.
    /// </summary>
    public class EnumerationQuery
    {
        #region Public-Members

        /// <summary>
        /// Maximum number of records to return in a single page. Clamped to 1..1000. Defaults to 25.
        /// </summary>
        public int MaxResults
        {
            get
            {
                return _MaxResults;
            }
            set
            {
                _MaxResults = Math.Clamp(value, 1, 1000);
            }
        }

        /// <summary>
        /// Number of records to skip before the page begins. Minimum 0. Defaults to 0.
        /// </summary>
        public int Skip
        {
            get
            {
                return _Skip;
            }
            set
            {
                _Skip = value < 0 ? 0 : value;
            }
        }

        /// <summary>
        /// Ordering applied before pagination. Defaults to newest first.
        /// </summary>
        public EnumerationOrderEnum Ordering { get; set; } = EnumerationOrderEnum.CreatedDescending;

        #endregion

        #region Private-Members

        private int _MaxResults = 25;
        private int _Skip = 0;

        #endregion
    }
}

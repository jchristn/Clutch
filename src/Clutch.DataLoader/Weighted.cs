namespace Clutch.DataLoader
{
    /// <summary>
    /// A candidate value paired with a selection weight, used by <see cref="RandomSource.PickWeighted{T}"/>.
    /// Replaces the value-tuple weighting pairs with a named type.
    /// </summary>
    /// <typeparam name="T">Type of the candidate value.</typeparam>
    public sealed class Weighted<T>
    {
        #region Public-Members

        /// <summary>
        /// The candidate value.
        /// </summary>
        public T Item { get; }

        /// <summary>
        /// The relative selection weight. Larger values are picked more often.
        /// </summary>
        public double Weight { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a weighted candidate.
        /// </summary>
        /// <param name="item">The candidate value.</param>
        /// <param name="weight">The relative selection weight.</param>
        public Weighted(T item, double weight)
        {
            Item = item;
            Weight = weight;
        }

        #endregion
    }
}

namespace Clutch.DataLoader
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Deterministic random helper: uniform draws, Gaussian and Poisson distributions, probability checks,
    /// and weighted selection. Seeded so a given <c>--seed</c> reproduces an identical dataset.
    /// </summary>
    public sealed class RandomSource
    {
        private readonly Random _Random;

        /// <summary>
        /// Instantiate with a seed.
        /// </summary>
        /// <param name="seed">Seed value.</param>
        public RandomSource(int seed)
        {
            _Random = new Random(seed);
        }

        /// <summary>Uniform double in [0,1).</summary>
        public double NextDouble()
        {
            return _Random.NextDouble();
        }

        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return _Random.Next(minInclusive, maxExclusive);
        }

        /// <summary>True with probability p.</summary>
        public bool Chance(double p)
        {
            return _Random.NextDouble() < p;
        }

        /// <summary>Clamp a value to a range.</summary>
        public static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>Gaussian (normal) draw via Box-Muller.</summary>
        public double Gaussian(double mean, double standardDeviation)
        {
            double u1 = 1.0 - _Random.NextDouble();
            double u2 = 1.0 - _Random.NextDouble();
            double normal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return mean + standardDeviation * normal;
        }

        /// <summary>Poisson draw (Knuth for small lambda, normal approximation for large).</summary>
        public int Poisson(double lambda)
        {
            if (lambda <= 0) return 0;
            if (lambda < 30)
            {
                double l = Math.Exp(-lambda);
                int k = 0;
                double p = 1.0;
                do
                {
                    k++;
                    p *= _Random.NextDouble();
                }
                while (p > l);
                return k - 1;
            }

            int approx = (int)Math.Round(Gaussian(lambda, Math.Sqrt(lambda)));
            return approx < 0 ? 0 : approx;
        }

        /// <summary>Pick a uniformly random element.</summary>
        public T Pick<T>(IReadOnlyList<T> items)
        {
            return items[_Random.Next(0, items.Count)];
        }

        /// <summary>Pick an element by weight.</summary>
        public T PickWeighted<T>(IReadOnlyList<(T Item, double Weight)> choices)
        {
            double total = 0;
            for (int i = 0; i < choices.Count; i++) total += choices[i].Weight;
            double roll = _Random.NextDouble() * total;
            double acc = 0;
            for (int i = 0; i < choices.Count; i++)
            {
                acc += choices[i].Weight;
                if (roll < acc) return choices[i].Item;
            }
            return choices[choices.Count - 1].Item;
        }
    }
}

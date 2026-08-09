namespace Clutch.Core.Services
{
    using System;
    using Clutch.Core.Enums;
    using Clutch.Core.Models;
    using Clutch.Core.Responses;

    /// <summary>
    /// Evaluates whether a requested lock mode may be granted given a key's policy and its current
    /// holders. Implements MRSW semantics plus a fully exclusive delete mode. Pure and side-effect free
    /// so it can be unit tested independently of storage.
    /// </summary>
    public static class LockCompatibilityEvaluator
    {
        #region Public-Methods

        /// <summary>
        /// Evaluate compatibility of a requested mode against the current holders under a definition's policy.
        /// </summary>
        /// <param name="definition">The lock definition (policy) for the key.</param>
        /// <param name="counts">Current active holder counts by mode.</param>
        /// <param name="requested">The requested mode.</param>
        /// <returns>The compatibility result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when definition or counts is null.</exception>
        public static CompatibilityResult Evaluate(LockDefinition definition, HolderCounts counts, LockModeEnum requested)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (counts == null) throw new ArgumentNullException(nameof(counts));

            // A held delete is fully exclusive and blocks everything.
            if (counts.Delete > 0)
            {
                return CompatibilityResult.Blocked("A delete lock is currently held on this key.");
            }

            switch (requested)
            {
                case LockModeEnum.Read:
                    return EvaluateRead(definition, counts);
                case LockModeEnum.Write:
                    return EvaluateWrite(definition, counts);
                case LockModeEnum.Delete:
                    return EvaluateDelete(counts);
                default:
                    return CompatibilityResult.Blocked("Unknown lock mode.");
            }
        }

        #endregion

        #region Private-Methods

        private static CompatibilityResult EvaluateRead(LockDefinition definition, HolderCounts counts)
        {
            // A held write blocks reads unless the policy allows concurrent reads during a write.
            if (counts.Write > 0 && definition.WriteBlocksReads)
            {
                return CompatibilityResult.Blocked("A write lock is currently held and blocks reads on this key.");
            }

            // Enforce the maximum concurrent reader count; -1 means unlimited.
            if (definition.ReadMaxHolders >= 0 && counts.Read >= definition.ReadMaxHolders)
            {
                return CompatibilityResult.Blocked("The maximum number of concurrent read holders is reached.");
            }

            return CompatibilityResult.Ok();
        }

        private static CompatibilityResult EvaluateWrite(LockDefinition definition, HolderCounts counts)
        {
            // A held read blocks a write unless the policy allows reads during a write.
            if (counts.Read > 0 && definition.WriteBlocksReads)
            {
                return CompatibilityResult.Blocked("Read locks are currently held and block writes on this key.");
            }

            int maxWriters = definition.WriteExclusivity == WriteExclusivityEnum.Shared
                ? definition.WriteMaxHolders
                : 1;

            if (counts.Write >= maxWriters)
            {
                return CompatibilityResult.Blocked("The maximum number of concurrent write holders is reached.");
            }

            return CompatibilityResult.Ok();
        }

        private static CompatibilityResult EvaluateDelete(HolderCounts counts)
        {
            // Delete requires the key to be completely free.
            if (counts.Total() > 0)
            {
                return CompatibilityResult.Blocked("A delete lock requires no other holders on this key.");
            }

            return CompatibilityResult.Ok();
        }

        #endregion
    }
}

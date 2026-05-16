using System;
using System.Collections.Generic;

namespace DriftRelics.Affixes;

public static class TierRoller
{
    public static TierConfig Roll(IReadOnlyList<TierConfig> tiers, Random rng)
    {
        if (tiers == null || tiers.Count == 0)
            throw new InvalidOperationException("TierRoller.Roll: tier list is empty");

        int total = 0;
        for (int i = 0; i < tiers.Count; i++) total += tiers[i].Weight;
        if (total <= 0)
            throw new InvalidOperationException("TierRoller.Roll: total tier weight is zero");

        int roll = rng.Next(total);
        int acc = 0;
        for (int i = 0; i < tiers.Count; i++)
        {
            acc += tiers[i].Weight;
            if (roll < acc) return tiers[i];
        }
        return tiers[tiers.Count - 1];
    }
}

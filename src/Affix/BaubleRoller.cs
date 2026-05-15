using System;
using System.Collections.Generic;
using Baubles.Api;

namespace Baubles.Affixes;

public static class BaubleRoller
{
    public static BaubleInstance Roll(BaubleSlotType slotType, long seed, AffixPool pool)
    {
        var rng = new Random(SeedToInt(seed));

        string? prefix = null;
        if (rng.NextDouble() < pool.RollChances.Prefix)
        {
            prefix = WeightedPick(pool.Prefixes, slotType, rng)?.Code;
        }

        string? suffix = null;
        if (rng.NextDouble() < pool.RollChances.Suffix)
        {
            suffix = WeightedPick(pool.Suffixes, slotType, rng)?.Code;
        }

        return new BaubleInstance(slotType, prefix, suffix, seed, Identified: false);
    }

    private static Affix? WeightedPick(IReadOnlyList<Affix> source,
                                       BaubleSlotType slot, Random rng)
    {
        int totalWeight = 0;
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i].Allows(slot)) totalWeight += source[i].Weight;
        }
        if (totalWeight <= 0) return null;

        int roll = rng.Next(totalWeight);
        int acc = 0;
        for (int i = 0; i < source.Count; i++)
        {
            var a = source[i];
            if (!a.Allows(slot)) continue;
            acc += a.Weight;
            if (roll < acc) return a;
        }
        return null;
    }

    private static int SeedToInt(long seed) => (int)(seed ^ (seed >>> 32));
}

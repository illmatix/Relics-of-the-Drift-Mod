using System;
using System.Collections.Generic;
using DriftRelics.Api;

namespace DriftRelics.Affixes;

public static class RelicRoller
{
    public static RelicInstance Roll(RelicSlotType slotType, long seed, AffixPool pool)
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

        return new RelicInstance(slotType, prefix, suffix, seed, Identified: false);
    }

    private static Affix? WeightedPick(IReadOnlyList<Affix> source,
                                       RelicSlotType slot, Random rng)
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

    // Fold a 64-bit seed into a 32-bit Random seed. The XOR fold can alias
    // distant seeds (e.g. seed=0L and seed=-1L both produce int seed 0; any
    // two seeds whose halves XOR to the same int collide). For the bauble
    // use case, seeds are derived from entity-id + GUID hashes which are
    // well-distributed in 64 bits, so the practical collision rate is
    // negligible. If you change this fold, all rolled affix combinations
    // for persisted seeds will change — that is a save-compatibility break.
    // Must match ScrambleNameGenerator.SeedToInt — both use the same seed
    // for the same stack so that name and affix line up.
    private static int SeedToInt(long seed) => (int)(seed ^ (seed >>> 32));
}

using System;
using System.Collections.Generic;
using DriftRelics.Api;

namespace DriftRelics.Affixes;

public static class RelicRoller
{
    public static RelicInstance Roll(RelicSlotType slotType, long seed, AffixPool pool, Random? rngOverride = null)
    {
        var rng = rngOverride ?? new Random(SeedToInt(seed));

        var tier = TierRoller.Roll(pool.Tiers, rng);

        string? prefix = null;
        string? suffix = null;

        if (tier.AffixCount >= 2)
        {
            prefix = WeightedPick(FilterByTier(pool.Prefixes, tier.Code), slotType, rng)?.Code;
            suffix = WeightedPick(FilterByTier(pool.Suffixes, tier.Code), slotType, rng)?.Code;
        }
        else if (tier.AffixCount == 1)
        {
            if (rng.Next(2) == 0)
            {
                prefix = WeightedPick(FilterByTier(pool.Prefixes, tier.Code), slotType, rng)?.Code;
            }
            else
            {
                suffix = WeightedPick(FilterByTier(pool.Suffixes, tier.Code), slotType, rng)?.Code;
            }
        }

        return new RelicInstance(slotType, prefix, suffix, seed, Identified: false, Tier: tier.Code);
    }

    private static IReadOnlyList<Affix> FilterByTier(IReadOnlyList<Affix> source, string tierCode)
    {
        var order = TierOrder(tierCode);
        var filtered = new List<Affix>(source.Count);
        for (int i = 0; i < source.Count; i++)
        {
            if (TierOrder(source[i].MinTier) <= order) filtered.Add(source[i]);
        }
        return filtered;
    }

    private static int TierOrder(string code) => code switch
    {
        "mundane"       => 0,
        "curious"       => 1,
        "notable"       => 2,
        "drift-touched" => 3,
        _               => 0
    };

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

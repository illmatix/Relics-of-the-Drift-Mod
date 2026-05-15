using System.Collections.Generic;

namespace DriftRelics.Affixes;

public sealed class AffixPool
{
    public AffixRollChances RollChances { get; }
    public IReadOnlyList<Affix> Prefixes { get; }
    public IReadOnlyList<Affix> Suffixes { get; }

    public AffixPool(AffixRollChances rollChances,
                     IReadOnlyList<Affix> prefixes,
                     IReadOnlyList<Affix> suffixes)
    {
        RollChances = rollChances;
        Prefixes = prefixes;
        Suffixes = suffixes;
    }
}

using System.Collections.Generic;

namespace DriftRelics.Affixes;

public sealed class AffixPool
{
    public IReadOnlyList<TierConfig> Tiers { get; }
    public IReadOnlyList<Affix> Prefixes { get; }
    public IReadOnlyList<Affix> Suffixes { get; }
    private readonly IReadOnlyDictionary<string, SignatureAffix> signatures;

    public AffixPool(IReadOnlyList<TierConfig> tiers,
                     IReadOnlyList<Affix> prefixes,
                     IReadOnlyList<Affix> suffixes,
                     IReadOnlyDictionary<string, SignatureAffix> signatures)
    {
        Tiers = tiers;
        Prefixes = prefixes;
        Suffixes = suffixes;
        this.signatures = signatures;
    }

    public SignatureAffix? GetSignatureFor(string slotTypeKey)
        => signatures.TryGetValue(slotTypeKey, out var s) ? s : null;

    public TierConfig? GetTier(string code)
    {
        for (int i = 0; i < Tiers.Count; i++) if (Tiers[i].Code == code) return Tiers[i];
        return null;
    }
}

using System.Collections.Generic;
using DriftRelics.Api;

namespace DriftRelics.Affixes;

public sealed class AffixRegistry : IAffixRegistry
{
    private readonly Dictionary<string, Affix> byCode = new();
    private readonly List<Affix> prefixes = new();
    private readonly List<Affix> suffixes = new();
    private List<TierConfig> tiers = new();
    private readonly Dictionary<string, SignatureAffix> signatures = new();

    public void Register(Affix affix)
    {
        if (string.IsNullOrEmpty(affix.Code))
            throw new System.ArgumentException("Affix.Code must be non-empty", nameof(affix));
        byCode[affix.Code] = affix;
        var list = affix.Kind == AffixKind.Prefix ? prefixes : suffixes;
        list.RemoveAll(a => a.Code == affix.Code);
        list.Add(affix);
    }

    public Affix? GetByCode(string code) => byCode.TryGetValue(code, out var a) ? a : null;

    public void SetTiers(IEnumerable<TierConfig> source)
    {
        tiers = new List<TierConfig>(source);
    }

    public void SetSignature(string slotTypeKey, SignatureAffix sig)
    {
        signatures[slotTypeKey] = sig;
    }

    public IReadOnlyList<TierConfig> Tiers => tiers;
    public SignatureAffix? GetSignatureFor(string slotTypeKey)
        => signatures.TryGetValue(slotTypeKey, out var s) ? s : null;

    public AffixPool BuildPool() => new(tiers, prefixes, suffixes, signatures);
}

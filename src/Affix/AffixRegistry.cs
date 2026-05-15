using System.Collections.Generic;
using Baubles.Api;

namespace Baubles.Affixes;

public sealed class AffixRegistry : IAffixRegistry
{
    private readonly Dictionary<string, Affix> byCode = new();
    private readonly List<Affix> prefixes = new();
    private readonly List<Affix> suffixes = new();

    public AffixRollChances RollChances { get; set; } = new();

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

    public AffixPool BuildPool() => new(RollChances, prefixes, suffixes);
}

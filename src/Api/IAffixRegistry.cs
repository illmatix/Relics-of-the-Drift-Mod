using Baubles.Affixes;

namespace Baubles.Api;

public interface IAffixRegistry
{
    void Register(Affix affix);
    Affix? GetByCode(string code);
    AffixPool BuildPool();
    AffixRollChances RollChances { get; set; }
}

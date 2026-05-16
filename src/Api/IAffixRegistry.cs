using DriftRelics.Affixes;
using System.Collections.Generic;

namespace DriftRelics.Api;

public interface IAffixRegistry
{
    void Register(Affix affix);
    Affix? GetByCode(string code);
    AffixPool BuildPool();
    IReadOnlyList<TierConfig> Tiers { get; }
    SignatureAffix? GetSignatureFor(string slotTypeKey);
    void SetTiers(IEnumerable<TierConfig> source);
    void SetSignature(string slotTypeKey, SignatureAffix sig);
}

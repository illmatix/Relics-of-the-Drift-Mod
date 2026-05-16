using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace DriftRelics.Affixes;

public static class AffixConfigLoader
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Converters = { new StringEnumConverter() },
        NullValueHandling = NullValueHandling.Ignore,
        MissingMemberHandling = MissingMemberHandling.Ignore
    };

    public static AffixConfig LoadFromJson(string json)
    {
        var cfg = JsonConvert.DeserializeObject<AffixConfig>(json, Settings)
                  ?? new AffixConfig();
        cfg.Tiers      ??= new List<TierConfig>();
        cfg.Signatures ??= new Dictionary<string, SignatureAffix>();
        cfg.Prefixes   ??= new List<Affix>();
        cfg.Suffixes   ??= new List<Affix>();

        foreach (var a in cfg.Prefixes) a.Kind = AffixKind.Prefix;
        foreach (var a in cfg.Suffixes) a.Kind = AffixKind.Suffix;

        return cfg;
    }
}

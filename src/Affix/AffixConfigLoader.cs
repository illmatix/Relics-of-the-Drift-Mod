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
        cfg.RollChances ??= new AffixRollChances();
        cfg.Prefixes ??= new System.Collections.Generic.List<Affix>();
        cfg.Suffixes ??= new System.Collections.Generic.List<Affix>();

        // Force Kind on entries — JSON authors shouldn't have to repeat it.
        foreach (var a in cfg.Prefixes) a.Kind = AffixKind.Prefix;
        foreach (var a in cfg.Suffixes) a.Kind = AffixKind.Suffix;

        return cfg;
    }
}

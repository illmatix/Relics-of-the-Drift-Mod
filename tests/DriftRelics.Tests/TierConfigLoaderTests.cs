using System.Linq;
using DriftRelics.Affixes;
using Xunit;

namespace DriftRelics.Tests;

public class TierConfigLoaderTests
{
    private const string Json = @"{
      ""tiers"": [
        { ""code"": ""mundane"",       ""weight"": 50, ""color"": ""#aaaaaa"", ""affixCount"": 1, ""valueScale"": 1.0 },
        { ""code"": ""drift-touched"", ""weight"":  5, ""color"": ""#a855f7"", ""affixCount"": 2, ""valueScale"": 1.6, ""signature"": true }
      ],
      ""signatures"": {
        ""ring"": { ""code"": ""drift_mark"", ""langKey"": ""driftrelics:signature-drift_mark"",
                    ""mods"": [{ ""key"": ""meleeDamage"", ""value"": 0.10, ""op"": ""Mul"" }] }
      },
      ""prefixes"": [
        { ""code"": ""burning"",      ""langKey"": ""x"", ""weight"": 10, ""mods"": [] },
        { ""code"": ""drift_marked"", ""langKey"": ""x"", ""weight"": 5, ""minTier"": ""drift-touched"", ""mods"": [] }
      ],
      ""suffixes"": []
    }";

    [Fact]
    public void Loads_Tiers_With_Weights_And_Flags()
    {
        var cfg = AffixConfigLoader.LoadFromJson(Json);
        Assert.Equal(2, cfg.Tiers.Count);
        var drift = cfg.Tiers.Single(t => t.Code == "drift-touched");
        Assert.Equal(5, drift.Weight);
        Assert.Equal(2, drift.AffixCount);
        Assert.Equal(1.6, drift.ValueScale);
        Assert.True(drift.Signature);
    }

    [Fact]
    public void Loads_Signature_For_Slot_Type()
    {
        var cfg = AffixConfigLoader.LoadFromJson(Json);
        Assert.True(cfg.Signatures.ContainsKey("ring"));
        var sig = cfg.Signatures["ring"];
        Assert.Equal("drift_mark", sig.Code);
        Assert.Single(sig.Mods);
        Assert.Equal("meleeDamage", sig.Mods[0].Key);
    }

    [Fact]
    public void Affixes_Inherit_MinTier_From_Json_Default_Mundane()
    {
        var cfg = AffixConfigLoader.LoadFromJson(Json);
        var burning      = cfg.Prefixes.Single(a => a.Code == "burning");
        var driftMarked  = cfg.Prefixes.Single(a => a.Code == "drift_marked");

        Assert.Equal("mundane", burning.MinTier);
        Assert.Equal("drift-touched", driftMarked.MinTier);
    }

    [Fact]
    public void Missing_Tiers_Section_Yields_Empty_List()
    {
        var cfg = AffixConfigLoader.LoadFromJson(@"{ ""prefixes"": [], ""suffixes"": [] }");
        Assert.NotNull(cfg.Tiers);
        Assert.Empty(cfg.Tiers);
        Assert.NotNull(cfg.Signatures);
        Assert.Empty(cfg.Signatures);
    }
}

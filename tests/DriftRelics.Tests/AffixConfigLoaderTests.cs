using DriftRelics.Affixes;
using DriftRelics.Modifier;
using Xunit;

namespace DriftRelics.Tests;

public class AffixConfigLoaderTests
{
    private const string SampleJson = @"{
      ""rollChances"": { ""prefix"": 0.8, ""suffix"": 0.6 },
      ""prefixes"": [
        { ""code"": ""burning"",  ""langKey"": ""driftrelics:affix-prefix-burning"",
          ""kind"": ""Prefix"", ""weight"": 10,
          ""mods"": [
            { ""key"": ""heatResist"",  ""value"": 2 },
            { ""key"": ""meleeDamage"", ""value"": 0.05, ""op"": ""Mul"" }
          ]}
      ],
      ""suffixes"": [
        { ""code"": ""of_swiftness"", ""langKey"": ""driftrelics:affix-suffix-of_swiftness"",
          ""kind"": ""Suffix"", ""weight"": 10,
          ""mods"": [ { ""key"": ""moveSpeed"", ""value"": 0.05, ""op"": ""Mul"" } ] }
      ]
    }";

    [Fact]
    public void Loads_RollChances()
    {
        var cfg = AffixConfigLoader.LoadFromJson(SampleJson);
        Assert.Equal(0.8, cfg.RollChances.Prefix);
        Assert.Equal(0.6, cfg.RollChances.Suffix);
    }

    [Fact]
    public void Loads_Prefixes_And_Suffixes()
    {
        var cfg = AffixConfigLoader.LoadFromJson(SampleJson);
        Assert.Single(cfg.Prefixes);
        Assert.Single(cfg.Suffixes);

        var burning = cfg.Prefixes[0];
        Assert.Equal("burning", burning.Code);
        Assert.Equal(AffixKind.Prefix, burning.Kind);
        Assert.Equal(10, burning.Weight);
        Assert.Equal(2, burning.Mods.Count);
        Assert.Equal("heatResist", burning.Mods[0].Key);
        Assert.Equal(2.0, burning.Mods[0].Value);
        Assert.Equal(ModifierOp.Add, burning.Mods[0].Op);
        Assert.Equal(ModifierOp.Mul, burning.Mods[1].Op);
    }

    [Fact]
    public void Fills_Default_When_Field_Missing()
    {
        var cfg = AffixConfigLoader.LoadFromJson("{}");
        Assert.NotNull(cfg.RollChances);
        Assert.Equal(0.75, cfg.RollChances.Prefix);
        Assert.Equal(0.75, cfg.RollChances.Suffix);
        Assert.Empty(cfg.Prefixes);
        Assert.Empty(cfg.Suffixes);
    }
}

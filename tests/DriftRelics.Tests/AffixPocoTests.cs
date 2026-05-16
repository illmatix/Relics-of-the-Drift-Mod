using DriftRelics.Affixes;
using DriftRelics.Api;
using DriftRelics.Modifier;
using Xunit;

namespace DriftRelics.Tests;

public class AffixPocoTests
{
    [Fact]
    public void Affix_Defaults_Are_Sensible()
    {
        var affix = new Affix
        {
            Code = "burning",
            LangKey = "driftrelics:affix-prefix-burning",
            Kind = AffixKind.Prefix
        };

        Assert.Equal(10, affix.Weight);
        Assert.Null(affix.AllowedSlots);
        Assert.NotNull(affix.Mods);
        Assert.Empty(affix.Mods);
    }

    [Fact]
    public void ModifierEntry_Defaults_To_Add()
    {
        var mod = new ModifierEntry { Key = "maxHealth", Value = 4 };
        Assert.Equal(ModifierOp.Add, mod.Op);
    }

    [Fact]
    public void Affix_Filters_By_Slot_When_AllowedSlots_Set()
    {
        var affix = new Affix
        {
            Code = "of_warding",
            Kind = AffixKind.Suffix,
            AllowedSlots = new[] { RelicSlotType.Trinket }
        };

        Assert.True(affix.Allows(RelicSlotType.Trinket));
        Assert.False(affix.Allows(RelicSlotType.Ring));
    }

    [Fact]
    public void Affix_Allows_All_When_AllowedSlots_Null()
    {
        var affix = new Affix { Code = "burning", Kind = AffixKind.Prefix };

        Assert.True(affix.Allows(RelicSlotType.Ring));
        Assert.True(affix.Allows(RelicSlotType.Bracelet));
        Assert.True(affix.Allows(RelicSlotType.Trinket));
    }

    [Fact]
    public void RelicInstance_DefaultTier_Is_Mundane()
    {
        var instance = new RelicInstance(RelicSlotType.Ring, null, null, 42L, Identified: false);
        Assert.Equal("mundane", instance.Tier);
    }

    [Fact]
    public void RelicInstance_Carries_Explicit_Tier()
    {
        var instance = new RelicInstance(RelicSlotType.Ring, "burning", null, 42L, Identified: true, Tier: "drift-touched");
        Assert.Equal("drift-touched", instance.Tier);
    }

    [Fact]
    public void Affix_DefaultMinTier_Is_Mundane()
    {
        var affix = new Affix { Code = "burning", Kind = AffixKind.Prefix };
        Assert.Equal("mundane", affix.MinTier);
    }

    [Fact]
    public void TierConfig_Defaults_Are_Sensible()
    {
        var t = new TierConfig { Code = "mundane" };
        Assert.Equal(50, t.Weight);
        Assert.Equal("#aaaaaa", t.Color);
        Assert.Equal(1, t.AffixCount);
        Assert.Equal(1.0, t.ValueScale);
        Assert.False(t.Signature);
    }

    [Fact]
    public void SignatureAffix_Defaults_Are_Sensible()
    {
        var s = new SignatureAffix();
        Assert.NotNull(s.Mods);
        Assert.Empty(s.Mods);
    }

    [Fact]
    public void AffixRegistry_Stores_Tiers_And_Signatures()
    {
        var reg = new AffixRegistry();
        reg.SetTiers(new System.Collections.Generic.List<TierConfig>
        {
            new() { Code = "mundane", Weight = 50 },
            new() { Code = "drift-touched", Weight = 5, Signature = true }
        });
        reg.SetSignature("ring", new SignatureAffix { Code = "drift_mark" });

        var pool = reg.BuildPool();
        Assert.Equal(2, pool.Tiers.Count);
        Assert.NotNull(pool.GetSignatureFor("ring"));
        Assert.Null(pool.GetSignatureFor("trinket"));
    }
}

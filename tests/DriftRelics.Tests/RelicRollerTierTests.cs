using System.Collections.Generic;
using DriftRelics.Affixes;
using DriftRelics.Api;
using DriftRelics.Modifier;
using Xunit;

namespace DriftRelics.Tests;

public class RelicRollerTierTests
{
    private static AffixPool BuildPool(params TierConfig[] tiers)
    {
        var prefixes = new List<Affix>
        {
            new() { Code = "burning",      Kind = AffixKind.Prefix, Weight = 10, MinTier = "mundane" },
            new() { Code = "drift_marked", Kind = AffixKind.Prefix, Weight = 10, MinTier = "drift-touched" },
        };
        var suffixes = new List<Affix>
        {
            new() { Code = "of_swiftness", Kind = AffixKind.Suffix, Weight = 10, MinTier = "mundane" },
        };
        var signatures = new Dictionary<string, SignatureAffix>
        {
            ["ring"] = new() { Code = "drift_mark",
                               Mods = { new ModifierEntry { Key = "meleeDamage", Value = 0.10, Op = ModifierOp.Mul } } }
        };
        return new AffixPool(tiers, prefixes, suffixes, signatures);
    }

    [Fact]
    public void Mundane_Roll_Has_Exactly_One_Affix()
    {
        var pool = BuildPool(new TierConfig { Code = "mundane", Weight = 100, AffixCount = 1, ValueScale = 1.0 });
        var rng = new System.Random(123);
        int withBoth = 0, withOne = 0, withNeither = 0;
        for (int i = 0; i < 200; i++)
        {
            var inst = RelicRoller.Roll(RelicSlotType.Ring, i, pool, rng);
            int affixCount = (inst.PrefixCode != null ? 1 : 0) + (inst.SuffixCode != null ? 1 : 0);
            if (affixCount == 2) withBoth++;
            else if (affixCount == 1) withOne++;
            else withNeither++;
            Assert.Equal("mundane", inst.Tier);
        }
        Assert.Equal(0, withBoth);
        Assert.Equal(0, withNeither);
        Assert.Equal(200, withOne);
    }

    [Fact]
    public void Curious_Roll_Has_Prefix_And_Suffix()
    {
        var pool = BuildPool(new TierConfig { Code = "curious", Weight = 100, AffixCount = 2, ValueScale = 1.0 });
        var inst = RelicRoller.Roll(RelicSlotType.Ring, 42, pool, new System.Random(0));
        Assert.NotNull(inst.PrefixCode);
        Assert.NotNull(inst.SuffixCode);
        Assert.Equal("curious", inst.Tier);
    }

    [Fact]
    public void Drift_Touched_Filters_To_Drift_Marked_Available()
    {
        var pool = BuildPool(new TierConfig
        {
            Code = "drift-touched", Weight = 100, AffixCount = 2, ValueScale = 1.6, Signature = true
        });
        var rng = new System.Random(7);
        int driftMarkedSeen = 0;
        for (int i = 0; i < 50; i++)
        {
            var inst = RelicRoller.Roll(RelicSlotType.Ring, i, pool, rng);
            if (inst.PrefixCode == "drift_marked") driftMarkedSeen++;
        }
        Assert.InRange(driftMarkedSeen, 15, 35);
    }

    [Fact]
    public void Filter_Excludes_Drift_Marked_At_Mundane()
    {
        var pool = BuildPool(new TierConfig { Code = "mundane", Weight = 100, AffixCount = 1, ValueScale = 1.0 });
        var rng = new System.Random(9);
        for (int i = 0; i < 200; i++)
        {
            var inst = RelicRoller.Roll(RelicSlotType.Ring, i, pool, rng);
            Assert.NotEqual("drift_marked", inst.PrefixCode);
        }
    }

    // --- pre-existing coverage preserved from the deleted BaubleRollerTests.cs ---

    [Fact]
    public void Roll_Is_Deterministic_For_Seed()
    {
        var pool = BuildPool(new TierConfig { Code = "curious", Weight = 100, AffixCount = 2, ValueScale = 1.0 });
        var a = RelicRoller.Roll(RelicSlotType.Ring, 42L, pool);
        var b = RelicRoller.Roll(RelicSlotType.Ring, 42L, pool);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Roll_Respects_AllowedSlots()
    {
        var prefixes = new List<Affix>
        {
            new() { Code = "ring_only", Kind = AffixKind.Prefix, Weight = 1,
                    AllowedSlots = new[] { RelicSlotType.Ring } }
        };
        var suffixes = new List<Affix>
        {
            new() { Code = "trinket_only", Kind = AffixKind.Suffix, Weight = 1,
                    AllowedSlots = new[] { RelicSlotType.Trinket } }
        };
        var pool = new AffixPool(
            new List<TierConfig> { new() { Code = "curious", Weight = 100, AffixCount = 2, ValueScale = 1.0 } },
            prefixes, suffixes,
            new Dictionary<string, SignatureAffix>());

        var ringRoll = RelicRoller.Roll(RelicSlotType.Ring, 1L, pool);
        Assert.Equal("ring_only", ringRoll.PrefixCode);
        Assert.Null(ringRoll.SuffixCode);   // trinket_only filtered out

        var trinketRoll = RelicRoller.Roll(RelicSlotType.Trinket, 1L, pool);
        Assert.Null(trinketRoll.PrefixCode);
        Assert.Equal("trinket_only", trinketRoll.SuffixCode);
    }

    [Fact]
    public void Roll_Result_Is_Unidentified()
    {
        var pool = BuildPool(new TierConfig { Code = "mundane", Weight = 100, AffixCount = 1, ValueScale = 1.0 });
        var roll = RelicRoller.Roll(RelicSlotType.Bracelet, 7L, pool);
        Assert.False(roll.Identified);
        Assert.Equal(7L, roll.Seed);
        Assert.Equal(RelicSlotType.Bracelet, roll.SlotType);
    }
}

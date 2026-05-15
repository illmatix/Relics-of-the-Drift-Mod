using System.Collections.Generic;
using System.Linq;
using Baubles.Affixes;
using Baubles.Api;
using Xunit;

namespace Baubles.Tests;

public class BaubleRollerTests
{
    private static AffixPool MakePool() => new AffixPool(
        new AffixRollChances { Prefix = 1.0, Suffix = 1.0 },
        new List<Affix>
        {
            new() { Code = "burning",  Kind = AffixKind.Prefix, Weight = 1 },
            new() { Code = "hardened", Kind = AffixKind.Prefix, Weight = 1 },
        },
        new List<Affix>
        {
            new() { Code = "of_swiftness", Kind = AffixKind.Suffix, Weight = 1 },
            new() { Code = "of_the_bear",  Kind = AffixKind.Suffix, Weight = 1 },
        });

    [Fact]
    public void Roll_Is_Deterministic_For_Seed()
    {
        var pool = MakePool();
        var a = BaubleRoller.Roll(BaubleSlotType.Ring, 42L, pool);
        var b = BaubleRoller.Roll(BaubleSlotType.Ring, 42L, pool);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Roll_With_RollChance_Zero_Yields_No_Affixes()
    {
        var pool = new AffixPool(
            new AffixRollChances { Prefix = 0.0, Suffix = 0.0 },
            MakePool().Prefixes, MakePool().Suffixes);

        var roll = BaubleRoller.Roll(BaubleSlotType.Ring, 1L, pool);
        Assert.Null(roll.PrefixCode);
        Assert.Null(roll.SuffixCode);
    }

    [Fact]
    public void Roll_Respects_AllowedSlots()
    {
        var pool = new AffixPool(
            new AffixRollChances { Prefix = 1.0, Suffix = 1.0 },
            new List<Affix>
            {
                new() { Code = "ring_only", Kind = AffixKind.Prefix, Weight = 1,
                        AllowedSlots = new[] { BaubleSlotType.Ring } }
            },
            new List<Affix>
            {
                new() { Code = "trinket_only", Kind = AffixKind.Suffix, Weight = 1,
                        AllowedSlots = new[] { BaubleSlotType.Trinket } }
            });

        var ringRoll = BaubleRoller.Roll(BaubleSlotType.Ring, 1L, pool);
        Assert.Equal("ring_only", ringRoll.PrefixCode);
        Assert.Null(ringRoll.SuffixCode);   // trinket_only filtered out

        var trinketRoll = BaubleRoller.Roll(BaubleSlotType.Trinket, 1L, pool);
        Assert.Null(trinketRoll.PrefixCode);
        Assert.Equal("trinket_only", trinketRoll.SuffixCode);
    }

    [Fact]
    public void Roll_Statistical_Weight_Bias()
    {
        var pool = new AffixPool(
            new AffixRollChances { Prefix = 1.0, Suffix = 0.0 },
            new List<Affix>
            {
                new() { Code = "common", Kind = AffixKind.Prefix, Weight = 9 },
                new() { Code = "rare",   Kind = AffixKind.Prefix, Weight = 1 }
            },
            new List<Affix>());

        int common = 0, rare = 0;
        for (long s = 1; s <= 10_000; s++)
        {
            var r = BaubleRoller.Roll(BaubleSlotType.Ring, s, pool);
            if (r.PrefixCode == "common") common++;
            else if (r.PrefixCode == "rare") rare++;
        }

        // With weights 9:1 expect ~9000:1000. Allow wide slack.
        Assert.InRange(common, 8500, 9500);
        Assert.InRange(rare,    500, 1500);
    }

    [Fact]
    public void Roll_Result_Is_Unidentified()
    {
        var pool = MakePool();
        var roll = BaubleRoller.Roll(BaubleSlotType.Bracelet, 7L, pool);
        Assert.False(roll.Identified);
        Assert.Equal(7L, roll.Seed);
        Assert.Equal(BaubleSlotType.Bracelet, roll.SlotType);
    }
}

using System;
using System.Collections.Generic;
using DriftRelics.Affixes;
using Xunit;

namespace DriftRelics.Tests;

public class TierRollerTests
{
    private static List<TierConfig> SampleTiers() => new()
    {
        new() { Code = "mundane",       Weight = 50 },
        new() { Code = "curious",       Weight = 30 },
        new() { Code = "notable",       Weight = 15 },
        new() { Code = "drift-touched", Weight =  5 },
    };

    [Fact]
    public void Roll_Distribution_Roughly_Matches_Weights()
    {
        var tiers = SampleTiers();
        var counts = new Dictionary<string, int>
        {
            ["mundane"] = 0, ["curious"] = 0, ["notable"] = 0, ["drift-touched"] = 0
        };

        var rng = new Random(12345);
        const int trials = 20000;
        for (int i = 0; i < trials; i++)
        {
            var tier = TierRoller.Roll(tiers, rng);
            counts[tier.Code]++;
        }

        // Expected: mundane=50%, curious=30%, notable=15%, drift-touched=5%
        // Allow 3 percentage points slack.
        Assert.InRange(counts["mundane"]       / (double)trials, 0.47, 0.53);
        Assert.InRange(counts["curious"]       / (double)trials, 0.27, 0.33);
        Assert.InRange(counts["notable"]       / (double)trials, 0.12, 0.18);
        Assert.InRange(counts["drift-touched"] / (double)trials, 0.03, 0.07);
    }

    [Fact]
    public void Roll_Returns_Single_Tier_When_Pool_Has_One()
    {
        var tiers = new List<TierConfig> { new() { Code = "mundane", Weight = 1 } };
        var tier = TierRoller.Roll(tiers, new Random(0));
        Assert.Equal("mundane", tier.Code);
    }

    [Fact]
    public void Roll_Empty_Pool_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => TierRoller.Roll(new List<TierConfig>(), new Random(0)));
    }

    [Fact]
    public void Roll_Zero_Weights_Throws()
    {
        var tiers = new List<TierConfig> { new() { Code = "x", Weight = 0 } };
        Assert.Throws<InvalidOperationException>(() => TierRoller.Roll(tiers, new Random(0)));
    }
}

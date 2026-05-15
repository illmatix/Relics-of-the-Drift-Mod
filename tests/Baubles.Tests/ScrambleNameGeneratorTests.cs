using Baubles.Affixes;
using Xunit;

namespace Baubles.Tests;

public class ScrambleNameGeneratorTests
{
    [Fact]
    public void SameSeed_SameOutput()
    {
        var a = ScrambleNameGenerator.Generate(12345L);
        var b = ScrambleNameGenerator.Generate(12345L);
        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentOutputs_For_Sample()
    {
        var seen = new System.Collections.Generic.HashSet<string>();
        for (long s = 1; s <= 100; s++)
        {
            seen.Add(ScrambleNameGenerator.Generate(s));
        }
        Assert.True(seen.Count > 80,
            $"expected >80 distinct names from 100 seeds, got {seen.Count}");
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(7L)]
    [InlineData(42L)]
    [InlineData(999999L)]
    [InlineData(-1L)]
    [InlineData(long.MaxValue)]
    public void Output_Starts_Capitalised(long seed)
    {
        var name = ScrambleNameGenerator.Generate(seed);
        Assert.True(char.IsUpper(name[0]),
            $"first char of '{name}' (seed {seed}) should be uppercase");

        // If the connector " of " was inserted, the post-space char must
        // also be uppercase.
        var spaceIdx = name.IndexOf(' ');
        if (spaceIdx > 0)
        {
            var afterSpace = name[name.LastIndexOf(' ') + 1];
            Assert.True(char.IsUpper(afterSpace),
                $"char after last space in '{name}' (seed {seed}) should be uppercase");
        }
    }

    [Fact]
    public void Output_Is_Reasonably_Sized()
    {
        for (long s = 1; s <= 50; s++)
        {
            var name = ScrambleNameGenerator.Generate(s);
            Assert.InRange(name.Length, 8, 40);
        }
    }
}

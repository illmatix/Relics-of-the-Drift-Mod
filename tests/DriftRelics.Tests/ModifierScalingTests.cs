using DriftRelics.Modifier;
using Xunit;

namespace DriftRelics.Tests;

public class ModifierScalingTests
{
    [Theory]
    [InlineData(0.10, 1.6, 0.16)]
    [InlineData(0.05, 1.3, 0.065)]
    [InlineData(0.05, 1.0, 0.05)]
    public void Scale_Mul_Value_Is_Product(double v, double scale, double expected)
    {
        var entry = new ModifierEntry { Key = "k", Value = v, Op = ModifierOp.Mul };
        var scaled = ModifierScaling.Scale(entry, scale);
        Assert.Equal(expected, scaled.Value, 6);
    }

    [Theory]
    [InlineData(2, 1.6, 3)]   // round-half-up: 2 * 1.6 = 3.2 → 3
    [InlineData(5, 1.6, 8)]   // 5 * 1.6 = 8.0
    [InlineData(4, 1.3, 5)]   // 4 * 1.3 = 5.2 → 5
    public void Scale_Add_Value_Rounds_Half_Up_For_Integer_Mods(int v, double scale, int expected)
    {
        var entry = new ModifierEntry { Key = "maxHealth", Value = v, Op = ModifierOp.Add };
        var scaled = ModifierScaling.Scale(entry, scale);
        Assert.Equal(expected, (int)scaled.Value);
    }
}

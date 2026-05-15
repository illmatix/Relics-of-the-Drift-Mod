using DriftRelics.Api;
using Xunit;

namespace DriftRelics.Tests;

public class RelicInstanceTests
{
    [Fact]
    public void RelicInstance_Equality_Is_Value_Based()
    {
        var a = new RelicInstance(RelicSlotType.Ring, "burning", "of_swiftness", 42L, true);
        var b = new RelicInstance(RelicSlotType.Ring, "burning", "of_swiftness", 42L, true);

        Assert.Equal(a, b);
    }

    [Fact]
    public void RelicSlotType_Has_Expected_Members()
    {
        Assert.Equal(0, (int)RelicSlotType.Ring);
        Assert.Equal(1, (int)RelicSlotType.Bracelet);
        Assert.Equal(2, (int)RelicSlotType.Trinket);
    }
}

using Baubles.Api;
using Xunit;

namespace Baubles.Tests;

public class BaubleInstanceTests
{
    [Fact]
    public void BaubleInstance_Equality_Is_Value_Based()
    {
        var a = new BaubleInstance(BaubleSlotType.Ring, "burning", "of_swiftness", 42L, true);
        var b = new BaubleInstance(BaubleSlotType.Ring, "burning", "of_swiftness", 42L, true);

        Assert.Equal(a, b);
    }

    [Fact]
    public void BaubleSlotType_Has_Expected_Members()
    {
        Assert.Equal(0, (int)BaubleSlotType.Ring);
        Assert.Equal(1, (int)BaubleSlotType.Bracelet);
        Assert.Equal(2, (int)BaubleSlotType.Trinket);
    }
}

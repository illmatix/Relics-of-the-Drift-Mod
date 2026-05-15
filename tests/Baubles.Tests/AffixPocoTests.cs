using Baubles.Affixes;
using Baubles.Api;
using Baubles.Modifier;
using Xunit;

namespace Baubles.Tests;

public class AffixPocoTests
{
    [Fact]
    public void Affix_Defaults_Are_Sensible()
    {
        var affix = new Affix
        {
            Code = "burning",
            LangKey = "baubles:affix-prefix-burning",
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
            AllowedSlots = new[] { BaubleSlotType.Trinket }
        };

        Assert.True(affix.Allows(BaubleSlotType.Trinket));
        Assert.False(affix.Allows(BaubleSlotType.Ring));
    }

    [Fact]
    public void Affix_Allows_All_When_AllowedSlots_Null()
    {
        var affix = new Affix { Code = "burning", Kind = AffixKind.Prefix };

        Assert.True(affix.Allows(BaubleSlotType.Ring));
        Assert.True(affix.Allows(BaubleSlotType.Bracelet));
        Assert.True(affix.Allows(BaubleSlotType.Trinket));
    }
}

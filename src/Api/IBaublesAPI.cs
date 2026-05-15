using System;
using Baubles.Inventory;
using Vintagestory.API.Common;

namespace Baubles.Api;

public interface IBaublesAPI
{
    InventoryBaubles? GetBaubles(EntityPlayer player);
    bool IsBauble(ItemStack? stack);
    BaubleSlotType? GetSlotType(ItemStack? stack);
    bool IsIdentified(ItemStack? stack);
    BaubleInstance? GetInstance(ItemStack? stack);
    string GetDisplayName(ItemStack stack);

    IAffixRegistry Affixes { get; }
    IModifierRegistry Modifiers { get; }

    ItemStack? RollUnidentifiedBauble(BaubleSlotType slotType, long seed);
    void Identify(ItemStack stack);

    event Action<EntityPlayer, ItemStack, BaubleSlotType> OnBaubleEquipped;
    event Action<EntityPlayer, ItemStack, BaubleSlotType> OnBaubleUnequipped;
    event Action<EntityPlayer, ItemStack> OnBaubleIdentified;
}

using System;
using DriftRelics.Inventory;
using Vintagestory.API.Common;

namespace DriftRelics.Api;

public interface IRelicsAPI
{
    InventoryRelics? GetRelics(EntityPlayer player);
    bool IsRelic(ItemStack? stack);
    RelicSlotType? GetSlotType(ItemStack? stack);
    bool IsIdentified(ItemStack? stack);
    RelicInstance? GetInstance(ItemStack? stack);
    string GetDisplayName(ItemStack stack);

    IAffixRegistry Affixes { get; }
    IModifierRegistry Modifiers { get; }

    ItemStack? RollUnidentifiedRelic(RelicSlotType slotType, long seed);
    void Identify(ItemStack stack);

    event Action<EntityPlayer, ItemStack, RelicSlotType> OnRelicEquipped;
    event Action<EntityPlayer, ItemStack, RelicSlotType> OnRelicUnequipped;
    event Action<EntityPlayer, ItemStack> OnRelicIdentified;
}

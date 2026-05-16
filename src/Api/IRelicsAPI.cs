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
    string GetTier(ItemStack? stack);

    IAffixRegistry Affixes { get; }
    IModifierRegistry Modifiers { get; }
    System.Collections.Generic.IReadOnlyList<DriftRelics.Affixes.TierConfig> Tiers { get; }

    ItemStack? RollUnidentifiedRelic(RelicSlotType slotType, long seed);
    void Identify(ItemStack stack);

    event Action<EntityPlayer, ItemStack, RelicSlotType> OnRelicEquipped;
    event Action<EntityPlayer, ItemStack, RelicSlotType> OnRelicUnequipped;
    event Action<EntityPlayer, ItemStack> OnRelicIdentified;
}

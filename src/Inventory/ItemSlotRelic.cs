using DriftRelics.Api;
using Vintagestory.API.Common;

namespace DriftRelics.Inventory;

public class ItemSlotRelic : ItemSlot
{
    public RelicSlotType AllowedSlotType { get; }

    public ItemSlotRelic(InventoryBase inventory, RelicSlotType allowedSlotType)
        : base(inventory)
    {
        AllowedSlotType = allowedSlotType;
        MaxSlotStackSize = 1;
        BackgroundIcon = allowedSlotType.ToString().ToLowerInvariant();
    }

    public override bool CanHold(ItemSlot sourceSlot)
    {
        var stack = sourceSlot?.Itemstack;
        if (stack == null) return false;
        if (!RelicsUtil.IsIdentified(stack)) return false;
        var slotType = RelicsUtil.GetSlotType(stack);
        return slotType == AllowedSlotType;
    }

    public override bool CanTakeFrom(ItemSlot sourceSlot,
                                     EnumMergePriority priority = EnumMergePriority.AutoMerge)
    {
        var stack = sourceSlot?.Itemstack;
        if (stack == null) return false;
        if (!RelicsUtil.IsIdentified(stack)) return false;
        var slotType = RelicsUtil.GetSlotType(stack);
        return slotType == AllowedSlotType
            && base.CanTakeFrom(sourceSlot, priority);
    }
}

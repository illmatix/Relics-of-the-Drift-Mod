using Baubles.Api;
using Vintagestory.API.Common;

namespace Baubles.Inventory;

public class ItemSlotBauble : ItemSlot
{
    public BaubleSlotType AllowedSlotType { get; }

    public ItemSlotBauble(InventoryBase inventory, BaubleSlotType allowedSlotType)
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
        var slotType = BaublesUtil.GetSlotType(stack);
        return slotType == AllowedSlotType;
    }

    public override bool CanTakeFrom(ItemSlot sourceSlot,
                                     EnumMergePriority priority = EnumMergePriority.AutoMerge)
    {
        if (sourceSlot?.Itemstack == null) return false;
        var slotType = BaublesUtil.GetSlotType(sourceSlot.Itemstack);
        return slotType == AllowedSlotType
            && base.CanTakeFrom(sourceSlot, priority);
    }
}

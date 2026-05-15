using System;
using System.Text;
using Baubles.Api;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Baubles.Items;

public class ItemBauble : Item, IBaubleItem
{
    public BaubleSlotType SlotType
    {
        get
        {
            var raw = Attributes?["bauble"]?["slotType"]?.AsString("Trinket") ?? "Trinket";
            return Enum.TryParse<BaubleSlotType>(raw, ignoreCase: true, out var t)
                ? t
                : BaubleSlotType.Trinket;
        }
    }

    public override string GetHeldItemName(ItemStack itemStack)
        => BaublesDisplay.GetDisplayName(itemStack, base.GetHeldItemName(itemStack));

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc,
                                         IWorldAccessor world, bool withDebugInfo)
    {
        var stack = inSlot.Itemstack;
        if (BaublesUtil.IsBauble(stack) && !BaublesUtil.IsIdentified(stack))
        {
            dsc.AppendLine(Lang.Get("baubles:unidentified-hint"));
        }
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
    }
}

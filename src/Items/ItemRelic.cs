using System;
using System.Text;
using DriftRelics.Api;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace DriftRelics.Items;

public class ItemRelic : Item, IRelicItem
{
    public RelicSlotType SlotType
    {
        get
        {
            var raw = Attributes?["relic"]?["slotType"]?.AsString("Trinket") ?? "Trinket";
            return Enum.TryParse<RelicSlotType>(raw, ignoreCase: true, out var t)
                ? t
                : RelicSlotType.Trinket;
        }
    }

    public override string GetHeldItemName(ItemStack itemStack)
        => RelicsDisplay.GetDisplayName(itemStack, base.GetHeldItemName(itemStack));

    public override void GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc,
                                         IWorldAccessor world, bool withDebugInfo)
    {
        var stack = inSlot.Itemstack;
        if (RelicsUtil.IsRelic(stack) && !RelicsUtil.IsIdentified(stack))
        {
            dsc.AppendLine(Lang.Get("driftrelics:unidentified-hint"));
        }
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
    }
}

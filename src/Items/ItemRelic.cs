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
        if (RelicsUtil.IsRelic(stack))
        {
            if (!RelicsUtil.IsIdentified(stack))
            {
                var tier = RelicsUtil.GetTier(stack);
                if (tier != "mundane")
                {
                    var tierName = Lang.Get("driftrelics:tier-" + tier);
                    dsc.AppendLine(Lang.Get("driftrelics:aura-line", tierName));
                }
                dsc.AppendLine(Lang.Get("driftrelics:unidentified-hint"));
            }
            else
            {
                var modSystem = api.ModLoader.GetModSystem<DriftRelicsModSystem>();
                var colored = RelicsDisplay.GetDisplayNameColored(stack, base.GetHeldItemName(stack),
                                                                  modSystem.Api.Tiers);
                dsc.AppendLine(colored);
                var tierName = Lang.Get("driftrelics:tier-" + RelicsUtil.GetTier(stack));
                dsc.AppendLine(Lang.Get("driftrelics:tier-line", tierName));
            }
        }
        base.GetHeldItemInfo(inSlot, dsc, world, withDebugInfo);
    }
}

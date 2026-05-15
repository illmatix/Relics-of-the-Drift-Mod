using System;
using Baubles.Api;
using Vintagestory.API.Common;

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
}

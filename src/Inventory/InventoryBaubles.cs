using Baubles.Api;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Baubles.Inventory;

public class InventoryBaubles : InventoryBasePlayer
{
    public new const string ClassName = "baubles";
    public const int Size = 4;

    private readonly ItemSlot[] slots;

    public override int Count => slots.Length;
    public override ItemSlot this[int slotId]
    {
        get => slots[slotId];
        set => slots[slotId] = value;
    }

    public InventoryBaubles(string className, string playerUID, ICoreAPI api)
        : base(className, playerUID, api)
    {
        slots = BuildSlots();
        baseWeight = 1.5f;
    }

    public InventoryBaubles(string inventoryId, ICoreAPI api)
        : base(inventoryId, api)
    {
        slots = BuildSlots();
        baseWeight = 1.5f;
    }

    private ItemSlot[] BuildSlots() => new ItemSlot[]
    {
        new ItemSlotBauble(this, BaubleSlotType.Ring),
        new ItemSlotBauble(this, BaubleSlotType.Ring),
        new ItemSlotBauble(this, BaubleSlotType.Bracelet),
        new ItemSlotBauble(this, BaubleSlotType.Trinket),
    };

    public override void FromTreeAttributes(ITreeAttribute tree)
    {
        var dirty = new System.Collections.Generic.List<ItemSlot>();
        SlotsFromTreeAttributes(tree, slots, dirty);
        for (int i = 0; i < dirty.Count; i++) DidModifyItemSlot(dirty[i], null);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
        => SlotsToTreeAttributes(slots, tree);

    protected override ItemSlot NewSlot(int slotId)
    {
        // Layout determines slot type. NewSlot is rarely called for fixed
        // layouts; if the base resizes for any reason, fall back to ring.
        return new ItemSlotBauble(this,
            slotId switch
            {
                0 or 1 => BaubleSlotType.Ring,
                2      => BaubleSlotType.Bracelet,
                _      => BaubleSlotType.Trinket
            });
    }
}

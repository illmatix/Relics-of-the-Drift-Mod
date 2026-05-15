using DriftRelics.Api;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace DriftRelics.Inventory;

public class InventoryRelics : InventoryBasePlayer
{
    public new const string ClassName = "driftrelics";
    public const int Size = 4;

    private readonly ItemSlot[] slots;

    public System.Action<int, ItemStack?, ItemStack?>? SlotChanged;
    private readonly ItemStack?[] previousStacks = new ItemStack?[Size];

    public override int Count => slots.Length;
    public override ItemSlot this[int slotId]
    {
        get => slots[slotId];
        set => slots[slotId] = value;
    }

    public InventoryRelics(string className, string playerUID, ICoreAPI api)
        : base(className, playerUID, api)
    {
        slots = BuildSlots();
        baseWeight = 1.5f;
    }

    public InventoryRelics(string inventoryId, ICoreAPI api)
        : base(inventoryId, api)
    {
        slots = BuildSlots();
        baseWeight = 1.5f;
    }

    private ItemSlot[] BuildSlots() => new ItemSlot[]
    {
        new ItemSlotRelic(this, RelicSlotType.Ring),
        new ItemSlotRelic(this, RelicSlotType.Ring),
        new ItemSlotRelic(this, RelicSlotType.Bracelet),
        new ItemSlotRelic(this, RelicSlotType.Trinket),
    };

    public override void FromTreeAttributes(ITreeAttribute tree)
    {
        var dirty = new System.Collections.Generic.List<ItemSlot>();
        SlotsFromTreeAttributes(tree, slots, dirty);
        for (int i = 0; i < dirty.Count; i++) DidModifyItemSlot(dirty[i], null);
        for (int i = 0; i < slots.Length; i++)
        {
            previousStacks[i] = slots[i].Itemstack?.Clone();
        }
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
        => SlotsToTreeAttributes(slots, tree);

    protected override ItemSlot NewSlot(int slotId)
    {
        // Layout determines slot type. NewSlot is rarely called for fixed
        // layouts; if the base resizes for any reason, fall back to ring.
        return new ItemSlotRelic(this,
            slotId switch
            {
                0 or 1 => RelicSlotType.Ring,
                2      => RelicSlotType.Bracelet,
                _      => RelicSlotType.Trinket
            });
    }

    public override void OnItemSlotModified(ItemSlot slot)
    {
        int idx = System.Array.IndexOf(slots, slot);
        if (idx >= 0)
        {
            var oldStack = previousStacks[idx];
            var newStack = slot.Itemstack;
            previousStacks[idx] = newStack?.Clone();
            SlotChanged?.Invoke(idx, oldStack, newStack);
        }
        base.OnItemSlotModified(slot);
    }
}

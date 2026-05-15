using System;
using DriftRelics.Affixes;
using DriftRelics.Entity;
using DriftRelics.Inventory;
using Vintagestory.API.Common;

namespace DriftRelics.Api;

public sealed class RelicsAPI : IRelicsAPI
{
    private readonly ICoreAPI api;
    public IAffixRegistry Affixes { get; }
    public IModifierRegistry Modifiers { get; }

    public event Action<EntityPlayer, ItemStack, RelicSlotType>? OnRelicEquipped;
    public event Action<EntityPlayer, ItemStack, RelicSlotType>? OnRelicUnequipped;
    public event Action<EntityPlayer, ItemStack>? OnRelicIdentified;

    public RelicsAPI(ICoreAPI api, IAffixRegistry affixes, IModifierRegistry modifiers)
    {
        this.api = api;
        Affixes = affixes;
        Modifiers = modifiers;
    }

    public InventoryRelics? GetRelics(EntityPlayer player)
        => player?.GetBehavior<EntityBehaviorRelics>()?.Inventory;

    public bool IsRelic(ItemStack? stack) => RelicsUtil.IsRelic(stack);
    public RelicSlotType? GetSlotType(ItemStack? stack) => RelicsUtil.GetSlotType(stack);
    public bool IsIdentified(ItemStack? stack) => RelicsUtil.IsIdentified(stack);
    public RelicInstance? GetInstance(ItemStack? stack) => RelicsUtil.GetInstance(stack);
    public string GetDisplayName(ItemStack stack)
        => RelicsDisplay.GetDisplayName(stack, stack.GetName());

    public ItemStack? RollUnidentifiedRelic(RelicSlotType slotType, long seed)
    {
        var code = new AssetLocation("driftrelics", slotType.ToString().ToLowerInvariant());
        var item = api.World.GetItem(code);
        if (item == null) return null;

        var stack = new ItemStack(item);
        var pool = Affixes.BuildPool();
        var instance = RelicRoller.Roll(slotType, seed, pool);
        RelicsUtil.WriteInstance(stack, instance);
        return stack;
    }

    public void Identify(ItemStack stack)
    {
        if (!IsRelic(stack) || IsIdentified(stack)) return;
        stack.Attributes.SetBool("relic.identified", true);
    }

    // Public so internal consumers (EntityBehaviorRelics, BEScholarsLectern)
    // can fire events without reflection. External mods should subscribe to
    // OnRelicEquipped / OnRelicUnequipped / OnRelicIdentified instead of
    // calling these directly.
    public void FireEquipped(EntityPlayer player, ItemStack stack, RelicSlotType type)
        => OnRelicEquipped?.Invoke(player, stack, type);

    public void FireUnequipped(EntityPlayer player, ItemStack stack, RelicSlotType type)
        => OnRelicUnequipped?.Invoke(player, stack, type);

    public void FireIdentified(EntityPlayer player, ItemStack stack)
        => OnRelicIdentified?.Invoke(player, stack);
}

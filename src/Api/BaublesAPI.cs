using System;
using Baubles.Affixes;
using Baubles.Entity;
using Baubles.Inventory;
using Vintagestory.API.Common;

namespace Baubles.Api;

public sealed class BaublesAPI : IBaublesAPI
{
    private readonly ICoreAPI api;
    public IAffixRegistry Affixes { get; }
    public IModifierRegistry Modifiers { get; }

    public event Action<EntityPlayer, ItemStack, BaubleSlotType>? OnBaubleEquipped;
    public event Action<EntityPlayer, ItemStack, BaubleSlotType>? OnBaubleUnequipped;
    public event Action<EntityPlayer, ItemStack>? OnBaubleIdentified;

    public BaublesAPI(ICoreAPI api, IAffixRegistry affixes, IModifierRegistry modifiers)
    {
        this.api = api;
        Affixes = affixes;
        Modifiers = modifiers;
    }

    public InventoryBaubles? GetBaubles(EntityPlayer player)
        => player?.GetBehavior<EntityBehaviorBaubles>()?.Inventory;

    public bool IsBauble(ItemStack? stack) => BaublesUtil.IsBauble(stack);
    public BaubleSlotType? GetSlotType(ItemStack? stack) => BaublesUtil.GetSlotType(stack);
    public bool IsIdentified(ItemStack? stack) => BaublesUtil.IsIdentified(stack);
    public BaubleInstance? GetInstance(ItemStack? stack) => BaublesUtil.GetInstance(stack);
    public string GetDisplayName(ItemStack stack)
        => BaublesDisplay.GetDisplayName(stack, stack.GetName());

    public ItemStack? RollUnidentifiedBauble(BaubleSlotType slotType, long seed)
    {
        var code = new AssetLocation("baubles", slotType.ToString().ToLowerInvariant());
        var item = api.World.GetItem(code);
        if (item == null) return null;

        var stack = new ItemStack(item);
        var pool = Affixes.BuildPool();
        var instance = BaubleRoller.Roll(slotType, seed, pool);
        BaublesUtil.WriteInstance(stack, instance);
        return stack;
    }

    public void Identify(ItemStack stack)
    {
        if (!IsBauble(stack) || IsIdentified(stack)) return;
        stack.Attributes.SetBool("bauble.identified", true);
    }

    // Public so internal consumers (EntityBehaviorBaubles, BEScholarsLectern)
    // can fire events without reflection. External mods should subscribe to
    // OnBaubleEquipped / OnBaubleUnequipped / OnBaubleIdentified instead of
    // calling these directly.
    public void FireEquipped(EntityPlayer player, ItemStack stack, BaubleSlotType type)
        => OnBaubleEquipped?.Invoke(player, stack, type);

    public void FireUnequipped(EntityPlayer player, ItemStack stack, BaubleSlotType type)
        => OnBaubleUnequipped?.Invoke(player, stack, type);

    public void FireIdentified(EntityPlayer player, ItemStack stack)
        => OnBaubleIdentified?.Invoke(player, stack);
}

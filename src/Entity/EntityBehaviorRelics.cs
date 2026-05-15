using System.Collections.Generic;
using DriftRelics.Api;
using DriftRelics.Inventory;
using DriftRelics.Modifier;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace DriftRelics.Entity;

public class EntityBehaviorRelics : EntityBehavior
{
    public const string Code = "driftrelics";

    public InventoryRelics Inventory { get; private set; } = null!;
    private DriftRelicsModSystem? modSystem;

    public EntityBehaviorRelics(Vintagestory.API.Common.Entities.Entity entity)
        : base(entity) { }

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        var api = entity.World.Api;
        modSystem = api.ModLoader.GetModSystem<DriftRelicsModSystem>();

        var uid = (entity as Vintagestory.API.Common.EntityPlayer)?.PlayerUID ?? entity.EntityId.ToString();
        Inventory = new InventoryRelics(InventoryRelics.ClassName, uid, api);
        Inventory.LateInitialize($"{InventoryRelics.ClassName}-{entity.EntityId}", api);

        Inventory.SlotChanged = OnSlotChanged;

        LoadFromTree();
        // Only the client needs to mirror server-pushed tree changes back into the
        // local inventory. Registering on the server would cause our own SaveToTree
        // calls to recursively re-load and re-fire slot-change events, double-
        // applying stat modifiers and potentially recursing through OnSlotChanged.
        if (api.Side == EnumAppSide.Client)
        {
            entity.WatchedAttributes.RegisterModifiedListener("relicsInv", LoadFromTree);
        }

        // Re-apply modifiers for every currently-equipped, identified bauble.
        if (api.Side == EnumAppSide.Server)
        {
            for (int i = 0; i < Inventory.Count; i++)
            {
                var stack = Inventory[i].Itemstack;
                if (stack != null && RelicsUtil.IsIdentified(stack))
                {
                    ApplyMods(stack);
                }
            }
        }

        base.Initialize(properties, attributes);
    }

    public override void OnEntityDespawn(EntityDespawnData despawn)
    {
        if (entity.World?.Side == EnumAppSide.Server)
        {
            for (int i = 0; i < Inventory.Count; i++)
            {
                var stack = Inventory[i].Itemstack;
                if (stack != null && RelicsUtil.IsIdentified(stack))
                {
                    RemoveMods(stack);
                }
            }
        }
        Inventory.SlotChanged = null;
        base.OnEntityDespawn(despawn);
    }

    private void OnSlotChanged(int index, ItemStack? oldStack, ItemStack? newStack)
    {
        if (entity.World.Side != EnumAppSide.Server) return;
        if (entity is not Vintagestory.API.Common.EntityPlayer ep) return;

        if (oldStack != null && RelicsUtil.IsRelic(oldStack))
        {
            if (RelicsUtil.IsIdentified(oldStack)) RemoveMods(oldStack);
            var slotType = RelicsUtil.GetSlotType(oldStack)!.Value;
            modSystem?.Api.FireUnequipped(ep, oldStack, slotType);
        }

        if (newStack != null && RelicsUtil.IsRelic(newStack))
        {
            if (RelicsUtil.IsIdentified(newStack)) ApplyMods(newStack);
            var slotType = RelicsUtil.GetSlotType(newStack)!.Value;
            modSystem?.Api.FireEquipped(ep, newStack, slotType);
        }

        SaveToTree();
    }

    private void ApplyMods(ItemStack stack)
    {
        if (entity is not Vintagestory.API.Common.EntityPlayer ep) return;
        foreach (var entry in EnumerateMods(stack))
        {
            var code = ModifierCode(stack, entry.Key);
            modSystem?.Modifiers.TryApply(ep, entry, code);
        }
    }

    private void RemoveMods(ItemStack stack)
    {
        if (entity is not Vintagestory.API.Common.EntityPlayer ep) return;
        foreach (var entry in EnumerateMods(stack))
        {
            var code = ModifierCode(stack, entry.Key);
            modSystem?.Modifiers.TryRemove(ep, entry, code);
        }
    }

    private IEnumerable<ModifierEntry> EnumerateMods(ItemStack stack)
    {
        var prefix = RelicsUtil.GetPrefixCode(stack);
        var suffix = RelicsUtil.GetSuffixCode(stack);
        if (prefix != null)
        {
            var a = modSystem?.Affixes.GetByCode(prefix);
            if (a != null) foreach (var m in a.Mods) yield return m;
        }
        if (suffix != null)
        {
            var a = modSystem?.Affixes.GetByCode(suffix);
            if (a != null) foreach (var m in a.Mods) yield return m;
        }
    }

    private static string ModifierCode(ItemStack stack, string key)
        => $"driftrelics:{key}:{RelicsUtil.GetSeed(stack):X}";

    private void LoadFromTree()
    {
        var tree = entity.WatchedAttributes.GetTreeAttribute("relicsInv");
        if (tree != null) Inventory.FromTreeAttributes(tree);
    }

    private void SaveToTree()
    {
        var tree = new TreeAttribute();
        Inventory.ToTreeAttributes(tree);
        entity.WatchedAttributes["relicsInv"] = tree;
        entity.WatchedAttributes.MarkPathDirty("relicsInv");
    }

    public override string PropertyName() => Code;
}

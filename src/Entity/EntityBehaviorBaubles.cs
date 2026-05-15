using Baubles.Inventory;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace Baubles.Entity;

public class EntityBehaviorBaubles : EntityBehavior
{
    public const string Code = "baubles";

    public InventoryBaubles Inventory { get; private set; } = null!;

    public EntityBehaviorBaubles(Vintagestory.API.Common.Entities.Entity entity)
        : base(entity)
    {
        Inventory = new InventoryBaubles(null!, entity.WatchedAttributes.GetString("playerUID") ?? "", null!);
    }

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        var api = entity.World.Api;
        Inventory = new InventoryBaubles(InventoryBaubles.ClassName,
            entity is Vintagestory.API.Common.EntityPlayer ep ? ep.PlayerUID : entity.EntityId.ToString(),
            api);
        Inventory.LateInitialize($"{InventoryBaubles.ClassName}-{entity.EntityId}", api);
        LoadFromTree();
        entity.WatchedAttributes.RegisterModifiedListener("baublesInv", LoadFromTree);
        base.Initialize(properties, attributes);
    }

    private void LoadFromTree()
    {
        var tree = entity.WatchedAttributes.GetTreeAttribute("baublesInv");
        if (tree != null) Inventory.FromTreeAttributes(tree);
    }

    public void SaveToTree()
    {
        var tree = new TreeAttribute();
        Inventory.ToTreeAttributes(tree);
        entity.WatchedAttributes["baublesInv"] = tree;
        entity.WatchedAttributes.MarkPathDirty("baublesInv");
    }

    public override string PropertyName() => Code;
}

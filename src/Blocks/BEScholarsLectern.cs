using Baubles.Api;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Baubles.Blocks;

public class BEScholarsLectern : BlockEntityOpenableContainer
{
    private const int TickMs = 250;

    public InventoryGeneric InventoryRef { get; private set; } = null!;
    public override InventoryBase Inventory => InventoryRef;
    public override string InventoryClassName => "baubles-lectern";

    public float ResearchProgressSeconds { get; private set; }
    public float ResearchDurationSeconds { get; private set; } = 60f;

    public BEScholarsLectern()
    {
        InventoryRef = new InventoryGeneric(1, null, null);
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        InventoryRef.LateInitialize($"baubles-lectern-{Pos.X}/{Pos.Y}/{Pos.Z}", api);

        var cfgAsset = api.Assets.TryGet(new AssetLocation("baubles", "config/lectern.json"));
        if (cfgAsset != null)
        {
            var cfg = Newtonsoft.Json.JsonConvert.DeserializeObject<LecternConfig>(cfgAsset.ToText());
            if (cfg != null) ResearchDurationSeconds = cfg.ResearchDurationSeconds;
        }

        if (api.Side == EnumAppSide.Server) RegisterGameTickListener(ResearchTick, TickMs);
    }

    private void ResearchTick(float dt)
    {
        var slot = InventoryRef[0];
        var stack = slot.Itemstack;
        if (stack == null) { Reset(); return; }
        if (!BaublesUtil.IsBauble(stack)) { Reset(); return; }
        if (BaublesUtil.IsIdentified(stack)) { Reset(); return; }

        ResearchProgressSeconds += dt;
        if (ResearchProgressSeconds >= ResearchDurationSeconds)
        {
            var modSystem = Api.ModLoader.GetModSystem<BaublesModSystem>();
            modSystem.Api.Identify(stack);
            slot.MarkDirty();
            MarkDirty(true);
            ResearchProgressSeconds = 0;
        }
        else
        {
            MarkDirty(true);
        }
    }

    private void Reset()
    {
        if (ResearchProgressSeconds != 0)
        {
            ResearchProgressSeconds = 0;
            MarkDirty(true);
        }
    }

    public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
    {
        if (byPlayer is IServerPlayer sp)
        {
            sp.InventoryManager.OpenInventory(InventoryRef);
            return true;
        }
        return false;
    }

    public void OnPlayerInteract(IPlayer byPlayer)
    {
        OnPlayerRightClick(byPlayer, null!);
    }

    public override void ToTreeAttributes(ITreeAttribute tree)
    {
        base.ToTreeAttributes(tree);
        var invTree = new TreeAttribute();
        InventoryRef.ToTreeAttributes(invTree);
        tree["inventory"] = invTree;
        tree.SetFloat("researchProgress", ResearchProgressSeconds);
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
    {
        base.FromTreeAttributes(tree, worldForResolving);
        var invTree = tree.GetTreeAttribute("inventory");
        if (invTree != null) InventoryRef.FromTreeAttributes(invTree);
        ResearchProgressSeconds = tree.GetFloat("researchProgress", 0);
    }

    private sealed class LecternConfig { public float ResearchDurationSeconds { get; set; } = 60f; }
}

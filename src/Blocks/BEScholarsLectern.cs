using DriftRelics.Api;
using DriftRelics.Gui;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace DriftRelics.Blocks;

public class BEScholarsLectern : BlockEntityOpenableContainer
{
    private const int TickMs = 250;

    public InventoryGeneric InventoryRef { get; private set; } = null!;
    public override InventoryBase Inventory => InventoryRef;
    public override string InventoryClassName => "driftrelics-lectern";

    public float ResearchProgressSeconds { get; private set; }
    public float ResearchDurationSeconds { get; private set; } = 60f;

    private float lastSyncSeconds;
    private GuiDialogBlockEntityLectern? clientDialog;

    public BEScholarsLectern()
    {
        InventoryRef = new InventoryGeneric(1, null, null);
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        InventoryRef.LateInitialize($"driftrelics-lectern-{Pos.X}/{Pos.Y}/{Pos.Z}", api);

        var cfgAsset = api.Assets.TryGet(new AssetLocation("driftrelics", "config/lectern.json"));
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
        if (!RelicsUtil.IsRelic(stack)) { Reset(); return; }
        if (RelicsUtil.IsIdentified(stack)) { Reset(); return; }

        ResearchProgressSeconds += dt;
        if (ResearchProgressSeconds >= ResearchDurationSeconds)
        {
            var modSystem = Api.ModLoader.GetModSystem<DriftRelicsModSystem>();
            modSystem.Api.Identify(stack);
            slot.MarkDirty();
            ResearchProgressSeconds = 0;
            lastSyncSeconds = 0;
            MarkDirty(true);
        }
        else if (ResearchProgressSeconds - lastSyncSeconds >= 1f)
        {
            lastSyncSeconds = ResearchProgressSeconds;
            MarkDirty(true);
        }
    }

    private void Reset()
    {
        if (ResearchProgressSeconds != 0)
        {
            ResearchProgressSeconds = 0;
            lastSyncSeconds = 0;
            MarkDirty(true);
        }
    }

    public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
    {
        if (Api.Side == EnumAppSide.Client)
        {
            toggleInventoryDialogClient(byPlayer, () =>
            {
                clientDialog = new GuiDialogBlockEntityLectern(
                    Lang.Get("driftrelics:block-scholarslectern"),
                    InventoryRef,
                    Pos,
                    (ICoreClientAPI)Api);
                clientDialog.Update(ResearchProgressSeconds, ResearchDurationSeconds);
                clientDialog.OnClosed += () => clientDialog = null;
                return clientDialog;
            });
        }
        return true;
    }

    public override void OnReceivedServerPacket(int packetid, byte[] data)
    {
        base.OnReceivedServerPacket(packetid, data);
        if (packetid == 1001)
        {
            clientDialog?.TryClose();
            clientDialog = null;
        }
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

        if (worldForResolving.Side == EnumAppSide.Client)
        {
            clientDialog?.Update(ResearchProgressSeconds, ResearchDurationSeconds);
        }
    }

    private sealed class LecternConfig { public float ResearchDurationSeconds { get; set; } = 60f; }
}

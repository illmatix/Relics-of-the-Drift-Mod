using System.Linq;
using DriftRelics.Affixes;
using DriftRelics.Api;
using DriftRelics.Entity;
using DriftRelics.Gui;
using DriftRelics.Inventory;
using DriftRelics.Items;
using DriftRelics.Modifier;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace DriftRelics;

public class DriftRelicsModSystem : ModSystem
{
    private ICoreClientAPI? capi;
    private bool tabRegistered;

    public AffixRegistry Affixes { get; private set; } = null!;
    public ModifierRegistry Modifiers { get; private set; } = null!;
    public RelicsAPI Api { get; private set; } = null!;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        Affixes   = new AffixRegistry();
        Modifiers = new ModifierRegistry(api);
        Api       = new RelicsAPI(api, Affixes, Modifiers);

        api.RegisterEntityBehaviorClass(EntityBehaviorRelics.Code, typeof(EntityBehaviorRelics));
        RegisterInventoryClass(api, InventoryRelics.ClassName, typeof(InventoryRelics));
        api.RegisterItemClass("ItemRelic", typeof(ItemRelic));
        api.RegisterItemClass("ItemUnidentifiedRoller", typeof(ItemUnidentifiedRoller));

        api.RegisterBlockClass("BlockScholarsLectern", typeof(DriftRelics.Blocks.BlockScholarsLectern));
        api.RegisterBlockEntityClass("BEScholarsLectern", typeof(DriftRelics.Blocks.BEScholarsLectern));

        api.Logger.Notification("[DriftRelics] mod system starting");
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        base.AssetsFinalize(api);
        var asset = api.Assets.TryGet(new AssetLocation("driftrelics", "config/affixes.json"));
        if (asset == null)
        {
            api.Logger.Warning("[DriftRelics] affixes.json not found — no affixes will roll");
            return;
        }
        var json = asset.ToText();
        var cfg = DriftRelics.Affixes.AffixConfigLoader.LoadFromJson(json);
        Affixes.SetTiers(cfg.Tiers);
        foreach (var kv in cfg.Signatures) Affixes.SetSignature(kv.Key, kv.Value);
        foreach (var a in cfg.Prefixes) Affixes.Register(a);
        foreach (var a in cfg.Suffixes) Affixes.Register(a);
        api.Logger.Notification(
            $"[DriftRelics] loaded {cfg.Tiers.Count} tiers, {cfg.Prefixes.Count} prefixes, {cfg.Suffixes.Count} suffixes");
    }

    public override void StartClientSide(ICoreClientAPI capi)
    {
        this.capi = capi;
        capi.Event.LevelFinalize += TryRegisterCharacterTab;
        capi.Event.PlayerJoin += _ => TryRegisterCharacterTab();
    }

    public override void StartServerSide(ICoreServerAPI sapi)
    {
        sapi.Event.PlayerJoin += player =>
        {
            var beh = player.Entity?.GetBehavior<EntityBehaviorRelics>();
            if (beh?.Inventory != null)
                player.InventoryManager.OpenInventory(beh.Inventory);
        };
    }

    private void TryRegisterCharacterTab()
    {
        if (tabRegistered || capi == null) return;
        var dlg = capi.Gui.LoadedGuis.OfType<GuiDialogCharacterBase>().FirstOrDefault();
        if (dlg == null) return;
        var tabName = Lang.Get("driftrelics:charactertab-relics");
        if (dlg.Tabs.Any(t => t.Name == tabName)) return;
        dlg.Tabs.Add(new GuiTab { Name = tabName, DataInt = dlg.Tabs.Count });
        dlg.RenderTabHandlers.Add(compo => GuiRelicsTab.Compose(compo, capi));
        tabRegistered = true;
    }

    // RegisterInventoryClass is not exposed through IClassRegistryAPI in VS 1.22.
    // api.ClassRegistry's runtime type is Vintagestory.Common.ClassRegistryAPI, which
    // holds an internal `registry` field of type Vintagestory.Common.ClassRegistry —
    // that's the class with the RegisterInventoryClass(string, Type) method.
    private static void RegisterInventoryClass(ICoreAPI api, string className, System.Type type)
    {
        var apiObj = api.ClassRegistry;
        var field = apiObj.GetType().GetField("registry",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var inner = field?.GetValue(apiObj);
        if (inner == null)
        {
            api.Logger.Error("[DriftRelics] could not reach internal ClassRegistry; inventory class '{0}' not registered", className);
            return;
        }
        var method = inner.GetType().GetMethod("RegisterInventoryClass",
            new[] { typeof(string), typeof(System.Type) });
        if (method == null)
        {
            api.Logger.Error("[DriftRelics] RegisterInventoryClass method missing on {0}", inner.GetType().FullName);
            return;
        }
        method.Invoke(inner, new object[] { className, type });
    }
}

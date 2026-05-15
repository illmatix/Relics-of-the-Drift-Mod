using System.Linq;
using Baubles.Affixes;
using Baubles.Api;
using Baubles.Entity;
using Baubles.Gui;
using Baubles.Inventory;
using Baubles.Items;
using Baubles.Modifier;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace Baubles;

public class BaublesModSystem : ModSystem
{
    private ICoreClientAPI? capi;
    private bool tabRegistered;

    public AffixRegistry Affixes { get; private set; } = null!;
    public ModifierRegistry Modifiers { get; private set; } = null!;
    public BaublesAPI Api { get; private set; } = null!;

    public override void Start(ICoreAPI api)
    {
        base.Start(api);

        Affixes   = new AffixRegistry();
        Modifiers = new ModifierRegistry(api);
        Api       = new BaublesAPI(api, Affixes, Modifiers);

        api.RegisterEntityBehaviorClass(EntityBehaviorBaubles.Code, typeof(EntityBehaviorBaubles));
        RegisterInventoryClass(api, InventoryBaubles.ClassName, typeof(InventoryBaubles));
        api.RegisterItemClass("ItemBauble", typeof(ItemBauble));
        api.RegisterItemClass("ItemUnidentifiedRoller", typeof(ItemUnidentifiedRoller));

        api.Logger.Notification("[Baubles] mod system starting");
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        base.AssetsFinalize(api);
        var asset = api.Assets.TryGet(new AssetLocation("baubles", "config/affixes.json"));
        if (asset == null)
        {
            api.Logger.Warning("[Baubles] affixes.json not found — no affixes will roll");
            return;
        }
        var json = asset.ToText();
        var cfg = Baubles.Affixes.AffixConfigLoader.LoadFromJson(json);
        Affixes.RollChances = cfg.RollChances;
        foreach (var a in cfg.Prefixes) Affixes.Register(a);
        foreach (var a in cfg.Suffixes) Affixes.Register(a);
        api.Logger.Notification(
            $"[Baubles] loaded {cfg.Prefixes.Count} prefixes, {cfg.Suffixes.Count} suffixes");
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
            var beh = player.Entity?.GetBehavior<EntityBehaviorBaubles>();
            if (beh?.Inventory != null)
                player.InventoryManager.OpenInventory(beh.Inventory);
        };
    }

    private void TryRegisterCharacterTab()
    {
        if (tabRegistered || capi == null) return;
        var dlg = capi.Gui.LoadedGuis.OfType<GuiDialogCharacterBase>().FirstOrDefault();
        if (dlg == null) return;
        var tabName = Lang.Get("baubles:charactertab-baubles");
        if (dlg.Tabs.Any(t => t.Name == tabName)) return;
        dlg.Tabs.Add(new GuiTab { Name = tabName, DataInt = dlg.Tabs.Count });
        dlg.RenderTabHandlers.Add(compo => GuiBaublesTab.Compose(compo, capi));
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
            api.Logger.Error("[Baubles] could not reach internal ClassRegistry; inventory class '{0}' not registered", className);
            return;
        }
        var method = inner.GetType().GetMethod("RegisterInventoryClass",
            new[] { typeof(string), typeof(System.Type) });
        if (method == null)
        {
            api.Logger.Error("[Baubles] RegisterInventoryClass method missing on {0}", inner.GetType().FullName);
            return;
        }
        method.Invoke(inner, new object[] { className, type });
    }
}

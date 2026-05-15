using System.Linq;
using Baubles.Entity;
using Baubles.Gui;
using Baubles.Inventory;
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

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.RegisterEntityBehaviorClass(EntityBehaviorBaubles.Code, typeof(EntityBehaviorBaubles));
        api.Logger.Notification("[Baubles] mod system starting");
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
            {
                player.InventoryManager.OpenInventory(beh.Inventory);
            }
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
}

using DriftRelics.Entity;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace DriftRelics.Gui;

public static class GuiRelicsTab
{
    private const double TabW      = 290;
    private const double TabH      = 245;
    private const double SlotGridX = 95;
    private const double SlotGridY = 76;

    public static void Compose(GuiComposer compo, ICoreClientAPI capi)
    {
        var player = capi.World.Player;
        var beh = player?.Entity?.GetBehavior<EntityBehaviorRelics>();
        if (beh == null) return;

        var inv = beh.Inventory;

        var bgBounds     = ElementBounds.Fixed(0,    18, TabW, TabH);
        var titleBounds  = ElementBounds.Fixed(20,   28, TabW - 40, 24);
        var accentBounds = ElementBounds.Fixed(70,   54, TabW - 140, 14);
        var slotBounds   = ElementStdBounds.SlotGrid(EnumDialogArea.None, SlotGridX, SlotGridY, 2, 2);
        var hintBounds   = ElementBounds.Fixed(20,  190, TabW - 40, 60);

        compo.AddDynamicCustomDraw(bgBounds,     (ctx, surf, b) => RelicTheme.DrawParchment(ctx, b),     "relicsBg");
        compo.AddDynamicCustomDraw(accentBounds, (ctx, surf, b) => RelicTheme.DrawBraidedAccent(ctx, b), "relicsAccent");

        compo.AddStaticText(Lang.Get("driftrelics:tab-title"),
            CairoFont.WhiteSmallishText(), titleBounds);

        compo.AddItemSlotGrid(inv, _ => { }, 2, new[] { 0, 1, 2, 3 }, slotBounds, "relicsGrid");

        compo.AddRichtext(Lang.Get("driftrelics:tab-hint"),
            CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15), hintBounds);
    }
}

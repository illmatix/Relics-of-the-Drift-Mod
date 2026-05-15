using Baubles.Entity;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Baubles.Gui;

public static class GuiBaublesTab
{
    public static void Compose(GuiComposer compo, ICoreClientAPI capi)
    {
        var player = capi.World.Player;
        var beh = player?.Entity?.GetBehavior<EntityBehaviorBaubles>();
        if (beh == null) return;

        var inv = beh.Inventory;

        var titleBounds  = ElementBounds.Fixed(0, 25, 385, 25);
        var slotBounds   = ElementStdBounds.SlotGrid(EnumDialogArea.None, 0, 60, 2, 2);
        var hintBounds   = ElementBounds.Fixed(0, 60 + 2 * (GuiElementPassiveItemSlot.unscaledSlotSize + GuiElementItemSlotGridBase.unscaledSlotPadding) + 20, 385, 50);

        compo.AddStaticText(Lang.Get("baubles:tab-title"),
            CairoFont.WhiteSmallishText(), titleBounds);

        compo.AddItemSlotGrid(inv, dummy => { }, 2, new[] { 0, 1, 2, 3 }, slotBounds, "baublesGrid");

        compo.AddRichtext(Lang.Get("baubles:tab-hint"),
            CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15), hintBounds);
    }
}

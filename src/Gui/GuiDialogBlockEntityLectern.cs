using System;
using Cairo;
using DriftRelics.Api;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace DriftRelics.Gui;

public class GuiDialogBlockEntityLectern : GuiDialogBlockEntity
{
    private const double DialogW      = 260;
    private const double DialogH      = 235;
    private const double SlotX        = 106;
    private const double SlotY        = 58;
    private const double SlotCenterX  = SlotX + 24;
    private const double SlotCenterY  = SlotY + 24;
    private const double RuneRadius   = 42;
    private const double GlowRadius   = 96;

    private long lastAnimRedrawMs;
    private long lastProgressRedrawMs;
    private float progressSeconds;
    private float durationSeconds = 60f;

    protected override double FloatyDialogPosition => 0.75;

    public GuiDialogBlockEntityLectern(string dialogTitle, InventoryBase inventory, BlockPos pos, ICoreClientAPI capi)
        : base(dialogTitle, inventory, pos, capi)
    {
        if (IsDuplicate) return;
        capi.World.Player.InventoryManager.OpenInventory(inventory);
        SetupDialog();
    }

    private void OnInventorySlotModified(int slotid)
    {
        capi.Event.EnqueueMainThreadTask(SetupDialog, "setuplecterndlg");
    }

    private void SetupDialog()
    {
        var hovered = capi.World.Player.InventoryManager.CurrentHoveredSlot;
        if (hovered != null && hovered.Inventory == Inventory) capi.Input.TriggerOnMouseLeaveSlot(hovered);
        else hovered = null;

        var dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.RightMiddle)
            .WithFixedAlignmentOffset(-GuiStyle.DialogToScreenPadding, 0);

        var bgBounds       = ElementBounds.Fixed(0,     0, DialogW, DialogH);
        var accentBounds   = ElementBounds.Fixed(40,   36, DialogW - 80, 14);
        var glowBounds     = ElementBounds.Fixed(0,     0, DialogW, DialogH);
        var runeBounds     = ElementBounds.Fixed(0,     0, DialogW, DialogH);
        var slotBounds     = ElementStdBounds.SlotGrid(EnumDialogArea.None, SlotX, SlotY, 1, 1);
        var particleBounds = ElementBounds.Fixed(0,     0, DialogW, DialogH);
        var progressBg     = ElementBounds.Fixed(20,  138, DialogW - 40, 22);
        var hintBounds     = ElementBounds.Fixed(20,  172, DialogW - 40, 55);

        var contentBounds = ElementBounds.Fixed(0, 0, DialogW, DialogH);

        ClearComposers();
        SingleComposer = capi.Gui.CreateCompo("driftrelicslectern-" + BlockEntityPosition, dialogBounds)
            .AddDialogTitleBar(DialogTitle, OnTitleBarClose)
            .BeginChildElements(contentBounds)
                .AddDynamicCustomDraw(bgBounds,       (ctx, surf, b) => RelicTheme.DrawParchment(ctx, b),     "parchment")
                .AddDynamicCustomDraw(glowBounds,     DrawGlow,                                              "glow")
                .AddDynamicCustomDraw(accentBounds,   (ctx, surf, b) => RelicTheme.DrawBraidedAccent(ctx, b), "accent")
                .AddDynamicCustomDraw(runeBounds,     DrawRunes,                                             "runes")
                .AddItemSlotGrid(Inventory, SendInvPacket, 1, new[] { 0 }, slotBounds,                       "lecternSlot")
                .AddDynamicCustomDraw(particleBounds, DrawParticles,                                         "particles")
                .AddDynamicCustomDraw(progressBg,     DrawProgress,                                          "progress")
                .AddRichtext(GetHintText(), CairoFont.WhiteDetailText().WithLineHeightMultiplier(1.15), hintBounds)
            .EndChildElements()
            .Compose();

        if (hovered != null) SingleComposer.OnMouseMove(new MouseEvent(capi.Input.MouseX, capi.Input.MouseY));
    }

    private string GetHintText()
    {
        var stack = Inventory[0].Itemstack;
        if (stack == null || !RelicsUtil.IsRelic(stack)) return Lang.Get("driftrelics:lectern-hint-empty");
        if (RelicsUtil.IsIdentified(stack)) return Lang.Get("driftrelics:lectern-hint-identified");
        return Lang.Get("driftrelics:lectern-hint-progress");
    }

    private bool IsResearching()
    {
        var stack = Inventory[0].Itemstack;
        return stack != null && RelicsUtil.IsRelic(stack) && !RelicsUtil.IsIdentified(stack);
    }

    private void DrawRunes(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        RelicTheme.DrawRuneRing(ctx, GuiElement.scaled(SlotCenterX), GuiElement.scaled(SlotCenterY), GuiElement.scaled(RuneRadius));
    }

    private void DrawGlow(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        var researching = IsResearching();
        double baseIntensity = researching ? 0.65 : 0.45;
        double pulse = researching
            ? 0.35 * (0.5 + 0.5 * Math.Sin(capi.ElapsedMilliseconds / 1000.0 * Math.PI * 2 / 3))
            : 0.0;
        var intensity = baseIntensity + pulse;
        RelicTheme.DrawAmberGlow(ctx, bounds,
            GuiElement.scaled(SlotCenterX),
            GuiElement.scaled(SlotCenterY),
            GuiElement.scaled(GlowRadius),
            intensity);
    }

    private void DrawParticles(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        if (!IsResearching()) return;
        RelicTheme.DrawEmberParticles(ctx, bounds,
            GuiElement.scaled(SlotCenterX),
            GuiElement.scaled(138),
            GuiElement.scaled(78),
            capi.ElapsedMilliseconds);
    }

    private void DrawProgress(Context ctx, ImageSurface surface, ElementBounds bounds)
    {
        var pct = durationSeconds > 0 ? GameMath.Clamp(progressSeconds / durationSeconds, 0f, 1f) : 0f;
        RelicTheme.DrawProgressBar(ctx, bounds, pct);
    }

    public override void OnFinalizeFrame(float dt)
    {
        base.OnFinalizeFrame(dt);
        if (!IsOpened() || SingleComposer == null) return;
        if (capi.ElapsedMilliseconds - lastAnimRedrawMs < 80) return;
        lastAnimRedrawMs = capi.ElapsedMilliseconds;
        SingleComposer.GetCustomDraw("glow")?.Redraw();
        SingleComposer.GetCustomDraw("particles")?.Redraw();
    }

    public void Update(float progress, float duration)
    {
        progressSeconds = progress;
        durationSeconds = duration;
        if (IsOpened() && capi.ElapsedMilliseconds - lastProgressRedrawMs > 250)
        {
            SingleComposer?.GetCustomDraw("progress")?.Redraw();
            lastProgressRedrawMs = capi.ElapsedMilliseconds;
        }
    }

    private void SendInvPacket(object p)
    {
        capi.Network.SendBlockEntityPacket(BlockEntityPosition.X, BlockEntityPosition.Y, BlockEntityPosition.Z, p);
    }

    private void OnTitleBarClose() => TryClose();

    public override void OnGuiOpened()
    {
        base.OnGuiOpened();
        Inventory.SlotModified += OnInventorySlotModified;
    }

    public override void OnGuiClosed()
    {
        Inventory.SlotModified -= OnInventorySlotModified;
        SingleComposer?.GetSlotGrid("lecternSlot")?.OnGuiClosed(capi);
        base.OnGuiClosed();
    }
}

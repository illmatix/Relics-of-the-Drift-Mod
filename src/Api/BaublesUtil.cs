using System;
using Baubles.Affixes;
using Vintagestory.API.Common;

namespace Baubles.Api;

public static class BaublesUtil
{
    private const string AttrSlotType   = "slotType";
    private const string AttrSeed       = "bauble.seed";
    private const string AttrPrefix     = "bauble.prefix";
    private const string AttrSuffix     = "bauble.suffix";
    private const string AttrIdentified = "bauble.identified";

    public static BaubleSlotType? GetSlotType(ItemStack? stack)
    {
        if (stack?.Collectible == null) return null;

        if (stack.Collectible is IBaubleItem bi) return bi.SlotType;

        var attr = stack.Collectible.Attributes?["bauble"]?[AttrSlotType];
        if (attr == null || !attr.Exists) return null;

        var raw = attr.AsString(null);
        if (raw == null) return null;
        return Enum.TryParse<BaubleSlotType>(raw, ignoreCase: true, out var t) ? t : null;
    }

    public static bool IsBauble(ItemStack? stack) => GetSlotType(stack) != null;

    public static bool IsIdentified(ItemStack? stack)
        => stack?.Attributes?.GetBool(AttrIdentified, false) ?? false;

    public static long GetSeed(ItemStack? stack)
        => stack?.Attributes?.GetLong(AttrSeed, 0L) ?? 0L;

    public static string? GetPrefixCode(ItemStack? stack)
    {
        var s = stack?.Attributes?.GetString(AttrPrefix, null);
        return string.IsNullOrEmpty(s) ? null : s;
    }

    public static string? GetSuffixCode(ItemStack? stack)
    {
        var s = stack?.Attributes?.GetString(AttrSuffix, null);
        return string.IsNullOrEmpty(s) ? null : s;
    }

    public static BaubleInstance? GetInstance(ItemStack? stack)
    {
        var slot = GetSlotType(stack);
        if (slot == null) return null;
        return new BaubleInstance(
            slot.Value,
            GetPrefixCode(stack),
            GetSuffixCode(stack),
            GetSeed(stack),
            IsIdentified(stack));
    }

    public static void WriteInstance(ItemStack stack, BaubleInstance instance)
    {
        stack.Attributes.SetLong(AttrSeed, instance.Seed);
        stack.Attributes.SetString(AttrPrefix, instance.PrefixCode ?? "");
        stack.Attributes.SetString(AttrSuffix, instance.SuffixCode ?? "");
        stack.Attributes.SetBool(AttrIdentified, instance.Identified);
    }
}

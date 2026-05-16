using System;
using DriftRelics.Affixes;
using Vintagestory.API.Common;

namespace DriftRelics.Api;

public static class RelicsUtil
{
    private const string AttrSlotType   = "slotType";
    private const string AttrSeed       = "relic.seed";
    private const string AttrPrefix     = "relic.prefix";
    private const string AttrSuffix     = "relic.suffix";
    private const string AttrIdentified = "relic.identified";
    private const string AttrTier       = "relic.tier";

    public static RelicSlotType? GetSlotType(ItemStack? stack)
    {
        if (stack?.Collectible == null) return null;

        if (stack.Collectible is IRelicItem bi) return bi.SlotType;

        var attr = stack.Collectible.Attributes?["relic"]?[AttrSlotType];
        if (attr == null || !attr.Exists) return null;

        var raw = attr.AsString(null);
        if (raw == null) return null;
        return Enum.TryParse<RelicSlotType>(raw, ignoreCase: true, out var t) ? t : null;
    }

    public static bool IsRelic(ItemStack? stack) => GetSlotType(stack) != null;

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

    public static string GetTier(ItemStack? stack)
        => stack?.Attributes?.GetString(AttrTier, "mundane") ?? "mundane";

    public static void SetTier(ItemStack stack, string tier)
        => stack.Attributes.SetString(AttrTier, tier);

    public static RelicInstance? GetInstance(ItemStack? stack)
    {
        var slot = GetSlotType(stack);
        if (slot == null) return null;
        return new RelicInstance(
            slot.Value,
            GetPrefixCode(stack),
            GetSuffixCode(stack),
            GetSeed(stack),
            IsIdentified(stack),
            GetTier(stack));
    }

    public static void WriteInstance(ItemStack stack, RelicInstance instance)
    {
        stack.Attributes.SetLong(AttrSeed, instance.Seed);
        stack.Attributes.SetString(AttrPrefix, instance.PrefixCode ?? "");
        stack.Attributes.SetString(AttrSuffix, instance.SuffixCode ?? "");
        stack.Attributes.SetBool(AttrIdentified, instance.Identified);
        stack.Attributes.SetString(AttrTier, instance.Tier);
    }
}

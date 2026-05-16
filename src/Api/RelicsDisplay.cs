using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DriftRelics.Affixes;
using DriftRelics.Modifier;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace DriftRelics.Api;

public static class RelicsDisplay
{
    public static string GetDisplayName(ItemStack stack, string fallback)
    {
        if (!RelicsUtil.IsRelic(stack)) return fallback;
        if (!RelicsUtil.IsIdentified(stack))
            return ScrambleNameGenerator.Generate(RelicsUtil.GetSeed(stack));

        return AssembleIdentifiedName(stack);
    }

    /// <summary>
    /// Returns the identified name wrapped in a richtext color tag matching the relic's tier.
    /// Plain (non-tagged) for tier <c>mundane</c>. Caller is responsible for rendering richtext.
    /// </summary>
    public static string GetDisplayNameColored(ItemStack stack, string fallback,
                                               System.Collections.Generic.IReadOnlyList<TierConfig> tiers)
    {
        var plain = GetDisplayName(stack, fallback);
        if (!RelicsUtil.IsRelic(stack) || !RelicsUtil.IsIdentified(stack)) return plain;

        var tier = RelicsUtil.GetTier(stack);
        if (tier == "mundane") return plain;

        string color = "#ffffff";
        for (int i = 0; i < tiers.Count; i++)
        {
            if (tiers[i].Code == tier) { color = tiers[i].Color; break; }
        }
        return $"<font color=\"{color}\">{plain}</font>";
    }

    private static string AssembleIdentifiedName(ItemStack stack)
    {
        var baseName = Lang.Get("driftrelics:item-" + stack.Collectible.LastCodePart());
        var prefix = RelicsUtil.GetPrefixCode(stack);
        var suffix = RelicsUtil.GetSuffixCode(stack);

        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(prefix))
        {
            sb.Append(Lang.Get("driftrelics:affix-prefix-" + prefix));
            sb.Append(' ');
        }
        sb.Append(baseName);
        if (!string.IsNullOrEmpty(suffix))
        {
            sb.Append(' ');
            sb.Append(Lang.Get("driftrelics:affix-suffix-" + suffix));
        }
        return sb.ToString();
    }

    private const string AmberHex   = "#c9882a";
    private const string EmberHex   = "#ffb957";
    private const string DividerStr = "---------------";
    private const string Bullet     = "·"; // middle dot (Latin-1; ✦ rendered as a box in VS's tooltip font)

    /// <summary>
    /// Appends the full parchment-themed tooltip block for an identified relic to <paramref name="dsc"/>:
    /// tier-colored name, tier line, amber divider, scaled stat lines, and (for drift-touched) a
    /// signature flavor line plus its scaled mods. Skips entirely for unidentified / non-relic stacks.
    /// </summary>
    public static void AppendIdentifiedTooltip(StringBuilder dsc, ItemStack stack,
                                               string fallbackName,
                                               IAffixRegistry registry)
    {
        if (!RelicsUtil.IsRelic(stack) || !RelicsUtil.IsIdentified(stack)) return;

        var tiers = registry.Tiers;
        dsc.AppendLine(GetDisplayNameColored(stack, fallbackName, tiers));

        var tierCode = RelicsUtil.GetTier(stack);
        var tierName = Lang.Get("driftrelics:tier-" + tierCode);
        var hex = TierColor(tiers, tierCode);
        dsc.AppendLine($"<font color=\"{hex}\">{tierName}</font>");

        AppendDivider(dsc);

        var pool = registry.BuildPool();
        var tierCfg = pool.GetTier(tierCode);
        double scale = tierCfg?.ValueScale ?? 1.0;

        var prefixCode = RelicsUtil.GetPrefixCode(stack);
        var suffixCode = RelicsUtil.GetSuffixCode(stack);
        if (!string.IsNullOrEmpty(prefixCode))
        {
            var a = registry.GetByCode(prefixCode);
            if (a != null) foreach (var m in a.Mods) AppendStatLine(dsc, m, scale);
        }
        if (!string.IsNullOrEmpty(suffixCode))
        {
            var a = registry.GetByCode(suffixCode);
            if (a != null) foreach (var m in a.Mods) AppendStatLine(dsc, m, scale);
        }

        if (tierCfg?.Signature ?? false)
        {
            var slotKey = (RelicsUtil.GetSlotType(stack) ?? RelicSlotType.Trinket)
                          .ToString().ToLowerInvariant();
            var sig = registry.GetSignatureFor(slotKey);
            if (sig != null)
            {
                AppendDivider(dsc);
                var sigFlavor = string.IsNullOrEmpty(sig.LangKey) ? sig.Code : Lang.Get(sig.LangKey);
                var tierColor = TierColor(tiers, tierCode);
                dsc.AppendLine($"<font color=\"{tierColor}\"><i>{sigFlavor}</i></font>");
                foreach (var m in sig.Mods) AppendStatLine(dsc, m, scale);
            }
        }
    }

    private static void AppendDivider(StringBuilder dsc)
        => dsc.AppendLine($"<font color=\"{AmberHex}\">{DividerStr}</font>");

    private static void AppendStatLine(StringBuilder dsc, ModifierEntry m, double scale)
    {
        var scaled = ModifierScaling.Scale(m, scale);
        var statName = Lang.Get("driftrelics:stat-" + m.Key);
        var valueStr = m.Op == ModifierOp.Mul
            ? FormatPercent(scaled.Value)
            : FormatAdd(scaled.Value);
        dsc.AppendLine(
            $"<font color=\"{AmberHex}\">{Bullet}</font> {statName}: " +
            $"<font color=\"{EmberHex}\">{valueStr}</font>");
    }

    private static string FormatAdd(double v)
    {
        var rounded = (v == (int)v) ? ((int)v).ToString(CultureInfo.InvariantCulture)
                                    : v.ToString("0.##", CultureInfo.InvariantCulture);
        return v >= 0 ? "+" + rounded : rounded;
    }

    private static string FormatPercent(double v)
    {
        var pct = v * 100;
        var s = pct.ToString("0.#", CultureInfo.InvariantCulture);
        return pct >= 0 ? "+" + s + "%" : s + "%";
    }

    private static string TierColor(IReadOnlyList<TierConfig> tiers, string tierCode)
    {
        for (int i = 0; i < tiers.Count; i++)
            if (tiers[i].Code == tierCode) return tiers[i].Color;
        return "#ffffff";
    }
}

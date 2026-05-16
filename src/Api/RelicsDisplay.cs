using System.Text;
using DriftRelics.Affixes;
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
}

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
        {
            return ScrambleNameGenerator.Generate(RelicsUtil.GetSeed(stack));
        }

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

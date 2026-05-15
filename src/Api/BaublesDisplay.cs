using System.Text;
using Baubles.Affixes;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace Baubles.Api;

public static class BaublesDisplay
{
    public static string GetDisplayName(ItemStack stack, string fallback)
    {
        if (!BaublesUtil.IsBauble(stack)) return fallback;

        if (!BaublesUtil.IsIdentified(stack))
        {
            return ScrambleNameGenerator.Generate(BaublesUtil.GetSeed(stack));
        }

        var baseName = Lang.Get("baubles:item-" + stack.Collectible.LastCodePart());
        var prefix = BaublesUtil.GetPrefixCode(stack);
        var suffix = BaublesUtil.GetSuffixCode(stack);

        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(prefix))
        {
            sb.Append(Lang.Get("baubles:affix-prefix-" + prefix));
            sb.Append(' ');
        }
        sb.Append(baseName);
        if (!string.IsNullOrEmpty(suffix))
        {
            sb.Append(' ');
            sb.Append(Lang.Get("baubles:affix-suffix-" + suffix));
        }
        return sb.ToString();
    }
}

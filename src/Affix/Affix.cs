using System.Collections.Generic;
using System.Linq;
using Baubles.Api;
using Baubles.Modifier;

namespace Baubles.Affixes;

public sealed class Affix
{
    public string Code { get; set; } = "";
    public string LangKey { get; set; } = "";
    public AffixKind Kind { get; set; }
    public int Weight { get; set; } = 10;
    public BaubleSlotType[]? AllowedSlots { get; set; }
    public List<ModifierEntry> Mods { get; set; } = new();

    public bool Allows(BaubleSlotType slot)
        => AllowedSlots == null || AllowedSlots.Contains(slot);
}

using System.Collections.Generic;
using System.Linq;
using DriftRelics.Api;
using DriftRelics.Modifier;

namespace DriftRelics.Affixes;

public sealed class Affix
{
    public string Code { get; set; } = "";
    public string LangKey { get; set; } = "";
    public AffixKind Kind { get; set; }
    public int Weight { get; set; } = 10;
    public RelicSlotType[]? AllowedSlots { get; set; }
    public List<ModifierEntry> Mods { get; set; } = new();

    public bool Allows(RelicSlotType slot)
        => AllowedSlots == null || AllowedSlots.Contains(slot);
}

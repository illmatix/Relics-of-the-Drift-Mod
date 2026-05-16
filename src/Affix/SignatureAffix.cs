using System.Collections.Generic;
using DriftRelics.Modifier;

namespace DriftRelics.Affixes;

public sealed class SignatureAffix
{
    public string Code { get; set; } = "";
    public string LangKey { get; set; } = "";
    public List<ModifierEntry> Mods { get; set; } = new();
}

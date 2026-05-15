namespace Baubles.Modifier;

public sealed class ModifierEntry
{
    public string Key { get; set; } = "";
    public double Value { get; set; }
    public ModifierOp Op { get; set; } = ModifierOp.Add;
}

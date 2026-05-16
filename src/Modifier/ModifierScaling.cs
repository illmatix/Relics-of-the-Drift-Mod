using System;

namespace DriftRelics.Modifier;

public static class ModifierScaling
{
    public static ModifierEntry Scale(ModifierEntry source, double scale)
    {
        if (scale == 1.0) return source;
        double newValue = source.Op == ModifierOp.Add
            ? Math.Round(source.Value * scale, MidpointRounding.AwayFromZero)
            : source.Value * scale;
        return new ModifierEntry { Key = source.Key, Value = newValue, Op = source.Op };
    }
}

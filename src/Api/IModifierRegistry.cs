using DriftRelics.Modifier;
using Vintagestory.API.Common;

namespace DriftRelics.Api;

public delegate void ModifierApplyDelegate(EntityPlayer player, double value, ModifierOp op, string code, bool apply);

public interface IModifierRegistry
{
    void Register(string key, ModifierApplyDelegate handler);
    bool TryApply(EntityPlayer player, ModifierEntry entry, string code);
    bool TryRemove(EntityPlayer player, ModifierEntry entry, string code);
}

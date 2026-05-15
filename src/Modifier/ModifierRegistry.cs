using System.Collections.Generic;
using Baubles.Api;
using Vintagestory.API.Common;

namespace Baubles.Modifier;

public sealed class ModifierRegistry : IModifierRegistry
{
    private readonly Dictionary<string, ModifierApplyDelegate> handlers = new();

    // api is reserved for future use (e.g. world-aware handlers in Task 15+);
    // currently the canonical handlers go straight through EntityPlayer.Stats.
    public ModifierRegistry(ICoreAPI api)
    {
        _ = api;
        // v1 canonical keys → EntityPlayer.Stats
        Register("moveSpeed",          MakeStatHandler("walkspeed"));
        Register("maxHealth",          MakeStatHandler("maxhealth"));
        Register("meleeDamage",        MakeStatHandler("meleeWeaponsDamage"));
        Register("rangedDamage",       MakeStatHandler("rangedWeaponsDamage"));
        Register("hungerRate",         MakeStatHandler("hungerrate"));
        // coldResist / heatResist bias the body-temperature comfort band.
        Register("coldResist",         MakeStatHandler("bodyTempHotMin"));
        Register("heatResist",         MakeStatHandler("bodyTempHotMax"));
        Register("rangedDamageResist", MakeStatHandler("rangedWeaponsDamageReceived"));
    }

    public void Register(string key, ModifierApplyDelegate handler)
        => handlers[key] = handler;

    public bool TryApply(EntityPlayer player, ModifierEntry entry, string code)
    {
        if (!handlers.TryGetValue(entry.Key, out var h)) return false;
        h(player, entry.Value, entry.Op, code, apply: true);
        return true;
    }

    public bool TryRemove(EntityPlayer player, ModifierEntry entry, string code)
    {
        if (!handlers.TryGetValue(entry.Key, out var h)) return false;
        h(player, entry.Value, entry.Op, code, apply: false);
        return true;
    }

    private static ModifierApplyDelegate MakeStatHandler(string statCategory) =>
        (EntityPlayer player, double value, ModifierOp op, string code, bool apply) =>
        {
            if (apply)
            {
                // For ModifierOp.Mul we use the value as a multiplicative delta (e.g. 0.05 = +5%).
                // VS's Stats system stacks all named modifiers additively into the category
                // value, which already approximates Mul-as-bonus-fraction.
                player.Stats.Set(statCategory, code, (float)value, persistent: true);
            }
            else
            {
                player.Stats.Remove(statCategory, code);
            }
        };
}

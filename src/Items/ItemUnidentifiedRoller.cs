using System;
using DriftRelics.Api;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace DriftRelics.Items;

public class ItemUnidentifiedRoller : Item
{
    public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity,
        BlockSelection blockSel, EntitySelection entitySel, bool firstEvent,
        ref EnumHandHandling handling)
    {
        if (api.Side != EnumAppSide.Server)
        {
            handling = EnumHandHandling.PreventDefault;
            return;
        }

        var player = (byEntity as EntityPlayer)?.Player as IServerPlayer;
        if (player == null) { handling = EnumHandHandling.NotHandled; return; }

        var stack = slot.Itemstack;
        var current = (RelicSlotType)(stack.Attributes.GetInt("rollerSlotType", 0));
        var next = (RelicSlotType)(((int)current + 1) % 3);
        stack.Attributes.SetInt("rollerSlotType", (int)next);
        slot.MarkDirty();

        var modSystem = api.ModLoader.GetModSystem<DriftRelicsModSystem>();
        var seed = (long)Guid.NewGuid().GetHashCode() ^ ((long)player.Entity.EntityId << 32);
        var rolled = modSystem.Api.RollUnidentifiedRelic(current, seed);
        if (rolled != null)
        {
            if (!player.InventoryManager.TryGiveItemstack(rolled, true))
            {
                api.World.SpawnItemEntity(rolled, player.Entity.Pos.XYZ);
            }
            player.SendMessage(0, $"Rolled {current} (seed {seed:X}). Next press → {next}.",
                EnumChatType.Notification);
        }

        handling = EnumHandHandling.PreventDefault;
    }
}

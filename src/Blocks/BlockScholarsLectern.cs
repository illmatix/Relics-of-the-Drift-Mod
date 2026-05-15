using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Baubles.Blocks;

public class BlockScholarsLectern : Block
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer,
        BlockSelection blockSel)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position)
                is BEScholarsLectern be)
        {
            be.OnPlayerInteract(byPlayer);
            return true;
        }
        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }
}

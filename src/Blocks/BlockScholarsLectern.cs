using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace DriftRelics.Blocks;

public class BlockScholarsLectern : Block
{
    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer,
        BlockSelection blockSel)
    {
        if (world.BlockAccessor.GetBlockEntity(blockSel.Position)
                is BEScholarsLectern be)
        {
            return be.OnPlayerRightClick(byPlayer, blockSel);
        }
        return base.OnBlockInteractStart(world, byPlayer, blockSel);
    }
}

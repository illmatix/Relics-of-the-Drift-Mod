using Vintagestory.API.Common;

namespace Baubles;

public class BaublesModSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.Logger.Notification("[Baubles] mod system starting");
    }
}

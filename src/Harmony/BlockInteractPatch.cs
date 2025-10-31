using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VsQuest.Harmony
{
    [HarmonyPatch(typeof(Block), "OnBlockInteractStart")]
    public class BlockInteractPatch
    {
        public static void Postfix(Block __instance, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, bool __result)
        {
            if (world.Api.Side != EnumAppSide.Client || blockSel == null) return;

            if (__result)
            {
                return;
            }

            var capi = world.Api as ICoreClientAPI;
            if (capi == null) return;

            capi.Network.GetChannel("vsquest").SendPacket(new VanillaBlockInteractMessage()
            {
                Position = blockSel.Position,
                BlockCode = __instance.Code.Path
            });
        }
    }
}
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.MathTools;

namespace VsQuest.Harmony
{
    [HarmonyPatch(typeof(Block), "OnBlockInteractStart")]
    public class Block_OnBlockInteractStart_Patch
    {
        public static void Postfix(Block __instance, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            // Only proceed if this is server-side and we have a valid player
            if (world.Side != EnumAppSide.Server || byPlayer == null)
            {
                return;
            }

            var sapi = world.Api as ICoreServerAPI;
            if (sapi == null) return;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem == null) return;

            sapi.Logger.Debug("Interacted with block!");

            // Call the existing OnBlockUsed method in all active quests for this player
            var activeQuests = questSystem.getPlayerQuests(byPlayer.PlayerUID, sapi);
            foreach (var quest in activeQuests)
            {
                // Get the block code and position for the interaction
                string blockCode = __instance.Code.Path;
                int[] position = new int[] { blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z };
                sapi.Logger.Debug(blockSel.Position.X.ToString() + " " + blockSel.Position.Y + " " + blockSel.Position.Z);
                quest.OnBlockUsed(blockCode, position, byPlayer);
            }
        }
    }
}
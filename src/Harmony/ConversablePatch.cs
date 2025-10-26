
using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;

namespace VsQuest.Harmony
{
    [HarmonyPatch(typeof(EntityBehaviorConversable), "Controller_DialogTriggers")]
    public class EntityBehaviorConversable_Controller_DialogTriggers_Patch
    {
        public static void Postfix(EntityBehaviorConversable __instance, EntityAgent triggeringEntity, string value, JsonObject data)
        {
            var sapi = __instance.entity.Api as ICoreServerAPI;
            if (sapi == null) return;

            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem == null) return;

            var player = (triggeringEntity as EntityPlayer)?.Player as IServerPlayer;
            if (player == null) return;

            var actionStrings = value.Split(';').Select(s => s.Trim());

            foreach (var actionString in actionStrings)
            {
                if (string.IsNullOrWhiteSpace(actionString)) continue;

                var matches = Regex.Matches(actionString, "(?:'([^']*)')|([^\\s]+)");
                if (matches.Count == 0) continue;

                var actionId = matches[0].Value;
                var args = new List<string>();

                for (int i = 1; i < matches.Count; i++)
                {
                    // Check which group was successful. Group 1 is for quoted, Group 2 for unquoted.
                    if (matches[i].Groups[1].Success)
                    {
                        args.Add(matches[i].Groups[1].Value);
                    }
                    else
                    {
                        args.Add(matches[i].Groups[2].Value);
                    }
                }

                if (questSystem.ActionRegistry.TryGetValue(actionId, out var action))
                {
                    var message = new QuestAcceptedMessage { questGiverId = __instance.entity.EntityId, questId = "dialog-action" };
                    action.Invoke(sapi, message, player, args.ToArray());
                }
            }
        }
    }
}

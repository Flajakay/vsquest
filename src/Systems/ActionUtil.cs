using Newtonsoft.Json;
using System;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VsQuest
{
    public class ActionUtil
    {
        private ActionUtil()
        {
        }

        public static void SpawnEntities(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            foreach (var code in args)
            {
                var type = sapi.World.GetEntityType(new AssetLocation(code));
                if (type == null)
                {
                    throw new QuestException(string.Format("Tried to spawn {0} for quest {1} but could not find the entity type!", code, message.questId));
                }
                var entity = sapi.World.ClassRegistry.CreateEntity(type);
                entity.ServerPos = sapi.World.GetEntityById(message.questGiverId).ServerPos.Copy();
                sapi.World.SpawnEntity(entity);
            }
        }

        public static void SpawnAnyOfEntities(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            var code = args[sapi.World.Rand.Next(0, args.Length)];
            var type = sapi.World.GetEntityType(new AssetLocation(code));
            if (type == null)
            {
                throw new QuestException(string.Format("Tried to spawn {0} for quest {1} but could not find the entity type!", code, message.questId));
            }
            var entity = sapi.World.ClassRegistry.CreateEntity(type);
            entity.ServerPos = sapi.World.GetEntityById(message.questGiverId).ServerPos.Copy();
            sapi.World.SpawnEntity(entity);
        }

        public static void RecruitEntity(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            var recruit = sapi.World.GetEntityById(message.questGiverId);
            recruit.WatchedAttributes.SetDouble("employedSince", sapi.World.Calendar.TotalHours);
            recruit.WatchedAttributes.SetString("guardedPlayerUid", byPlayer.PlayerUID);
            recruit.WatchedAttributes.SetBool("commandSit", false);
            recruit.WatchedAttributes.MarkPathDirty("guardedPlayerUid");
        }

                public static void GiveItem(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
                {
                    if (args.Length < 2)
                    {
                        throw new QuestException("The 'giveitem' action requires at least 2 arguments: itemCode and amount.");
                    }
        
                    string code = args[0];
                    int amount = int.Parse(args[1]);
        
                    CollectibleObject item = sapi.World.GetItem(new AssetLocation(code));
                    if (item == null)
                    {
                        item = sapi.World.GetBlock(new AssetLocation(code));
                    }
                    if (item == null)
                    {
                        throw new QuestException(string.Format("Could not find item {0} for quest {1}!", code, message.questId));
                    }
        
                    var stack = new ItemStack(item, amount);
        
                    // Itemizer integration
                    if (args.Length > 2)
                    {
                        stack.Attributes.SetString("itemizerName", args[2]);
                    }
                    if (args.Length > 3)
                    {
                        string desc = string.Join(" ", args, 3, args.Length - 3);
                        stack.Attributes.SetString("itemizerDesc", desc);
                    }
        
                    if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
                    {
                        sapi.World.SpawnItemEntity(stack, byPlayer.Entity.ServerPos.XYZ);
                    }
                }
        public static void CompleteQuest(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            var questSystem = sapi.ModLoader.GetModSystem<QuestSystem>();
            QuestCompletedMessage questCompletedMessage;
            switch (args.Length)
            {
                case 1:
                    questCompletedMessage = new QuestCompletedMessage() { questGiverId = long.Parse(args[1]), questId = args[0] };
                    break;
                case 2:
                    questCompletedMessage = new QuestCompletedMessage() { questGiverId = message.questGiverId, questId = args[0] };
                    break;
                default:
                    questCompletedMessage = new QuestCompletedMessage() { questGiverId = message.questGiverId, questId = message.questId };
                    break;
            }
            questSystem.OnQuestCompleted(byPlayer, questCompletedMessage, sapi);
        }

        public static void SpawnSmoke(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            SimpleParticleProperties smoke = new SimpleParticleProperties(
                    40, 60,
                    ColorUtil.ToRgba(80, 100, 100, 100),
                    new Vec3d(),
                    new Vec3d(2, 1, 2),
                    new Vec3f(-0.25f, 0f, -0.25f),
                    new Vec3f(0.25f, 0f, 0.25f),
                    0.6f,
                    -0.075f,
                    0.5f,
                    3f,
                    EnumParticleModel.Quad
                );
            var questgiver = sapi.World.GetEntityById(message.questGiverId);
            if (questgiver != null)
            {
                smoke.MinPos = questgiver.ServerPos.XYZ.AddCopy(-1.5, -0.5, -1.5);
                sapi.World.SpawnParticles(smoke);
            }
        }
        public static void AddTraits(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            var traits = byPlayer.Entity.WatchedAttributes
                .GetStringArray("extraTraits", new string[0])
                .ToHashSet();
            traits.AddRange(args);
            byPlayer.Entity.WatchedAttributes
                .SetStringArray("extraTraits", traits.ToArray());
        }
        public static void RemoveTraits(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            var traits = byPlayer.Entity.WatchedAttributes
                .GetStringArray("extraTraits", new string[0])
                .ToHashSet();
            args.Foreach(trait => traits.Remove(trait));
            byPlayer.Entity.WatchedAttributes
                .SetStringArray("extraTraits", traits.ToArray());
        }
        public static void ServerCommand(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length > 0)
            {
                string command = string.Join(" ", args);
                if (!command.StartsWith("/"))
                {
                    command = "/" + command;
                }
                sapi.InjectConsole(command);
            }
        }

        public static void PlayerCommand(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {
            if (args.Length > 0)
            {
                string command = string.Join(" ", args);
                sapi.Network.GetChannel("vsquest").SendPacket(new ExecutePlayerCommandMessage() { Command = command }, byPlayer);
            }
        }

        public static void GiveActionItem(ICoreServerAPI sapi, QuestMessage message, IServerPlayer byPlayer, string[] args)
        {

            sapi.Logger.Debug("Hello!");

            if (args.Length < 1)
            {
                throw new QuestException("The 'giveactionitem' action requires at least 1 argument: actionItemId.");
            }
            var itemSystem = sapi.ModLoader.GetModSystem<ItemSystem>();
            if (itemSystem.ActionItemRegistry.TryGetValue(args[0], out var actionItem))
            {
                sapi.Logger.Debug(actionItem.itemCode);

                CollectibleObject collectible = sapi.World.GetItem(new AssetLocation(actionItem.itemCode));
                if (collectible == null)
                {
                    collectible = sapi.World.GetBlock(new AssetLocation(actionItem.itemCode));
                }
                if (collectible != null)
                {
                    var stack = new ItemStack(collectible);
                    stack.Attributes.SetString("itemizerName", actionItem.name);
                    stack.Attributes.SetString("itemizerDesc", actionItem.description);
                    stack.Attributes.SetString("vsquest:actions", JsonConvert.SerializeObject(actionItem.actions));
                    if (!byPlayer.InventoryManager.TryGiveItemstack(stack))
                    {
                        sapi.World.SpawnItemEntity(stack, byPlayer.Entity.ServerPos.XYZ);
                    }
                }
            }
        }
    }


    public class QuestException : Exception
    {
        public QuestException() { }

        public QuestException(string message) : base(message) { }

        public QuestException(string message, Exception inner) : base(message, inner) { }
    }
}
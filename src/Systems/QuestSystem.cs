using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.API.MathTools;
using vsquest.src.Systems.Actions;

namespace VsQuest
{
    public delegate void QuestAction(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args);
    public class QuestSystem : ModSystem
    {
        public Dictionary<string, Quest> QuestRegistry { get; private set; } = new Dictionary<string, Quest>();
        public Dictionary<string, QuestAction> ActionRegistry { get; private set; } = new Dictionary<string, QuestAction>();
        public Dictionary<string, ActiveActionObjective> ActionObjectiveRegistry { get; private set; } = new Dictionary<string, ActiveActionObjective>();
        private ConcurrentDictionary<string, List<ActiveQuest>> playerQuests = new ConcurrentDictionary<string, List<ActiveQuest>>();
        public QuestConfig Config { get; set; }
        private ICoreAPI api;
        public override void Start(ICoreAPI api)
        {
            this.api = api;
            base.Start(api);

            var harmony = new HarmonyLib.Harmony("vsquest");
            harmony.PatchAll();

            api.RegisterEntityBehaviorClass("questgiver", typeof(EntityBehaviorQuestGiver));

            api.RegisterItemClass("ItemDebugTool", typeof(ItemDebugTool));


            ActionObjectiveRegistry.Add("plantflowers", new NearbyFlowersActionObjective());
            ActionObjectiveRegistry.Add("hasAttribute", new PlayerHasAttributeActionObjective());
            ActionObjectiveRegistry.Add("interactat", new InteractAtCoordinateObjective());

            try
            {
                Config = api.LoadModConfig<QuestConfig>("questconfig.json");
                if (Config != null)
                {
                    api.Logger.Notification("Mod Config successfully loaded.");
                }
                else
                {
                    api.Logger.Notification("No Mod Config specified. Falling back to default settings");
                    Config = new QuestConfig();
                }
            }
            catch
            {
                Config = new QuestConfig();
                api.Logger.Error("Failed to load custom mod configuration. Falling back to default settings!");
            }
            finally
            {
                api.StoreModConfig(Config, "questconfig.json");
            }
        }

        public override void StartClientSide(ICoreClientAPI capi)
        {
            base.StartClientSide(capi);

            capi.Network.RegisterChannel("vsquest")
                .RegisterMessageType<QuestAcceptedMessage>()
                .RegisterMessageType<QuestCompletedMessage>()
                .RegisterMessageType<QuestInfoMessage>().SetMessageHandler<QuestInfoMessage>(message => OnQuestInfoMessage(message, capi))
                .RegisterMessageType<ExecutePlayerCommandMessage>().SetMessageHandler<ExecutePlayerCommandMessage>(message => OnExecutePlayerCommand(message, capi))
                .RegisterMessageType<VanillaBlockInteractMessage>()
                .RegisterMessageType<ShowNotificationMessage>().SetMessageHandler<ShowNotificationMessage>(message => capi.ShowChatMessage(message.Notification));
        }

        public override void StartServerSide(ICoreServerAPI sapi)
        {
            base.StartServerSide(sapi);

            sapi.Network.RegisterChannel("vsquest")
                .RegisterMessageType<QuestAcceptedMessage>().SetMessageHandler<QuestAcceptedMessage>((player, message) => OnQuestAccepted(player, message, sapi))
                .RegisterMessageType<QuestCompletedMessage>().SetMessageHandler<QuestCompletedMessage>((player, message) => OnQuestCompleted(player, message, sapi))
                .RegisterMessageType<QuestInfoMessage>()
                .RegisterMessageType<ExecutePlayerCommandMessage>()
                .RegisterMessageType<VanillaBlockInteractMessage>().SetMessageHandler<VanillaBlockInteractMessage>((player, message) => OnVanillaBlockInteract(player, message, sapi))
                .RegisterMessageType<ShowNotificationMessage>();

            ActionRegistry.Add("despawnquestgiver", (api, message, byPlayer, args) => api.World.RegisterCallback(dt => api.World.GetEntityById(message.questGiverId).Die(EnumDespawnReason.Removed), int.Parse(args[0])));
            ActionRegistry.Add("playsound", (api, message, byPlayer, args) => api.World.PlaySoundFor(new AssetLocation(args[0]), byPlayer));
            ActionRegistry.Add("spawnentities", ActionUtil.SpawnEntities);
            ActionRegistry.Add("spawnany", ActionUtil.SpawnAnyOfEntities);
            ActionRegistry.Add("spawnsmoke", ActionUtil.SpawnSmoke);
            ActionRegistry.Add("recruitentity", ActionUtil.RecruitEntity);
            ActionRegistry.Add("healplayer", (api, message, byPlayer, args) => byPlayer.Entity.ReceiveDamage(new DamageSource() { Type = EnumDamageType.Heal }, 100));
            ActionRegistry.Add("addplayerattribute", (api, message, byPlayer, args) => byPlayer.Entity.WatchedAttributes.SetString(args[0], args[1]));
            ActionRegistry.Add("removeplayerattribute", (api, message, byPlayer, args) => byPlayer.Entity.WatchedAttributes.RemoveAttribute(args[0]));
            ActionRegistry.Add("completequest", ActionUtil.CompleteQuest);
            ActionRegistry.Add("acceptquest", (api, message, byPlayer, args) => OnQuestAccepted(byPlayer, new QuestAcceptedMessage() { questGiverId = long.Parse(args[0]), questId = args[1] }, api));
            ActionRegistry.Add("giveitem", ActionUtil.GiveItem);
            ActionRegistry.Add("addtraits", ActionUtil.AddTraits);
            ActionRegistry.Add("removetraits", ActionUtil.RemoveTraits);
            ActionRegistry.Add("servercommand", ActionUtil.ServerCommand);
            ActionRegistry.Add("playercommand", ActionUtil.PlayerCommand);
            ActionRegistry.Add("giveactionitem", ActionUtil.GiveActionItem);

            sapi.Event.GameWorldSave += () => OnSave(sapi);
            sapi.Event.PlayerDisconnect += player => OnDisconnect(player, sapi);
            sapi.Event.OnEntityDeath += (entity, dmgSource) => OnEntityDeath(entity, dmgSource, sapi);
            sapi.Event.DidBreakBlock += (byPlayer, blockId, blockSel) => getPlayerQuests(byPlayer?.PlayerUID, sapi).ForEach(quest => quest.OnBlockBroken(sapi.World.GetBlock(blockId)?.Code.Path, new int[] { blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z }, byPlayer));
            sapi.Event.DidPlaceBlock += (byPlayer, oldBlockId, blockSel, itemstack) => getPlayerQuests(byPlayer?.PlayerUID, sapi).ForEach(quest => quest.OnBlockPlaced(sapi.World.BlockAccessor.GetBlock(blockSel.Position)?.Code.Path, new int[] { blockSel.Position.X, blockSel.Position.Y, blockSel.Position.Z }, byPlayer));

            sapi.ChatCommands.GetOrCreate("giveactionitem")
                .WithDescription("Gives a player an action item defined in itemconfig.json.")
                .RequiresPrivilege(Privilege.give)
                .WithArgs(sapi.ChatCommands.Parsers.Word("itemId"), sapi.ChatCommands.Parsers.OptionalInt("amount", 1))
                .HandleWith(OnGiveActionItemCommand);
        }
        private TextCommandResult OnGiveActionItemCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = (IServerPlayer)args.Caller.Player;
            if (player == null) return TextCommandResult.Error("This command can only be run by a player.");

            string itemId = (string)args[0];
            int amount = (int)args[1];

            var itemSystem = api.ModLoader.GetModSystem<ItemSystem>();

            if (!itemSystem.ActionItemRegistry.TryGetValue(itemId, out var actionItem))
            {
                return TextCommandResult.Error($"Action item with ID '{itemId}' not found in itemconfig.json.");
            }

            CollectibleObject collectible = api.World.GetItem(new AssetLocation(actionItem.itemCode));
            if (collectible == null)
            {
                collectible = api.World.GetBlock(new AssetLocation(actionItem.itemCode));
            }

            if (collectible == null)
            {
                return TextCommandResult.Error($"Could not find base item/block with code '{actionItem.itemCode}'.");
            }

            var stack = new ItemStack(collectible, amount);
            stack.Attributes.SetString("itemizerName", actionItem.name);
            stack.Attributes.SetString("itemizerDesc", actionItem.description);
            stack.Attributes.SetString("vsquest:actions", JsonConvert.SerializeObject(actionItem.actions));

            if (!player.InventoryManager.TryGiveItemstack(stack))
            {
                api.World.SpawnItemEntity(stack, player.Entity.ServerPos.XYZ);
            }

            return TextCommandResult.Success($"Successfully gave {amount}x {actionItem.name}.");
        }
        

        public override void AssetsLoaded(ICoreAPI api)
        {
            base.AssetsLoaded(api);
            foreach (var mod in api.ModLoader.Mods)
            {
                api.Assets
                    .GetMany<List<Quest>>(api.Logger, "config/quests", mod.Info.ModID)
                    .SelectMany(pair => pair.Value)
                    .Foreach(quest => QuestRegistry.Add(quest.id, quest));
            }
        }

        public List<ActiveQuest> getPlayerQuests(string playerUID, ICoreServerAPI sapi)
        {
            return playerQuests.GetOrAdd(playerUID, (val) => loadPlayerQuests(sapi, val));
        }

        private void OnEntityDeath(Entity entity, DamageSource damageSource, ICoreServerAPI sapi)
        {
            if (damageSource?.SourceEntity is EntityPlayer player)
            {
                getPlayerQuests(player.PlayerUID, sapi).ForEach(quest => quest.OnEntityKilled(entity.Code.Path, player.Player));
            }
        }

        private void OnDisconnect(IServerPlayer byPlayer, ICoreServerAPI sapi)
        {
            if (playerQuests.TryGetValue(byPlayer.PlayerUID, out var activeQuests))
            {
                savePlayerQuests(sapi, byPlayer.PlayerUID, activeQuests);
                playerQuests.Remove(byPlayer.PlayerUID);
            }
        }

        private void OnSave(ICoreServerAPI sapi)
        {
            foreach (var player in playerQuests)
            {
                savePlayerQuests(sapi, player.Key, player.Value);
            }
        }

        private void savePlayerQuests(ICoreServerAPI sapi, string playerUID, List<ActiveQuest> activeQuests)
        {
            sapi.WorldManager.SaveGame.StoreData<List<ActiveQuest>>(String.Format("quests-{0}", playerUID), activeQuests);
        }
        private List<ActiveQuest> loadPlayerQuests(ICoreServerAPI sapi, string playerUID)
        {
            try
            {
                return sapi.WorldManager.SaveGame.GetData<List<ActiveQuest>>(String.Format("quests-{0}", playerUID), new List<ActiveQuest>());
            }
            catch (ProtoException)
            {
                sapi.Logger.Error("Could not load quests for player with id {0}, corrupted quests will be deleted.", playerUID);
                return new List<ActiveQuest>();
            }
        }

        private void OnQuestAccepted(IServerPlayer fromPlayer, QuestAcceptedMessage message, ICoreServerAPI sapi)
        {
            var quest = QuestRegistry[message.questId];
            var killTrackers = new List<EventTracker>();
            foreach (var objective in quest.killObjectives)
            {
                var tracker = new EventTracker()
                {
                    count = 0,
                    relevantCodes = new List<string>(objective.validCodes)
                };
                killTrackers.Add(tracker);
            }
            var blockPlaceTrackers = new List<EventTracker>();
            foreach (var objective in quest.blockPlaceObjectives)
            {
                var tracker = new EventTracker()
                {
                    count = 0,
                    relevantCodes = new List<string>(objective.validCodes)
                };
                blockPlaceTrackers.Add(tracker);
            }
            var blockBreakTrackers = new List<EventTracker>();
            foreach (var objective in quest.blockBreakObjectives)
            {
                var tracker = new EventTracker()
                {
                    count = 0,
                    relevantCodes = new List<string>(objective.validCodes)
                };
                blockBreakTrackers.Add(tracker);
            }
            var activeQuest = new ActiveQuest()
            {
                questGiverId = message.questGiverId,
                questId = message.questId,
                killTrackers = killTrackers,
                blockPlaceTrackers = blockPlaceTrackers,
                blockBreakTrackers = blockBreakTrackers
            };
            getPlayerQuests(fromPlayer.PlayerUID, sapi).Add(activeQuest);
            var questgiver = sapi.World.GetEntityById(message.questGiverId);
            var key = quest.perPlayer ? String.Format("lastaccepted-{0}-{1}", quest.id, fromPlayer.PlayerUID) : String.Format("lastaccepted-{0}", quest.id);
            questgiver.WatchedAttributes.SetDouble(key, sapi.World.Calendar.TotalDays);
            questgiver.WatchedAttributes.MarkPathDirty(key);
            foreach (var action in quest.onAcceptedActions)
            {
                try
                {
                    ActionRegistry[action.id].Invoke(sapi, message, fromPlayer, action.args);
                }
                catch (Exception ex)
                {
                    sapi.Logger.Error(string.Format("Action {0} caused an Error in Quest {1}. The Error had the following message: {2}\n Stacktrace:", action.id, quest.id, ex.Message, ex.StackTrace));
                    sapi.SendMessage(fromPlayer, GlobalConstants.InfoLogChatGroup, string.Format("An error occurred during quest {0}, please check the server logs for more details.", quest.id), EnumChatType.Notification);
                }
            }
        }

        public void OnQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi)
        {
            var playerQuests = getPlayerQuests(fromPlayer.PlayerUID, sapi);
            var activeQuest = playerQuests.Find(item => item.questId == message.questId && item.questGiverId == message.questGiverId);
            if (activeQuest.isCompletable(fromPlayer))
            {
                activeQuest.completeQuest(fromPlayer);
                playerQuests.Remove(activeQuest);
                var questgiver = sapi.World.GetEntityById(message.questGiverId);
                rewardPlayer(fromPlayer, message, sapi, questgiver);
                markQuestCompleted(fromPlayer, message, questgiver);
            }
            else
            {
                sapi.SendMessage(fromPlayer, GlobalConstants.InfoLogChatGroup, "Something went wrong, the quest could not be completed", EnumChatType.Notification);
            }
        }

        private void rewardPlayer(IServerPlayer fromPlayer, QuestCompletedMessage message, ICoreServerAPI sapi, Entity questgiver)
        {
            var quest = QuestRegistry[message.questId];
            foreach (var reward in quest.itemRewards)
            {
                CollectibleObject item = sapi.World.GetItem(new AssetLocation(reward.itemCode));
                if (item == null)
                {
                    item = sapi.World.GetBlock(new AssetLocation(reward.itemCode));
                }
                var stack = new ItemStack(item, reward.amount);
                if (!fromPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    sapi.World.SpawnItemEntity(stack, questgiver.ServerPos.XYZ);
                }
            }
            List<RandomItem> randomItems = quest.randomItemRewards.items;
            for (int i = 0; i < quest.randomItemRewards.selectAmount; i++)
            {
                if (randomItems.Count <= 0) break;
                var randomItem = randomItems[sapi.World.Rand.Next(0, randomItems.Count)];
                randomItems.Remove(randomItem);
                CollectibleObject item = sapi.World.GetItem(new AssetLocation(randomItem.itemCode));
                if (item == null)
                {
                    item = sapi.World.GetBlock(new AssetLocation(randomItem.itemCode));
                }
                var stack = new ItemStack(item, sapi.World.Rand.Next(randomItem.minAmount, randomItem.maxAmount + 1));
                if (!fromPlayer.InventoryManager.TryGiveItemstack(stack))
                {
                    sapi.World.SpawnItemEntity(stack, questgiver.ServerPos.XYZ);
                }
            }
            foreach (var action in quest.actionRewards)
            {
                try
                {
                    ActionRegistry[action.id].Invoke(sapi, message, fromPlayer, action.args);
                }
                catch (Exception ex)
                {
                    sapi.Logger.Error(string.Format("Action {0} caused an Error in Quest {1}. The Error had the following message: {2}\n Stacktrace:", action.id, quest.id, ex.Message, ex.StackTrace));
                    sapi.SendMessage(fromPlayer, GlobalConstants.InfoLogChatGroup, string.Format("An error occurred during quest {0}, please check the server logs for more details.", quest.id), EnumChatType.Notification);
                }
            }
        }

        private static void markQuestCompleted(IServerPlayer fromPlayer, QuestCompletedMessage message, Entity questgiver)
        {
            var completedQuests = new HashSet<string>(questgiver.WatchedAttributes.GetStringArray(String.Format("playercompleted-{0}", fromPlayer.PlayerUID), new string[0]));
            completedQuests.Add(message.questId);
            var completedQuestsArray = new string[completedQuests.Count];
            completedQuests.CopyTo(completedQuestsArray);
            questgiver.WatchedAttributes.SetStringArray(String.Format("playercompleted-{0}", fromPlayer.PlayerUID), completedQuestsArray);
        }

        private void OnQuestInfoMessage(QuestInfoMessage message, ICoreClientAPI capi)
        {
            new QuestSelectGui(capi, message.questGiverId, message.availableQestIds, message.activeQuests, Config).TryOpen();
        }

        private void OnExecutePlayerCommand(ExecutePlayerCommandMessage message, ICoreClientAPI capi)
        {
            string command = message.Command;

            if (command.StartsWith("."))
            {
                capi.TriggerChatMessage(command);
            }
            else
            {
                capi.SendChatMessage(command);
            }
        }



        private void OnVanillaBlockInteract(IServerPlayer player, VanillaBlockInteractMessage message, ICoreServerAPI sapi)
        {
            //sapi.Logger.Debug("Message recieved" + message.Position.ToString());
            int[] position = new int[] { message.Position.X, message.Position.Y, message.Position.Z };
            getPlayerQuests(player?.PlayerUID, sapi).ForEach(quest => quest.OnBlockUsed(message.BlockCode, position, player, sapi));
        }
    }

    public class QuestConfig
    {
        public bool CloseGuiAfterAcceptingAndCompleting = true;
    }

    [ProtoContract]
    public class QuestAcceptedMessage : QuestMessage
    {
    }

    [ProtoContract]
    public class QuestCompletedMessage : QuestMessage
    {
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    [ProtoInclude(10, typeof(QuestAcceptedMessage))]
    [ProtoInclude(11, typeof(QuestCompletedMessage))]
    public abstract class QuestMessage
    {
        public string questId { get; set; }

        public long questGiverId { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class QuestInfoMessage
    {
        public long questGiverId { get; set; }
        public List<string> availableQestIds { get; set; }
        public List<ActiveQuest> activeQuests { get; set; }
    }

    [ProtoContract]
    public class ExecutePlayerCommandMessage
    {
        [ProtoMember(1)]
        public string Command { get; set; }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ShowNotificationMessage
    {
        public string Notification { get; set; }
    }
}
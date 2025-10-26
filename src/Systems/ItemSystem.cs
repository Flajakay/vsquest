using Newtonsoft.Json;
using ProtoBuf;
using Vintagestory.API.Datastructures;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VsQuest
{
    public class ItemSystem : ModSystem
    {
        private ICoreAPI api;
        private ICoreServerAPI sapi;
        private ICoreClientAPI capi;
        private QuestSystem questSystem;
        private IClientNetworkChannel clientChannel;
        private IServerNetworkChannel serverChannel;

        public Dictionary<string, ActionItem> ActionItemRegistry { get; private set; } = new Dictionary<string, ActionItem>();

        public override void StartPre(ICoreAPI api)
        {
            this.api = api;
            questSystem = api.ModLoader.GetModSystem<QuestSystem>();
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            foreach (var mod in api.ModLoader.Mods)
            {
                var assets = api.Assets.GetMany<ItemConfig>(api.Logger, "config/itemconfig", mod.Info.ModID);
                foreach (var asset in assets)
                {
                    if (asset.Value != null)
                    {
                        foreach (var actionItem in asset.Value.actionItems)
                        {
                            ActionItemRegistry[actionItem.id] = actionItem;
                        }
                    }
                }
            }
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            serverChannel = api.Network.RegisterChannel("vsquest-itemaction")
                .RegisterMessageType<ExecuteActionItemPacket>()
                .SetMessageHandler<ExecuteActionItemPacket>(OnActionPacket);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;
            clientChannel = api.Network.RegisterChannel("vsquest-itemaction")
                .RegisterMessageType<ExecuteActionItemPacket>();

            api.Event.MouseDown += OnMouseDown;
        }

        private void OnMouseDown(MouseEvent args)
        {
            if (args.Button != EnumMouseButton.Right) return;

            var slot = capi.World.Player.InventoryManager.ActiveHotbarSlot;
            if (slot?.Itemstack == null) return;

            var attributes = slot.Itemstack.Attributes;
            var actions = attributes.GetString("vsquest:actions");

            if (actions != null)
            {
                args.Handled = true;
                clientChannel.SendPacket(new ExecuteActionItemPacket());
            }
        }

        private void OnActionPacket(IServerPlayer fromPlayer, ExecuteActionItemPacket packet)
        {
            var slot = fromPlayer.InventoryManager.ActiveHotbarSlot;
            if (slot?.Itemstack == null) return;

            var attributes = slot.Itemstack.Attributes;
            var actionsJson = attributes.GetString("vsquest:actions");
            if (actionsJson == null) return;

            var actions = JsonConvert.DeserializeObject<List<ItemAction>>(actionsJson);

            foreach (var action in actions)
            {
                if (questSystem.ActionRegistry.TryGetValue(action.id, out var registeredAction))
                {
                    var message = new QuestAcceptedMessage { questGiverId = fromPlayer.Entity.EntityId, questId = "item-action" };
                    registeredAction.Invoke(sapi, message, fromPlayer, action.args);
                }
            }
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ExecuteActionItemPacket
    {
    }
}
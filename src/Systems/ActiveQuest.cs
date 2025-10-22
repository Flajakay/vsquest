using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace VsQuest
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class ActiveQuest
    {
        public long questGiverId { get; set; }
        public string questId { get; set; }
        public List<EventTracker> killTrackers { get; set; } = new List<EventTracker>();
        public List<EventTracker> blockPlaceTrackers { get; set; } = new List<EventTracker>();
        public List<EventTracker> blockBreakTrackers { get; set; } = new List<EventTracker>();
        public void OnEntityKilled(string entityCode, IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.QuestRegistry[questId];
            checkEventTrackers(killTrackers, entityCode, null, quest.killObjectives);
        }

        public void OnBlockPlaced(string blockCode, int[] position, IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.QuestRegistry[questId];
            checkEventTrackers(blockPlaceTrackers, blockCode, position, quest.blockPlaceObjectives);
        }

        public void OnBlockBroken(string blockCode, int[] position, IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.QuestRegistry[questId];
            checkEventTrackers(blockBreakTrackers, blockCode, position, quest.blockBreakObjectives);
        }

        private void checkEventTrackers(List<EventTracker> trackers, string code, int[] position, List<Objective> objectives)
        {
            foreach (var tracker in trackers)
            {
                if (position == null)
                {
                    if (trackerMatches(tracker, code))
                    {
                        tracker.count++;
                    }
                }
                else
                {
                    var index = trackers.IndexOf(tracker);
                    if (index != -1 && trackerMatches(objectives[index], tracker, code, position))
                    {
                        tracker.count++;
                    }
                }
            }
        }

        private static bool trackerMatches(EventTracker tracker, string code)
        {
            foreach (var candidate in tracker.relevantCodes)
            {
                if (candidate == code || candidate.EndsWith("*") && code.StartsWith(candidate.Remove(candidate.Length - 1)))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool trackerMatches(Objective objective, EventTracker tracker, string code, int[] position)
        {
            if (objective.positions != null && objective.positions.Count > 0)
            {
                foreach (var candidate in objective.positions)
                {
                    var pos = candidate.Split(',').Select(int.Parse).ToArray();
                    if (pos.Length == 3 && pos[0] == position[0] && pos[1] == position[1] && pos[2] == position[2])
                    {
                        foreach (var codeCandidate in objective.validCodes)
                        {
                            if (codeCandidate == code || codeCandidate.EndsWith("*") && code.StartsWith(codeCandidate.Remove(codeCandidate.Length - 1)))
                            {
                                tracker.placedPositions.Add(candidate);
                                return true;
                            }
                        }
                    }
                }
                return false;
            }
            else
            {
                foreach (var candidate in tracker.relevantCodes)
                {
                    if (candidate == code || candidate.EndsWith("*") && code.StartsWith(candidate.Remove(candidate.Length - 1)))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public bool isCompletable(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.QuestRegistry[questId];
            var activeActionObjectives = quest.actionObjectives.ConvertAll<ActiveActionObjective>(objective => questSystem.ActionObjectiveRegistry[objective.id]);
            bool completable = true;

            while (blockPlaceTrackers.Count < quest.blockPlaceObjectives.Count)
            {
                blockPlaceTrackers.Add(new EventTracker());
            }
            while (blockBreakTrackers.Count < quest.blockBreakObjectives.Count)
            {
                blockBreakTrackers.Add(new EventTracker());
            }
            while (killTrackers.Count < quest.killObjectives.Count)
            {
                killTrackers.Add(new EventTracker());
            }

            for (int i = 0; i < quest.blockPlaceObjectives.Count; i++)
            {
                if (quest.blockPlaceObjectives[i].positions != null && quest.blockPlaceObjectives[i].positions.Count > 0)
                {
                    completable &= quest.blockPlaceObjectives[i].positions.Count <= blockPlaceTrackers[i].placedPositions.Count;
                }
                else
                {
                    completable &= quest.blockPlaceObjectives[i].demand <= blockPlaceTrackers[i].count;
                }
            }
            for (int i = 0; i < quest.blockBreakObjectives.Count; i++)
            {
                completable &= quest.blockBreakObjectives[i].demand <= blockBreakTrackers[i].count;
            }
            for (int i = 0; i < quest.killObjectives.Count; i++)
            {
                completable &= quest.killObjectives[i].demand <= killTrackers[i].count;
            }
            foreach (var gatherObjective in quest.gatherObjectives)
            {
                int itemsFound = itemsGathered(byPlayer, gatherObjective);
                completable &= itemsFound >= gatherObjective.demand;
            }
            for (int i = 0; i < activeActionObjectives.Count; i++)
            {
                completable &= activeActionObjectives[i].isCompletable(byPlayer, quest.actionObjectives[i].args);
            }
            return completable;
        }

        public void completeQuest(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.QuestRegistry[questId];
            foreach (var gatherObjective in quest.gatherObjectives)
            {
                handOverItems(byPlayer, gatherObjective);
            }
            for (int i = 0; i < quest.blockPlaceObjectives.Count; i++)
            {
                if (quest.blockPlaceObjectives[i].removeAfterFinished && i < blockPlaceTrackers.Count)
                {
                    foreach (var posStr in blockPlaceTrackers[i].placedPositions)
                    {
                        var pos = posStr.Split(',').Select(int.Parse).ToArray();
                        byPlayer.Entity.World.BlockAccessor.SetBlock(0, new Vintagestory.API.MathTools.BlockPos(pos[0], pos[1], pos[2]));
                    }
                }
            }
        }

        public List<int> trackerProgress()
        {
            var result = new List<int>();
            foreach (var trackerList in new List<EventTracker>[] { killTrackers, blockPlaceTrackers, blockBreakTrackers })
            {
                if (trackerList != null)
                {
                    result.AddRange(trackerList.ConvertAll<int>(tracker => tracker.count));
                }
            }
            return result;
        }

        public List<int> gatherProgress(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.QuestRegistry[questId];
            return quest.gatherObjectives.ConvertAll<int>(gatherObjective => itemsGathered(byPlayer, gatherObjective));
        }

        public List<int> actionProgress(IPlayer byPlayer)
        {
            var questSystem = byPlayer.Entity.Api.ModLoader.GetModSystem<QuestSystem>();
            var quest = questSystem.QuestRegistry[questId];
            var activeActionObjectives = quest.actionObjectives.ConvertAll<ActiveActionObjective>(objective => questSystem.ActionObjectiveRegistry[objective.id]);
            List<int> result = new List<int>();
            for (int i = 0; i < activeActionObjectives.Count; i++)
            {
                result.AddRange(activeActionObjectives[i].progress(byPlayer, quest.actionObjectives[i].args));
            }
            return result;
        }

        public List<int> progress(IPlayer byPlayer)
        {
            var progress = gatherProgress(byPlayer);
            progress.AddRange(trackerProgress());
            progress.AddRange(actionProgress(byPlayer));
            return progress;
        }

        public int itemsGathered(IPlayer byPlayer, Objective gatherObjective)
        {
            int itemsFound = 0;
            foreach (var inventory in byPlayer.InventoryManager.Inventories.Values)
            {
                if (inventory.ClassName == GlobalConstants.creativeInvClassName)
                {
                    continue;
                }
                foreach (var slot in inventory)
                {
                    if (gatherObjectiveMatches(slot, gatherObjective))
                    {
                        itemsFound += slot.Itemstack.StackSize;
                    }
                }
            };

            return itemsFound;
        }

        private bool gatherObjectiveMatches(ItemSlot slot, Objective gatherObjective)
        {
            if (slot.Empty) return false;

            var code = slot.Itemstack.Collectible.Code.Path;
            foreach (var candidate in gatherObjective.validCodes)
            {
                if (candidate == code || candidate.EndsWith("*") && code.StartsWith(candidate.Remove(candidate.Length - 1)))
                {
                    return true;
                }
            }
            return false;
        }

        public void handOverItems(IPlayer byPlayer, Objective gatherObjective)
        {
            int itemsFound = 0;
            foreach (var inventory in byPlayer.InventoryManager.Inventories.Values)
            {
                if (inventory.ClassName == GlobalConstants.creativeInvClassName)
                {
                    continue;
                }
                foreach (var slot in inventory)
                {
                    if (gatherObjectiveMatches(slot, gatherObjective))
                    {
                        var stack = slot.TakeOut(Math.Min(slot.Itemstack.StackSize, gatherObjective.demand - itemsFound));
                        slot.MarkDirty();
                        itemsFound += stack.StackSize;
                    }
                    if (itemsFound > gatherObjective.demand) { return; }
                }
            }
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class EventTracker
    {
        public List<string> relevantCodes { get; set; } = new List<string>();
        public int count { get; set; }
        public List<string> placedPositions { get; set; } = new List<string>();
    }
}
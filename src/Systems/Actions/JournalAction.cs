
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using System.Collections.Generic;
using VsQuest;

namespace vsquest.src.Systems.Actions
{
    public class JournalActionSystem : ModSystem
    {
        public override void StartServerSide(ICoreServerAPI api)
        {
            var questSystem = api.ModLoader.GetModSystem<QuestSystem>();
            if (questSystem != null)
            {
                questSystem.ActionRegistry.Add("addjournalentry", AddJournalEntry);
            }
        }

        private void AddJournalEntry(ICoreServerAPI sapi, QuestMessage message, IServerPlayer player, string[] args)
        {
            if (args.Length < 3)
            {
                throw new QuestException("The 'addjournalentry' action requires at least 3 arguments: loreCode, title, and at least one chapter.");
            }

            var modJournal = sapi.ModLoader.GetModSystem<ModJournal>();
            if (modJournal == null)
            {
                throw new QuestException("ModJournal system not found.");
            }

            var loreCode = args[0];
            var title = args[1];
            var chapters = new List<JournalChapter>();
            for (int i = 2; i < args.Length; i++)
            {
                chapters.Add(new JournalChapter() { Text = args[i] });
            }

            var journalEntry = new JournalEntry()
            {
                Title = title,
                LoreCode = loreCode,
                Chapters = chapters
            };

            modJournal.AddOrUpdateJournalEntry(player, journalEntry);
        }
    }
}
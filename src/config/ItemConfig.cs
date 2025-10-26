
using System.Collections.Generic;

namespace VsQuest
{
    public class ItemConfig
    {
        public List<ActionItem> actionItems { get; set; } = new List<ActionItem>();
    }

    public class ActionItem
    {
        public string id { get; set; }
        public string itemCode { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public ItemAction action { get; set; }
    }

    public class ItemAction
    {
        public string id { get; set; }
        public string[] args { get; set; }
    }
}

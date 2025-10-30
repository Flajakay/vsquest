using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class InteractAtCoordinateObjective : ActiveActionObjective
    {
        // We'll use player attributes to track interaction state
        public bool isCompletable(IPlayer byPlayer, params string[] args)
        {
            // The args should contain the target position in format: "x,y,z"
            if (args.Length < 1) return false;
            
            string coordString = args[0];
            string[] coords = coordString.Split(',');
            if (coords.Length != 3) return false;
            
            int targetX, targetY, targetZ;
            if (!int.TryParse(coords[0], out targetX) || 
                !int.TryParse(coords[1], out targetY) || 
                !int.TryParse(coords[2], out targetZ)) 
            {
                return false;
            }
            
            // Check if this specific position has been interacted with by the player
            // We'll create a unique key for this interaction
            string interactionKey = $"interactat_{targetX}_{targetY}_{targetZ}";
            string completedInteractions = byPlayer.Entity.WatchedAttributes.GetString("completedInteractions", "");
            string[] completed = completedInteractions.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            
            return completed.Contains(interactionKey);
        }

        public List<int> progress(IPlayer byPlayer, params string[] args)
        {
            bool completed = isCompletable(byPlayer, args);
            return new List<int>(new int[] { completed ? 1 : 0 });
        }
    }
}

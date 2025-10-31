using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace VsQuest
{
    public class InteractAtCoordinateObjective : ActiveActionObjective
    {
        public bool isCompletable(IPlayer byPlayer, params string[] args)
        {
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

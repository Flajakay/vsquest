using ProtoBuf;
using Vintagestory.API.MathTools;

namespace VsQuest
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class VanillaBlockInteractMessage
    {
        public BlockPos Position { get; set; }
        public string BlockCode { get; set; }
    }
}

using VoxelTycoon.Tracks.Rails;

namespace AdvancedPathfinder.PathSignals
{
    public class RailToBlock
    {
        public Rail Rail { get; set; }
        public bool IsLinkedRail { get; set; }
        public bool IsBeyondPath { get; set; }

        public RailToBlock(Rail rail, bool isLinkedRail, bool isBeyondPath)
        {
            Rail = rail;
            IsLinkedRail = isLinkedRail;
            IsBeyondPath = isBeyondPath;
        }
    }
}
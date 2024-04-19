using UnityEngine;
using VoxelTycoon.Tracks.Rails;

namespace AdvancedPathfinder.RailPathfinder
{
    public record RailEdgeSettings
    {
        private const float VelocityResolution = 5f;
        private const float LengthResolution = 50f;
        private const float AccelerationResolution = 10f;
        public bool Electric { get; set; }
        public float MaxSpeed { get; set; }
        public float AccelerationSec { get; set; }
        public float Length { get; set; }
        public bool CalculateOnlyBaseScore { get; set; }

        public RailEdgeSettings()
        { 
        }

        public RailEdgeSettings(Train train)
        {
            Electric = train.Electric;
            MaxSpeed = Mathf.Ceil(train.VelocityLimit / VelocityResolution) * VelocityResolution;
            Length = Mathf.Ceil(train.Length / LengthResolution) * LengthResolution;
            CalculateOnlyBaseScore = false;
        }
    }
}
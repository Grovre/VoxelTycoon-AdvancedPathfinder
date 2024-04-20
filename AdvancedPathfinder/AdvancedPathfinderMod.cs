//#define DISABLE
using System.Collections.Generic;
using AdvancedPathfinder.PathSignals;
using AdvancedPathfinder.RailPathfinder;
using AdvancedPathfinder.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using VoxelTycoon;
using VoxelTycoon.Game.UI;
using VoxelTycoon.Modding;
using VoxelTycoon.Serialization;
using VoxelTycoon.Tracks;
using VoxelTycoon.Tracks.Rails;
using VoxelTycoon.UI;
using XMNUtils;
using Logger = VoxelTycoon.Logger;

namespace AdvancedPathfinder
{
    [SchemaVersion(2)]
    [HarmonyPatch]
    public class AdvancedPathfinderMod: Mod
    {
        private Harmony _harmony;
        private const string HarmonyID = "cz.xmnovotny.advancedpathfinder.patch";
        public static readonly Logger Logger = new Logger("AdvancedPathfinder");

        protected override void Initialize()
        {
            Harmony.DEBUG = false;
            _harmony = new Harmony(HarmonyID);
            FileLog.Reset();
            #if DISABLE
            #else
            _harmony.PatchAll();
            #endif
        }

        protected override void OnGameStarted()
        {
            Manager<RailPathfinderManager>.Initialize();
            if (SimpleManager<PathSignalManager>.Current == null)
            {
                SimpleManager<PathSignalManager>.Initialize();
            }
        }

        protected override void Deinitialize()
        {
            _harmony.UnpatchAll(HarmonyID);
            _harmony = null;
        }

        protected override void Write(StateBinaryWriter writer)
        {
            SimpleManager<PathSignalManager>.Current?.Write(writer);
        }

        protected override void Read(StateBinaryReader reader)
        {
#if DISABLE
#else
            SimpleManager<PathSignalManager>.Initialize();
//            FileLog.Log($"SchemaVersion: {SchemaVersion<AdvancedPathfinderMod>.Get()}");
            if (SchemaVersion<AdvancedPathfinderMod>.AtLeast(2))
            {
                // ReSharper disable once PossibleNullReferenceException
                SimpleManager<PathSignalManager>.Current.Read(reader);
            }
#endif
        }
        
        private static void ShowUpdatePathHint(double elapsedMilliseconds, Train train)
        {
            if (DebugSettings.VehicleUpdatePath)
            {
                FloatingHint.ShowHint(string.Format("Update path [{0} ms]", elapsedMilliseconds.ToString("F2")), color: (elapsedMilliseconds > 1.0) ? Color.red : Color.white, worldPosition: train.HeadPosition.GetValueOrDefault(), background: new PanelColor(Color.black, 0.4f));
            }
        }
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Train), "TryFindPath")]
        [HarmonyPriority(Priority.VeryLow)]
        private static bool Train_TryFindPath_prf(Train __instance, ref bool __result, TrackConnection origin, IVehicleDestination target, List<TrackConnection> result)
        {
            RailPathfinderManager manager = Manager<RailPathfinderManager>.Current;
            if (manager == null)
                return true;
            
            result.Clear();
            bool result2 = manager.FindImmediately(__instance, (RailConnection) origin, target, result);
            ShowUpdatePathHint(manager.ElapsedMilliseconds, __instance);
            __result = result2;
            
            // Don't continue with original pathfinding method
            return false;
        }

        protected override void OnLateUpdate()
        {
            PathfinderStats stats = Manager<RailPathfinderManager>.Current?.Stats;
            if (ModSettings.DebugPathfinderStats && stats != null)
            {
                GUIHelper.Draw(delegate
                {
                    GUILayout.TextArea(stats.GetStatsText());
                });
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(VehicleWindow), "Initialize")]
        private static void VehicleWindow_Initialize_pof(Vehicle vehicle)
        {
            if (vehicle is Train train)
            {
                SimpleLazyManager<TrainPathHighlighter>.Current.ShowFor(train);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(VehicleWindow), "OnClose")]
        private static void VehicleWindow_OnClose_pof(VehicleWindow __instance)
        {
            if (__instance.Vehicle is Train train)
            {
                SimpleLazyManager<TrainPathHighlighter>.Current.HideFor(train);
            }
        }
    }
}
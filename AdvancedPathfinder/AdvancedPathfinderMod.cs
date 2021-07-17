﻿using System.Collections.Generic;
using AdvancedPathfinder.PathSignals;
using AdvancedPathfinder.Rails;
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
using XMNUtils;
using Logger = VoxelTycoon.Logger;

namespace AdvancedPathfinder
{
    [SchemaVersion(1)]
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
            _harmony.PatchAll();
        }

        protected override void OnGameStarted()
        {
            Manager<RailPathfinderManager>.Initialize();
            SimpleManager<PathSignalManager>.Initialize();
        }

        protected override void Deinitialize()
        {
            _harmony.UnpatchAll(HarmonyID);
            _harmony = null;
        }

        protected override void Write(StateBinaryWriter writer)
        {
        }

        protected override void Read(StateBinaryReader reader)
        {
        }
        
/*        [HarmonyPostfix]
        [HarmonyPatch(typeof(VehiclePathfinder<TrackConnection, TrackPathNode, Train>), "FindImmediately")]
        private static void TrainPathfinder_FindImmediately_pof(VehiclePathfinder<TrackConnection, TrackPathNode, Train> __instance)
        {
            FileLog.Log("FindImmediately");
        }*/

        private static double _origMs = 0;
        private static double _newMs = 0;
        [HarmonyPostfix]
        [HarmonyPatch(typeof(Train), "TryFindPath")]
        private static void Train_TryFindPath_pof(Train __instance, ref bool __result, TrackConnection origin, IVehicleDestination target, List<TrackConnection> result)
        {
            RailPathfinderManager manager = Manager<RailPathfinderManager>.Current;
            if (manager != null)
            {
                //List<TrackConnection> resultList = new();
                result.Clear();
                bool result2 = manager.FindImmediately(__instance, (RailConnection) origin, target, result);
                _origMs += TrainPathfinder.Current.ElapsedMilliseconds;
                _newMs += manager.ElapsedMilliseconds;
                float blockUpdatesMs = SimpleLazyManager<RailBlockHelper>.Current.ElapsedMilliseconds;
 //               FileLog.Log("Finding path, result={0}, in {1}ms (original was in {2}ms)".Format(result2.ToString(), manager.ElapsedMilliseconds.ToString("N2"), TrainPathfinder.Current.ElapsedMilliseconds.ToString("N2")));
//                FileLog.Log(string.Format("Total original = {0:N0}ms, new = {1:N0}ms, ratio = {2:N1}%, block updates = {3:N2}ms", (_origMs), _newMs+blockUpdatesMs, (_origMs / (_newMs+blockUpdatesMs) * 100f), blockUpdatesMs));
                __result = result2;
            }
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(VehicleWindow), "Initialize")]
        private static void VehicleWindow_Initialize_pof(Vehicle vehicle)
        {
            if (vehicle is Train train)
            {
                LazyManager<TrainPathHighlighter>.Current.ShowFor(train);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(VehicleWindow), "OnClose")]
        private static void VehicleWindow_OnClose_pof(VehicleWindow __instance)
        {
            if (__instance.Vehicle is Train train)
            {
                LazyManager<TrainPathHighlighter>.Current.HideFor(train);
            }
        }
    }
}
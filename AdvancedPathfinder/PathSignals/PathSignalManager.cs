﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AdvancedPathfinder.UI;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using VoxelTycoon;
using VoxelTycoon.Tracks;
using VoxelTycoon.Tracks.Rails;
using XMNUtils;

namespace AdvancedPathfinder.PathSignals
{
    [HarmonyPatch]
    public class PathSignalManager: SimpleManager<PathSignalManager>
    {
        private readonly Dictionary<RailSignal, PathSignalData> _pathSignals = new();
        private readonly Dictionary<RailBlock, RailBlockData> _railBlocks = new();
        private readonly HashSet<RailSignal> _changedStates = new(); //list of signals with changed states (for performance)
        private readonly Dictionary<Train, RailSignal> _passedSignals = new();  //for delay of passing signal 
        private readonly Dictionary<PathCollection, Train> _pathToTrain = new();

        public RailSignalState GetSignalState(RailSignal signal)
        {
            if (!signal.IsBuilt)
                return RailSignalState.Green;
            if (!_pathSignals.TryGetValue(signal, out PathSignalData data))
                throw new InvalidOperationException("No data for signal.");
            return data.GetSignalState();
        }
        
        [NotNull]
        internal PathSignalData GetPathSignalData(RailSignal signal)
        {
            if (!_pathSignals.TryGetValue(signal, out PathSignalData signalData) || signalData == null)
                throw new InvalidOperationException("Signal data not found");
            return signalData;
        }
        
        protected override void OnInitialize()
        {
            Stopwatch sw = Stopwatch.StartNew();
            base.OnInitialize();
            FindBlocksAndSignals();
            sw.Stop();
            FileLog.Log(string.Format("Path signals initialized in {0:N3}ms, found signals: {1:N0}, found blocks: {2:N0}", sw.ElapsedTicks / 10000f, _pathSignals.Count, _railBlocks.Count));
        }

        private void TrainPassedSignal(Train train, RailSignal signal)
        {
            FileLog.Log("Train passed signal");
            if (!_pathSignals.TryGetValue(signal, out PathSignalData data))
                throw new InvalidOperationException("No data for signal.");
            data.TrainPassedSignal(train);
        }

        private void FindBlocksAndSignals()
        {
            Dictionary<RailBlock, int> blockSignalCounts = new();
            HashSet<RailSignal> signals = TrackHelper.GetAllRailSignals();
            foreach (RailSignal signal in signals)
            {
                if (!signal.IsBuilt) continue;
                
                RailBlock block = signal.Connection?.InnerConnection.Block;
                if (block == null) continue;
                
                PathRailBlockData data = GetOrCreateRailBlockData(block);
                data.InboundSignals.Add(signal, null);
                RailSignal oppositeSignal = signal.Connection.InnerConnection.Signal; 
                if (oppositeSignal != null)
                {
                    data.OutboundSignals.Add(oppositeSignal);
                }
            }
            
            DetectSimpleBlocks();
            CreatePathSignalsData();
        }

        /**
         * Remove blocks, that have no switch in the block and have no chain signal
         */
        private void DetectSimpleBlocks()
        {
            List<KeyValuePair<RailBlock, RailBlockData>> toConvert = new List<KeyValuePair<RailBlock, RailBlockData>>();
            HashSet<RailSignal> signalsToCheck = new();
            foreach (KeyValuePair<RailBlock,RailBlockData> pair in _railBlocks)
            {
                PathRailBlockData blockData = (PathRailBlockData) pair.Value;
                int inbCount = blockData.InboundSignals.Count;
                if (inbCount == 0)
                {
                    toConvert.Add(pair);
                    continue;
                }

                foreach (RailSignal signal in blockData.InboundSignals.Keys)
                {
                    if (PathSignalData.CheckIsChainSignal(signal))  //some of inbound signals are chain = no simple block
                        goto NotSimple;;
                }

                signalsToCheck.Clear();
                signalsToCheck.UnionWith(blockData.InboundSignals.Keys);
                bool isSimple = true;
                while (signalsToCheck.Count > 0)
                {
                    RailSignal signal = signalsToCheck.First();
                    signalsToCheck.Remove(signal);
                    RailConnection connection = signal.Connection.InnerConnection;
                    while (true)
                    {
                        if (connection.OuterConnectionCount > 1)
                        {
                            goto NotSimple;
                        }

                        if (connection.OuterConnectionCount == 0)  //end of track
                            break;

                        connection = (RailConnection) connection.OuterConnections[0];
                        if (connection.OuterConnectionCount > 1)
                        {
                            goto NotSimple;
                        }

                        connection = connection.InnerConnection;
                        
                        if (connection.Signal != null)
                        {
                            //opposite signal - we can safely remove it from testing
                            signalsToCheck.Remove(connection.Signal);
                            break;
                        }

                        if (connection.InnerConnection.Signal != null) //next signal, end of searching path of this signal
                            break;
                    }
                }
                if (isSimple)
                    toConvert.Add(pair);
                NotSimple: ;
            }

            foreach (KeyValuePair<RailBlock, RailBlockData> pair in toConvert)
            {
                _railBlocks[pair.Key] = ((PathRailBlockData) pair.Value).ToSimpleBlockData();
            }
        }

        private void CreatePathSignalsData()
        {
            foreach (KeyValuePair<RailBlock,RailBlockData> blockPair in _railBlocks)
            {
                foreach (RailSignal signal in blockPair.Value.InboundSignals.Keys.ToArray())
                {

                    PathSignalData data = new(signal, blockPair.Value) {StateChanged = OnSignalStateChanged};
                    _pathSignals.Add(signal, data);
                    _changedStates.Add(signal);
                    blockPair.Value.InboundSignals[signal] = data;
                }
            }
        }

        private PathRailBlockData GetOrCreateRailBlockData(RailBlock block)
        {
            if (!_railBlocks.TryGetValue(block, out RailBlockData data))
            {
                data = new PathRailBlockData(block);
                _railBlocks.Add(block, data);
            }

            if (data is not PathRailBlockData pathData)
                throw new InvalidOperationException("RailBlockData contains SimpleBlockData");

            return pathData;
        }

        private void OnSignalStateChanged(PathSignalData signalData)
        {
            _changedStates.Add(signalData.Signal);
        }
        
        private bool IsSignalOpenForTrain(RailSignal signal, Train train, PathCollection path)
        {
            _pathToTrain[path] = train;
            PathSignalData signalData = GetPathSignalData(signal);
            if (signalData.ReservedForTrain == train)
                return true;
            if (signalData.ReservedForTrain != null)
            {
                //it should not be
                FileLog.Log("Signal is reserved for another train.");
                return false;
            }

            RailConnection conn = signal.Connection;
            int? pathIndex = path.FindConnectionIndex(conn, train.FrontBound.ConnectionIndex);
            if (pathIndex == null)
                throw new InvalidOperationException("Signal connection not found in the path");
            bool result = signalData.BlockData.TryReservePath(train, path, pathIndex.Value) && signalData.ReservedForTrain == train;
            HighlightReservedPaths();
            return result;
        }

        private void TrainConnectionReached(Train train, TrackConnection connection)
        {
            if (_passedSignals.TryGetValue(train, out RailSignal signal))
            {
                TrainPassedSignal(train, signal);
                _passedSignals.Remove(train);
            }
            RailConnection railConn = (RailConnection) connection;
            if (railConn.Signal != null)
            {
                _passedSignals.Add(train, railConn.Signal);
            }
        }

        private RailBlockData GetBlockData(RailBlock block)
        {
            if (!_railBlocks.TryGetValue(block, out RailBlockData result))
            {
                throw new InvalidOperationException("Block data not found");
            }

            return result;
        }

        private void PathShrinkingRear(PathCollection path, int newRearIndex)
        {
            if (path.RearIndex >= newRearIndex) 
                return;
            PathShrinking(path, path.RearIndex, newRearIndex-1);            
        }

        private void PathShrinkingFront(PathCollection path, int newFrontIndex)
        {
            if (path.FrontIndex <= newFrontIndex) 
                return;
            PathShrinking(path, newFrontIndex+1, path.FrontIndex);            
        }
        
        private void PathShrinking(PathCollection path, int from, int to)  //indexes from and to are inclusive 
        {
            if (!_pathToTrain.TryGetValue(path, out Train train)) 
                return;
            bool changed = false;
            RailBlockData currBlockData = null;
            for (int index = from; index <= to; index++)
            {
                changed = true;
                RailConnection currConnection = (RailConnection) path[index];
                if (currBlockData?.Block != currConnection.Block)
                {
                    currBlockData = GetBlockData(currConnection.Block);
                }
                currBlockData?.ReleaseRailSegment(train, currConnection.Track);

                currConnection = currConnection.InnerConnection;
                if (currBlockData?.Block != currConnection.Block)
                {
                    currBlockData = GetBlockData(currConnection.Block);
                    currBlockData.ReleaseRailSegment(train, currConnection.Track);
                }
            }
            if (changed)
                HighlightReservedPaths();
        }
        
        #region DEBUG

        private readonly HashSet<Highlighter> _highlighters = new();

        private void HideHighlighters()
        {
            foreach (Highlighter highlighter in _highlighters)
            {
                highlighter.gameObject.SetActive(false);
            }
            _highlighters.Clear();
        }
        
        private void HighlightRail(Rail rail, Color color)
        {
            RailConnectionHighlighter man = LazyManager<RailConnectionHighlighter>.Current;
            _highlighters.Add(man.ForOneTrack(rail, color, 0.6f));
        }
        
        private void HighlightReservedPaths()
        {
            HideHighlighters();
            Color color = Color.green;
            Color linkedColor = Color.red;
            foreach (RailBlockData blockData in _railBlocks.Values)
            {
                if (blockData is PathRailBlockData pathRailBlockData)
                {
                    foreach (KeyValuePair<Rail, int> railPair in pathRailBlockData.BlockedRails)
                    {
                        if (railPair.Value > 0)
                            HighlightRail(railPair.Key, color.WithAlpha(0.2f + railPair.Value * 0.2f));
                    }
                    foreach (KeyValuePair<Rail, int> railPair in pathRailBlockData.BlockedLinkedRails)
                    {
                        if (railPair.Value > 0)
                            HighlightRail(railPair.Key, linkedColor.WithAlpha(0.2f + railPair.Value * 0.2f));
                    }
                    foreach (KeyValuePair<Train, (PooledHashSet<Rail> rails, Rail lastPathRail)> railPair in pathRailBlockData.ReservedBeyondPath)
                    {
                        foreach (Rail rail in railPair.Value.rails)
                        {
                            HighlightRail(rail, Color.blue.WithAlpha(0.9f));
                        }
                    }
                }
            }
        }
        
        #endregion

        #region HARMONY

        #region SignalStates

        [HarmonyPrefix]
        [HarmonyPatch(typeof(RailSignal), "GetState")]
        // ReSharper disable once InconsistentNaming
        private static bool RailSignal_GetState_prf(RailSignal __instance, ref RailSignalState __result)
        {
            if (Current != null)
            {
                __result = Current.GetSignalState(__instance);
                return false;
            }

            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(RailSignal), "InvalidateState")]
        // ReSharper disable once InconsistentNaming
        private static bool RailSignal_InvalidateState_prf(RailSignal __instance)
        {
            if (Current != null)
            {
                return Current._changedStates.Remove(__instance);  //if not found, return false to stop further execution of method
            }

            return true;
        }

        #endregion

        #region TrainSignalObstacle

        private static bool _isDetectingObstacle = false;
        private static PathCollection _trainPath = null;
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Train), "DetectObstacle")]
        // ReSharper disable once InconsistentNaming
        private static void Train_DetectObstacle_prf(PathCollection ___Path)
        {
            _isDetectingObstacle = true;
            _trainPath = ___Path;
        }

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(Train), "DetectObstacle")]
        // ReSharper disable once InconsistentNaming
        private static void Train_DetectObstacle_fin()
        {
            _isDetectingObstacle = false;
            _trainPath = null;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(RailSignal), "IsOpen")]
        // ReSharper disable once InconsistentNaming
        private static bool RailSignal_IsOpen_prf(RailSignal __instance, ref bool __result, Train train)
        {
            if (_isDetectingObstacle && Current != null)
            {
                __result = Current.IsSignalOpenForTrain(__instance, train, _trainPath);
                return false;
            }

            return true;
        }
        
        #endregion

        #region TrainMovement

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Train), "OnConnectionReached")]
        // ReSharper disable once InconsistentNaming
        private static void Train_OnConnectionReached_pof(Train __instance, TrackConnection connection)
        {
            Current?.TrainConnectionReached(__instance, connection);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PathCollection), "ShrinkRear")]
        // ReSharper disable once InconsistentNaming
        private static void PathCollection_ShrinkRear_prf(PathCollection __instance, int newRearIndex)
        {
            Current?.PathShrinkingRear(__instance, newRearIndex);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(PathCollection), "ShrinkFront")]
        // ReSharper disable once InconsistentNaming
        private static void PathCollection_ShrinkFront_prf(PathCollection __instance, int newFrontIndex)
        {
            Current?.PathShrinkingFront(__instance, newFrontIndex);
        }
        #endregion 

        #endregion
        
    }
}
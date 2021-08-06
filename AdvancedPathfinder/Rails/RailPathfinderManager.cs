﻿using System.Collections.Generic;
using AdvancedPathfinder.UI;
using JetBrains.Annotations;
using UnityEngine;
using VoxelTycoon;
using VoxelTycoon.Tracks;
using VoxelTycoon.Tracks.Rails;
using XMNUtils;

namespace AdvancedPathfinder.Rails
{
    public class RailPathfinderManager: PathfinderManager<RailPathfinderManager, Rail, RailConnection, RailSection, RailPathfinderNode, RailPathfinderEdge>
    {
        //TODO: Optimize checking direction of path from starting connection to the first node
        //TODO: refresh highlighted path after detaching a train
        private readonly List<RailPathfinderNode> _electricalNodes = new(); //nodes reachable by electric trains
        private bool _closedSectionsDirty; 

        public bool FindImmediately([NotNull] Train train, [NotNull] RailConnection origin, [NotNull] IVehicleDestination target,
            List<TrackConnection> result)
        {
            RailEdgeSettings settings = new RailEdgeSettings(train);
            return FindPath(origin, target, settings, result);
        }

        public void MarkClosedSectionsDirty()
        {
//            _closedSectionsDirty = true;
        }

        protected override IReadOnlyCollection<RailPathfinderNode> GetNodesList(object edgeSettings)
        {
            return edgeSettings is RailEdgeSettings {Electric: true} ? _electricalNodes : base.GetNodesList(edgeSettings);
        }

        protected override void ProcessNodeToSubLists(RailPathfinderNode node)
        {
            base.ProcessNodeToSubLists(node);
            if (node.IsElReachable)
                _electricalNodes.Add(node);
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();
            LazyManager<TrackHelper>.Current.RegisterRailsChanged(OnRailsChanged);
            LazyManager<TrackHelper>.Current.RegisterSignalBuildChanged(OnSignalBuildChanged);
            SimpleLazyManager<RailBlockHelper>.Current.RegisterAddedBlockedRailAction(OnAddedBlockedRail);
            SimpleLazyManager<RailBlockHelper>.Current.RegisterReleasedBlockedRailAction(OnReleasedBlockedRail);
        }

        protected override void OnLateUpdate()
        {
            if (_closedSectionsDirty)
            {
                _closedSectionsDirty = false;
                HighlightClosedBlockSections();
            }
        }

        protected override IReadOnlyCollection<RailConnection> GetStationStopsConnections()
        {
            return StationHelper<RailConnection, RailStation>.Current.GetStationStopsConnections();
        }

        protected override void HighlightNonProcessedTracks(HashSet<Rail> unprocessedTracks)
        {
            RailConnectionHighlighter hlMan = LazyManager<RailConnectionHighlighter>.Current; 
            foreach (Rail rail in unprocessedTracks)
            {
                if (!rail.IsBuilt)
                    return;
                Highlighter hl = hlMan.ForOneTrack(rail, Color.red);
                hl.transform.SetParent(rail.transform);
            }
        }

        protected override Highlighter HighlightConnection(RailConnection connection, bool halfConnection,
            Color color)
        {
            RailConnectionHighlighter hlMan = LazyManager<RailConnectionHighlighter>.Current;
            Highlighter hl = halfConnection ? hlMan.ForOneConnection(connection, color) : hlMan.ForOneTrack(connection.Track, color);
            Rail rail = connection.Track;
            hl.transform.SetParent(rail.transform);
            return hl;
        }

        protected override bool TestPathToFirstNode(List<TrackConnection> connections)
        {
            foreach (TrackConnection connection in connections)
            {
                if (connection is RailConnection railConn)
                {
                    RailSignal signal = railConn.InnerConnection.Signal; //opposite signal
                    RailSignal signal2 = railConn.Signal;
                    if (!ReferenceEquals(signal, null) && signal.IsBuilt && (ReferenceEquals(signal2, null) || signal2.IsBuilt == false))
                    {
                        //only oposite signal = wrong direction
                        return false;
                    }
                }
            }

            return true;
        }

        private void OnRailsChanged(IReadOnlyList<Rail> newRails, IReadOnlyList<Rail> removedRails, IReadOnlyList<Rail> electrificationChangedRails)
        {
            MarkGraphDirty();
        }
        
        private void OnSignalBuildChanged(IReadOnlyList<RailSignal> newSignals,
            IReadOnlyList<RailSignal> removedSignals)
        {
            MarkGraphDirty();
        }

        private void OnAddedBlockedRail(Rail rail, int count, RailBlock block)
        {
            RailSection section = FindSection(rail);
            section?.AddBlockedRail(rail, count, block);
        }

        private void OnReleasedBlockedRail(Rail rail, int count, RailBlock block)
        {
            RailSection section = FindSection(rail);
            section?.ReleaseBlockedRail(rail, count, block);
        }

        private void HighlightClosedBlockSections()
        {
            HideHighlighters();
            int idx = 0;
            foreach (RailSection section in Sections)
            {
                float? closedLength = section.CachedClosedBlockLength;
                if (closedLength is > 0)
                {
                    float ratio = closedLength.Value / section.CalculateCloseBlockLength(section.Length);
                    Color color = Colors[idx].WithAlpha(ratio);
                    HighlightSection(section, color);
                }
                idx = (idx + 1) % Colors.Length;
            }
        }

    }
}
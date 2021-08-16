﻿using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using VoxelTycoon;
using VoxelTycoon.Tracks;
using VoxelTycoon.Tracks.Rails;

namespace AdvancedPathfinder.Rails
{
    public class RailPathfinderNode: PathfinderNodeBase
    {
        public bool IsElReachable { get; private set; }
        public int NumPassableOutboundEdges { get; private set; }  //number of passable edges, that leads to this node
        private Dictionary<PathfinderNodeBase, float> _elReachableNodes;
        private readonly Dictionary<(int destinationHash, bool electric), bool> _pathDiversionCache = new();
        private readonly HashSet<RailConnection> _inboundConnections = new();
        private readonly HashSet<RailConnection> _outboundConnections = new();
        private readonly List<RailPathfinderEdge> _edges = new();

        private Dictionary<PathfinderNodeBase, float> _reachableNodes;
        
        private bool _initialized = false;

        public IReadOnlyCollection<RailConnection> InboundConnections => _inboundConnections;
        public IReadOnlyCollection<RailConnection> OutboundConnections => _outboundConnections;
        public ImmutableList<RailPathfinderEdge> Edges => _edges.ToImmutableList();
        [CanBeNull] 
        internal new RailPathfinderNode PreviousBestNode => (RailPathfinderNode) base.PreviousBestNode;
        [CanBeNull] 
        internal new RailPathfinderEdge PreviousBestEdge => (RailPathfinderEdge) base.PreviousBestEdge;

        internal override IReadOnlyList<PathfinderEdgeBase> GetEdges()
        {
            return _edges;
        }
        
        public Dictionary<PathfinderNodeBase, float> GetReachableNodes(object edgeSettings)
        {
            if (edgeSettings is not RailEdgeSettings {Electric: true})
            {
                return _reachableNodes;
            }

            return _elReachableNodes;
        }

        public bool HasPathDiversion(Train train, IVehicleDestination destination)
        {
            int hash = destination.GetDestinationHash();
            if (_pathDiversionCache.TryGetValue((hash, train.Electric), out bool isDiversion))
                return isDiversion;
            
            int possibilities = 0;
            HashSet<RailPathfinderNode> convertedDest = Manager<RailPathfinderManager>.Current.GetConvertedDestination(destination);
            for (int i = Edges.Count - 1; i >= 0; i--)
            {
                RailPathfinderEdge edge = Edges[i];
                if (!edge.IsPassable(train.Electric))
                    continue;
                ;

                RailPathfinderNode nextNode = (RailPathfinderNode) edge.NextNode;
                if (nextNode == null)
                    continue;

                Dictionary<PathfinderNodeBase, float> reachNodes = nextNode.GetReachableNodes(new RailEdgeSettings() {Electric = train.Electric});
                if (reachNodes == null) //incomplete reachable nodes, cannot determine if path can be diversified, so return true for path update 
                    return true;

                foreach (RailPathfinderNode targetNode in convertedDest)
                {
                    if (reachNodes.ContainsKey(targetNode))
                    {
                        if (++possibilities > 1)
                        {
                            _pathDiversionCache[(hash, train.Electric)] = true;
                            return true;
                        }

                        break;
                    }
                }
            }

            _pathDiversionCache[(hash, train.Electric)] = false;
            return false;
        }

        internal void Initialize(RailConnection inboundConnection, bool trackStart = false)
        {
            if (_initialized)
                throw new InvalidOperationException("Already initialized");
            _initialized = true;
            if (trackStart)
            {
                if (inboundConnection.OuterConnectionCount > 0)
                {
                    throw new InvalidOperationException("Some outer connection on start connection");
                }

                _outboundConnections.Add(inboundConnection);  //add provided connection os outbound (there will be no inbound connections)
            }
            else
            {
                foreach (TrackConnection conn in inboundConnection.OuterConnections)
                {
                    _outboundConnections.Add((RailConnection) conn);
                }

                if (inboundConnection.OuterConnectionCount > 0)
                {
                    TrackConnection outConn = inboundConnection.OuterConnections[0];
                    foreach (TrackConnection conn in outConn.OuterConnections)
                    {
                        _inboundConnections.Add((RailConnection) conn);
                    }
                }
                else
                {
                    //end connection, add provided connection as only inbound
                    _inboundConnections.Add(inboundConnection);
                }
            }
        }
        
        internal void FindEdges(ISectionFinder sectionFinder, INodeFinder nodeFinder)
        {
            foreach (RailConnection connection in _outboundConnections)
            {
                RailPathfinderEdge edge = new() {Owner = this};
                if (!edge.Fill(connection, sectionFinder, nodeFinder))
                    continue;
                _edges.Add(edge);
                ProcessNewEdge(edge);
            }
        }
        
        internal void SetReachableNodes(Dictionary<PathfinderNodeBase, float> reachableNodes, object edgeSettings)
        {
            if (edgeSettings is not RailEdgeSettings {Electric: true})
                _reachableNodes = reachableNodes;
            else
                _elReachableNodes = reachableNodes;
        }

        private void ProcessNewEdge(RailPathfinderEdge edge)
        {
            if (edge.IsPassable())
            {
                edge.NextNode.IsReachable = true;
                NumPassableOutboundEdges++;
                if (edge.Data.IsElectrified)
                    ((RailPathfinderNode) edge.NextNode).IsElReachable = true;
            }
        }
    }
}
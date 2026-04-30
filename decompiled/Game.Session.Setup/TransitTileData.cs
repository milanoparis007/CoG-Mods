using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Setup;

internal sealed class TransitTileData
{
	public List<RoadTileInfo> roads = new List<RoadTileInfo>();

	public List<RoadTileInfo> rails = new List<RoadTileInfo>();

	public List<NodeEdge> railEdges = new List<NodeEdge>();
}

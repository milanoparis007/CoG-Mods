using System.Collections.Generic;
using SomaSim.Util;

namespace Game.Core;

public interface IPathContext
{
	void OnSearchStart(PlayerID pid, EntityID eid);

	Fixnum LeastCostEstimate(PlayerID pid, Node current, Node target);

	void GetNeighbors(PathElement pathSoFar, List<PathContextNeighbor> outNeighbors);

	void OnSearchEnd();
}

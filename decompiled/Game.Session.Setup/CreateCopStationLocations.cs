using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Setup;

internal class CreateCopStationLocations
{
	private SetupOrchestratorContext _ctx;

	internal CreateCopStationLocations(SetupOrchestratorContext ctx)
	{
		_ctx = ctx;
	}

	public IEnumerator Start()
	{
		List<Node> nodes = CreatePrecinctCenters(Game.ctx.session.mapconfig.groups.GetCount(PlayerType.CopPlayer));
		_ctx.copStationData = new SpecialBuildingLocationData(nodes);
		yield break;
	}

	private static List<Node> CreatePrecinctCenters(int count)
	{
		List<Node> list = (from node in Game.ctx.board.nodes.GetAllNodesUnsafe()
			where node.HasRoad && !node.HasRail && node.IsOnGround
			select node).ToList();
		TagList skipDistricts = Game.ctx.session.mapconfig.playerStart.copsSkipDistricts;
		if (skipDistricts != null && skipDistricts.Count > 0)
		{
			list = list.Where((Node node) => !node.IsInMatchingDistrict(skipDistricts)).ToList();
		}
		List<float> list2 = ListGenerators.ListOfValues(list.Count, 1f);
		Xorshift rng = Game.ctx.scenario.MakeSeededRng<CreateCopStationLocations>();
		List<Node> list3 = new List<Node>();
		int minDistSquare = 50 * 50;
		for (int num = 0; num < count; num++)
		{
			int index = rng.PickIndex(list, list2, normalized: false);
			list3.Add(list[index]);
			list2[index] = 1E-05f;
			ZeroOutNeighbors(list, list2, list[index].pos, minDistSquare);
		}
		return list3;
	}

	private static void ZeroOutNeighbors(List<Node> nodes, List<float> weights, WorldPos center, int minDistSquare)
	{
		int i = 0;
		for (int count = nodes.Count; i < count; i++)
		{
			if (!(weights[i] < 1f) && (nodes[i].pos - center).MagnitudeSquared < (float)minDistSquare)
			{
				weights[i] = 0.001f;
			}
		}
	}
}

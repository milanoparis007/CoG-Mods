using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Services.Maps;
using Game.Session.Board;
using SomaSim.Util;

namespace Game.Session.Setup;

internal class AssignNamesToStreets
{
	private class Names
	{
		public List<Label> horizontal = new List<Label>();

		public List<Label> vertical = new List<Label>();
	}

	private Xorshift _rng;

	private MapBoardConfig _config;

	private NodeManager _nodes;

	private LabelDictionary<Names> _namesForConfigs = new LabelDictionary<Names>();

	internal IEnumerator Start()
	{
		_rng = Game.ctx.scenario.MakeSeededRng<AssignNamesToStreets>();
		_config = Game.ctx.board.MapConfig;
		_nodes = Game.ctx.board.nodes;
		foreach (NodeEdge item in _nodes.GetAllEdgesUnsafe())
		{
			if (item.IsValid)
			{
				TryAssignName(item);
			}
		}
		_namesForConfigs.Clear();
		yield return null;
	}

	private void TryAssignName(NodeEdge edge)
	{
		Label? label = FindNameFromExistingEdge(edge.a, edge.IsVertical) ?? FindNameFromExistingEdge(edge.b, edge.IsVertical);
		if (!label.HasValue)
		{
			Names names = FindNamesForConfig(edge.a.FindNode().cfg);
			List<Label> list = (edge.IsVertical ? names.vertical : names.horizontal);
			label = list.RemoveLast();
		}
		edge.roadName = label.Value;
	}

	private Label? FindNameFromExistingEdge(NodeID nodeId, bool isVertical)
	{
		NodeEdgeID[] edges = _nodes.GetNode(nodeId).edges;
		for (int i = 0; i < edges.Length; i++)
		{
			NodeEdgeID id = edges[i];
			if (!id.IsNotValid)
			{
				NodeEdge edge = _nodes.GetEdge(id);
				if (edge.IsVertical == isVertical && edge.roadName.IsSet)
				{
					return edge.roadName;
				}
			}
		}
		return null;
	}

	private Names FindNamesForConfig(Label cfg)
	{
		Names names = _namesForConfigs.FindOrNull(cfg);
		if (names == null || names.horizontal.Count == 0 || names.vertical.Count == 0)
		{
			Names names2 = (_namesForConfigs[cfg] = MakeNamesForConfig(cfg));
			names = names2;
		}
		return names;
	}

	private Names MakeNamesForConfig(Label cfg)
	{
		List<Label> list = new List<Label>(_config.streetNames);
		_rng.Shuffle(list);
		foreach (Label initialName in _config.GetNodesConfigByID(cfg).initialNames)
		{
			list.Remove(initialName);
			list.Add(initialName);
		}
		Names names = new Names();
		for (int i = 0; i < list.Count; i++)
		{
			Label label = list[i];
			List<string> allPossibleValues = Game.serv.loc.GetAllPossibleValues(label.String);
			_rng.Shuffle(allPossibleValues);
			List<Label> list2 = (MathUtil.IsOdd(i) ? names.horizontal : names.vertical);
			foreach (string item in allPossibleValues)
			{
				list2.Add(new Label(item));
			}
		}
		return names;
	}
}

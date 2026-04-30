using System;
using System.Collections;
using Game.Core;
using Game.Services.Maps;
using Game.Session.Board;

namespace Game.Session.Setup;

internal sealed class CreateMapEdges
{
	private const int ITERATIONS_PER_FRAME = 1000;

	private MapBoardConfig _configs;

	private NodeManager _manager;

	private IntSize _beadSpacing;

	private SetupOrchestratorContext _ctx;

	public CreateMapEdges(SetupOrchestratorContext ctx)
	{
		_ctx = ctx;
	}

	public IEnumerator Start()
	{
		_configs = Game.ctx.board.MapConfig;
		_manager = Game.ctx.board.nodes;
		_beadSpacing = Game.ctx.board.MapConfig.beadSpacing;
		int iter = 0;
		foreach (Node item in _manager.GetAllNodesUnsafe())
		{
			if (item.IsValid)
			{
				Direction[] aLL_DIRECTIONS = DirectionUtil.ALL_DIRECTIONS;
				foreach (Direction dir in aLL_DIRECTIONS)
				{
					TryAddEdge(item, dir);
				}
				int i = iter + 1;
				iter = i;
				if (i % 1000 == 0)
				{
					yield return null;
				}
			}
		}
	}

	private bool TestIfBridge(Node a, Node b, Direction dir, out bool canMakeBridge)
	{
		WorldPos normalized = (b.pos - a.pos).Normalized;
		float magnitude = (b.pos - a.pos).Magnitude;
		int inDirection = _beadSpacing.GetInDirection(dir);
		int num = (int)Math.Round(magnitude / (float)inDirection);
		bool flag = false;
		canMakeBridge = true;
		for (int i = 1; i < num; i++)
		{
			WorldPos pos = a.pos + normalized * i;
			if (_ctx.waterRegionData.IsPointInWater(pos, out var allowBridges))
			{
				flag = true;
				canMakeBridge = allowBridges;
				break;
			}
		}
		if (a.terraintype == TerrainType.Water || b.terraintype == TerrainType.Water)
		{
			flag = (byte)((flag ? 1u : 0u) | 1u) != 0;
		}
		return flag;
	}

	private void TryAddEdge(Node a, Direction dir)
	{
		if (a.GetEdgeID(dir).IsValid)
		{
			return;
		}
		int inDirection = _configs.GetNodesConfigByID(a.cfg).nodeSpacing.GetInDirection(dir);
		WorldPos worldPos = a.GenerateDirectionDelta(dir) * inDirection;
		WorldPos pos = a.pos + worldPos;
		Node node = _manager.FindNearestNodeAround(pos);
		if (node == null || node.cfg.Index != a.cfg.Index)
		{
			return;
		}
		if (TestIfBridge(a, node, dir, out var canMakeBridge))
		{
			if (canMakeBridge)
			{
				_manager.AddEdge(a, node, dir, isBridge: true);
			}
		}
		else
		{
			_manager.AddEdge(a, node, dir, isBridge: false);
		}
	}
}

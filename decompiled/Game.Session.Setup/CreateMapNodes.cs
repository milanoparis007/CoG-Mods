using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Services.Maps;
using Game.Session.Board;
using SomaSim.Util;

namespace Game.Session.Setup;

internal sealed class CreateMapNodes
{
	private sealed class NodeGenQueue
	{
		public MapNodesConfig cfg;

		public int countDone;

		public Queue<Node> todos = new Queue<Node>();
	}

	private const int ITERATIONS_PER_FRAME = 100;

	private IRandom _rng;

	private MapBoardConfig _mapConfig;

	private NodeManager _nodes;

	private List<NodeGenQueue> _queues = new List<NodeGenQueue>();

	private List<NodeBridgeAttempt> _bridgeAttempts = new List<NodeBridgeAttempt>();

	private SetupOrchestratorContext _ctx;

	public CreateMapNodes(SetupOrchestratorContext ctx)
	{
		_ctx = ctx;
	}

	public IEnumerator Start()
	{
		_rng = Game.ctx.board.data.rng;
		_mapConfig = Game.ctx.session.mapconfig.map;
		_nodes = Game.ctx.board.nodes;
		GenerateInitialNodes();
		int iter = 0;
		while (true)
		{
			foreach (NodeGenQueue queue in _queues)
			{
				float relativeSpeed = queue.cfg.relativeSpeed;
				if (queue.todos.Count > 0 && _rng.CheckProbability(relativeSpeed))
				{
					ExpandNextNode(queue);
				}
				if (queue.cfg.HasForceMaxSize && queue.countDone > queue.cfg.forceMaxSize)
				{
					queue.todos.Clear();
				}
			}
			if (_queues.TrueForAll((NodeGenQueue q) => q.todos.Count == 0))
			{
				break;
			}
			int num = iter + 1;
			iter = num;
			if (num % 100 == 0)
			{
				yield return null;
			}
		}
		_ctx.gridConnectionData = GetGridConnectionData();
	}

	private void GenerateInitialNodes()
	{
		foreach (MapNodesConfig node in _mapConfig.nodes)
		{
			GenerateInitialNode(node);
		}
	}

	private void GenerateInitialNode(MapNodesConfig cfg)
	{
		WorldPos pos = cfg.forceStart;
		if (pos.IsZero)
		{
			pos = FindRandomNodePosition(cfg.nodeSpacing, _mapConfig.mapSize, _rng);
		}
		float deg = (cfg.HasForcedAngle ? cfg.forceAngle : _rng.Generate(-45f, 45f));
		GridTransform transform = new GridTransform
		{
			pos = pos,
			deg = deg
		};
		TerrainType terrainType = _ctx.heightmapData.GetTerrainType(pos);
		Node node = _nodes.AddNode(cfg, transform, terrainType, new IntPos(0, 0));
		if (cfg.allowAsIsland)
		{
			node.procData.disconnectedIslandRoot = true;
		}
		NodeGenQueue nodeGenQueue = new NodeGenQueue();
		nodeGenQueue.cfg = cfg;
		nodeGenQueue.todos.Enqueue(node);
		_queues.Add(nodeGenQueue);
	}

	public static WorldPos FindRandomNodePosition(IntSize margin, IntSize mapSize, IRandom rng)
	{
		RandomRangeF range = new RandomRangeF(margin.width, mapSize.width - margin.width, 1);
		RandomRangeF range2 = new RandomRangeF(margin.height, mapSize.height - margin.height, 1);
		float x = rng.Generate(range);
		float y = rng.Generate(range2);
		return new WorldPos(x, y);
	}

	private void ExpandNextNode(NodeGenQueue queue)
	{
		Node node = queue.todos.Dequeue();
		DirectionUtil.ALL_DIRECTIONS.ForEach(delegate(Direction dir)
		{
			TryExpandNode(queue, node, dir);
		});
	}

	private float GetNodeDistanceInDirection(MapNodesConfig cfg, Direction dir)
	{
		return cfg.nodeSpacing.GetInDirection(dir);
	}

	private void TryExpandNode(NodeGenQueue queue, Node oldNode, Direction dir)
	{
		float nodeDistanceInDirection = GetNodeDistanceInDirection(queue.cfg, dir);
		WorldPos worldPos = oldNode.GenerateDirectionDelta(dir);
		WorldPos pos = oldNode.pos + worldPos * nodeDistanceInDirection;
		if (!Game.ctx.board.IsValid(pos))
		{
			return;
		}
		IntPos genOffset = oldNode.genOffset + DirectionUtil.GetSpreadDirection(dir).AsIntPos;
		if (!IsGenOffsetWithinConfigBounds(queue.cfg, genOffset))
		{
			return;
		}
		TerrainType terrainType = _ctx.heightmapData.GetTerrainType(pos);
		switch (terrainType)
		{
		case TerrainType.Mountain:
			return;
		case TerrainType.Water:
			if (queue.cfg.stopAtWater)
			{
				return;
			}
			break;
		}
		if (!_nodes.CanAddNode(oldNode, pos, out var hitNode))
		{
			if (hitNode != null && hitNode.cfg.Index != oldNode.cfg.Index && !oldNode.IsOnWater && !hitNode.IsOnWater)
			{
				_bridgeAttempts.Add(new NodeBridgeAttempt
				{
					node = oldNode,
					destinationNode = hitNode,
					direction = dir
				});
			}
		}
		else
		{
			GridTransform transform = new GridTransform
			{
				pos = pos,
				deg = oldNode.deg
			};
			Node item = _nodes.AddNode(queue.cfg, transform, terrainType, genOffset);
			queue.todos.Enqueue(item);
			queue.countDone++;
		}
	}

	private bool IsGenOffsetWithinConfigBounds(MapNodesConfig cfg, IntPos genOffset)
	{
		IntSize forceMaxSpread = cfg.forceMaxSpread;
		float num = ((float)forceMaxSpread.width + 0.1f) / 2f;
		float num2 = num - (float)forceMaxSpread.width;
		float num3 = ((float)forceMaxSpread.height + 0.1f) / 2f;
		float num4 = num3 - (float)forceMaxSpread.height;
		if ((float)genOffset.x >= num2 && (float)genOffset.x <= num && (float)genOffset.y >= num4)
		{
			return (float)genOffset.y <= num3;
		}
		return false;
	}

	public GridConnectionData GetGridConnectionData()
	{
		return new GridConnectionData
		{
			bridgeAttempts = _bridgeAttempts
		};
	}
}

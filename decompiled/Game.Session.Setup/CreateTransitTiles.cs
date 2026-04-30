using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Session.Board;
using Game.Session.Entities;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Setup;

internal sealed class CreateTransitTiles
{
	private const int ITERATIONS_PER_FRAME = 1000;

	private static readonly TransitTileset ROAD_TILES = new TransitTileset
	{
		IIntersectionTmpl = (Label)"road-I",
		LIntersectionTmpl = (Label)"road-L",
		TIntersectionTmpl = (Label)"road-T",
		FourWayIntersectionTmpl = (Label)"road-4way",
		EndNodeTmpl = (Label)"road-end",
		VerticalEdgeTmpl = (Label)"road-edge",
		VerticalBetweenGridsTmpl = (Label)"road-edge",
		BridgeStartTmpl = (Label)"road-bridge-start",
		BridgeSlopeTmpl = (Label)"road-bridge-slope",
		BridgeSmallTrussTmpl = (Label)"road-bridge-small-truss",
		BridgeTrussTmpl = (Label)"road-bridge-truss",
		BridgeStartBead = RoadBeadPlacement.RoadBridgeStart,
		BridgeSlopeBead = RoadBeadPlacement.RoadBridgeSlope,
		BridgeSmallTrussBead = RoadBeadPlacement.RoadBridgeSmall,
		BridgeTrussBead = RoadBeadPlacement.RoadBridgeNormal,
		IntersectionSize = 2
	};

	private static readonly TransitTileset RAIL_TILES = new TransitTileset
	{
		IIntersectionTmpl = (Label)"rail-I",
		LIntersectionTmpl = (Label)"rail-L",
		TIntersectionTmpl = (Label)"rail-T",
		FourWayIntersectionTmpl = (Label)"rail-4way",
		EndNodeTmpl = (Label)"rail-end",
		VerticalEdgeTmpl = (Label)"rail-edge",
		VerticalEdgeStartTmpl = (Label)"rail-edge-start",
		VerticalBetweenGridsTmpl = (Label)"rail-edge-between",
		BridgeStartTmpl = (Label)"rail-bridge-start",
		BridgeSlopeTmpl = (Label)"rail-bridge-truss",
		BridgeSmallTrussTmpl = (Label)"rail-bridge-small-truss",
		BridgeTrussTmpl = (Label)"rail-bridge-truss",
		BridgeStartBead = RoadBeadPlacement.RailBridgeStart,
		BridgeSlopeBead = RoadBeadPlacement.RailBridgeNormal,
		BridgeSmallTrussBead = RoadBeadPlacement.RailBridgeSmall,
		BridgeTrussBead = RoadBeadPlacement.RailBridgeNormal,
		IntersectionSize = 4
	};

	private BoardManager _mgr;

	private NodeManager _nodes;

	private CoroutineTask _task;

	private SetupOrchestratorContext _ctx;

	public CreateTransitTiles(SetupOrchestratorContext ctx)
	{
		_ctx = ctx;
	}

	internal IEnumerator Start()
	{
		_mgr = Game.ctx.board;
		_nodes = _mgr.nodes;
		_task = Game.serv.sequencer.StartCoroutineTask(CreateTiles());
		while (!_task.IsFinished)
		{
			yield return null;
		}
		Game.ctx.models.CombinedRoadsMesh.PushUpdates();
	}

	private IEnumerator CreateTiles()
	{
		int i = 0;
		foreach (Node item in _nodes.GetAllNodesUnsafe())
		{
			TryMakeRoadOrRailNode(item);
			int num = i + 1;
			i = num;
			if (num % 1000 == 0)
			{
				yield return null;
			}
		}
		List<NodeEdge> allEdgesUnsafe = _nodes.GetAllEdgesUnsafe();
		foreach (NodeEdge item2 in allEdgesUnsafe.FindAll(delegate(NodeEdge edge)
		{
			_nodes.GetNode(edge.a);
			_nodes.GetNode(edge.b);
			return (edge.IsRoad || edge.IsRail) && edge.isBridge;
		}))
		{
			if (!item2.transitTilesCreated)
			{
				TryMakeBridge(item2);
			}
		}
		foreach (NodeEdge item3 in allEdgesUnsafe)
		{
			if (!item3.transitTilesCreated)
			{
				TryMakeRoadOrRailEdge(item3);
				int num = i + 1;
				i = num;
				if (num % 1000 == 0)
				{
					yield return null;
				}
			}
		}
	}

	private void TryMakeBridge(NodeEdge edge)
	{
		NodeEdgeID edgeID = _nodes.GetNode(edge.a).GetEdgeID(DirectionUtil.GetOppositeDirection(edge.abDir));
		if (!edgeID.IsValid || !_nodes.GetEdge(edgeID).isBridge)
		{
			DoBridge(edge, edge.IsRail ? RAIL_TILES : ROAD_TILES, alternatePieceRotation: false);
		}
	}

	private void DoBridge(NodeEdge edge, TransitTileset tileset, bool alternatePieceRotation)
	{
		Node node = _nodes.GetNode(edge.a);
		Node node2 = _nodes.GetNode(edge.b);
		_ = node.IsOnWater;
		bool isOnWater = node2.IsOnWater;
		EntityConfig entityConfig = Game.ctx.entityman.FindTemplate(tileset.BridgeStartTmpl);
		EntityConfig entityConfig2 = Game.ctx.entityman.FindTemplate(tileset.BridgeSlopeTmpl);
		EntityConfig entityConfig3 = Game.ctx.entityman.FindTemplate(tileset.BridgeTrussTmpl);
		List<BridgeBead> list = edge.bridgeBeads.FindAll((BridgeBead bead) => bead.placementStyle != RoadBeadPlacement.None);
		int count = list.Count;
		for (int num = 0; num < count; num++)
		{
			BridgeBead bridgeBead = list[num];
			EntityConfig tileConfig = entityConfig;
			float num2 = 0f;
			if ((!isOnWater && num == count - 1) || num == count - 2)
			{
				num2 = 180f;
			}
			if (bridgeBead.placementStyle == tileset.BridgeTrussBead)
			{
				tileConfig = entityConfig3;
				num2 += (alternatePieceRotation ? 180f : 0f);
			}
			else if (bridgeBead.placementStyle == tileset.BridgeStartBead)
			{
				tileConfig = entityConfig;
			}
			else if (bridgeBead.placementStyle == RoadBeadPlacement.RoadBridgeSlope)
			{
				tileConfig = entityConfig2;
			}
			MakeBeadTile(edge, bridgeBead, tileConfig, num2);
			alternatePieceRotation = !alternatePieceRotation;
		}
		edge.transitTilesCreated = true;
		if (isOnWater)
		{
			Direction oppositeDirection = DirectionUtil.GetOppositeDirection(edge.baDir);
			NodeEdgeID edgeID = node2.GetEdgeID(oppositeDirection);
			if (edgeID.IsValid)
			{
				NodeEdge edge2 = _nodes.GetEdge(edgeID);
				DoBridge(edge2, tileset, alternatePieceRotation);
			}
		}
	}

	private void TryMakeRoadOrRailEdge(NodeEdge edge)
	{
		if (edge.IsRail)
		{
			Node node = _nodes.GetNode(edge.a);
			Node node2 = _nodes.GetNode(edge.b);
			bool stretchVert = node.cfg.Index != node2.cfg.Index;
			MakeConnectingRoad(edge, RAIL_TILES, stretchVert);
		}
		else if (edge.IsRoad)
		{
			MakeConnectingRoad(edge, ROAD_TILES, stretchVert: true);
		}
	}

	private void TryMakeRoadOrRailNode(Node node)
	{
		if (node.HasRoad && node.HasRail)
		{
			NodeEdge nodeEdge = node.GetEdgeID(Direction.N).FindEdge();
			int num = ((nodeEdge == null || !nodeEdge.IsRail) ? 90 : 0);
			MakeNodeTilePlain(node, EntityConstants.ROAD_RAIL_CROSSING, RoadTileType.Crossing, num);
		}
		else if (node.HasRail)
		{
			if (node.HasTerminal)
			{
				float degOffset = FindAngleToAttachedRailNode(node) - node.deg;
				MakeNodeTilePlain(node, EntityConstants.RAIL_TERMINAL, RoadTileType.None, degOffset);
				return;
			}
			NeighborFlags neighborFlags = _nodes.GetNeighborFlags(node, (NodeEdge nodeEdge2) => nodeEdge2.IsRail);
			MakeNodeTile(node, neighborFlags, RAIL_TILES);
		}
		else if (node.HasRoad)
		{
			NeighborFlags neighborFlags2 = _nodes.GetNeighborFlags(node, (NodeEdge nodeEdge2) => nodeEdge2.IsRoad);
			MakeNodeTile(node, neighborFlags2, ROAD_TILES);
		}
	}

	private float FindAngleToAttachedRailNode(Node source)
	{
		using (ListPool<Node>.PooledBlockList pooledBlockList = ListPool<Node>.Allocate())
		{
			source.FindAllNeighbors(pooledBlockList, clearFirst: false);
			foreach (Node item in pooledBlockList)
			{
				if (item.HasRail)
				{
					return WorldPos.GetFacingDegrees(item.pos, source.pos).GetValueOrDefault();
				}
			}
		}
		return 0f;
	}

	private void MakeConnectingRoad(NodeEdge edge, TransitTileset tileset, bool stretchVert)
	{
		Node node = _nodes.GetNode(edge.a);
		Node node2 = _nodes.GetNode(edge.b);
		bool isOnWater = node.IsOnWater;
		bool isOnWater2 = node2.IsOnWater;
		bool flag = edge.roadBeads.FindAll((RoadBead roadBead) => roadBead.placementStyle != RoadBeadPlacement.None).TrueForAll((RoadBead roadBead) => !_ctx.waterRegionData.IsPointInWater(roadBead.pos));
		if (isOnWater || isOnWater2 || !flag)
		{
			EntityConfig entityConfig = Game.ctx.entityman.FindTemplate(tileset.BridgeStartTmpl);
			EntityConfig entityConfig2 = Game.ctx.entityman.FindTemplate(tileset.BridgeSlopeTmpl);
			EntityConfig entityConfig3 = Game.ctx.entityman.FindTemplate(tileset.BridgeSmallTrussTmpl);
			EntityConfig entityConfig4 = Game.ctx.entityman.FindTemplate(tileset.BridgeTrussTmpl);
			List<BridgeBead> list = edge.bridgeBeads.FindAll((BridgeBead bridgeBead2) => bridgeBead2.placementStyle != RoadBeadPlacement.None);
			int count = list.Count;
			bool flag2 = false;
			for (int num = 0; num < count; num++)
			{
				BridgeBead bridgeBead = list[num];
				EntityConfig tileConfig = entityConfig;
				float num2 = 0f;
				if (!isOnWater2 && num == count - 1)
				{
					num2 = 180f;
				}
				if (bridgeBead.placementStyle == tileset.BridgeTrussBead)
				{
					tileConfig = entityConfig4;
					num2 += (flag2 ? 180f : 0f);
				}
				else if (bridgeBead.placementStyle == tileset.BridgeSmallTrussBead)
				{
					Debug.LogError("THIS SHOULDN'T BE USED ANYMORE? WHYYY");
					tileConfig = entityConfig3;
				}
				else if (bridgeBead.placementStyle == tileset.BridgeStartBead)
				{
					tileConfig = entityConfig;
				}
				else if (bridgeBead.placementStyle == tileset.BridgeSlopeBead)
				{
					tileConfig = entityConfig2;
				}
				MakeBeadTile(edge, bridgeBead, tileConfig, num2);
				flag2 = !flag2;
			}
		}
		else if (stretchVert)
		{
			MakeRoadForEdge(edge, tileset);
		}
		else
		{
			EntityConfig entityConfig5 = Game.ctx.entityman.FindTemplate(tileset.VerticalEdgeTmpl);
			EntityConfig entityConfig6 = Game.ctx.entityman.FindTemplate(tileset.VerticalEdgeStartTmpl);
			int count2 = edge.roadBeads.Count;
			for (int num3 = 0; num3 < edge.roadBeads.Count; num3++)
			{
				RoadBead bead = edge.roadBeads[num3];
				float degModifier = 0f;
				EntityConfig tileConfig2 = entityConfig6;
				if (num3 != 0)
				{
					if (num3 == count2 - 1)
					{
						degModifier = 180f;
					}
					else
					{
						tileConfig2 = entityConfig5;
					}
				}
				MakeBeadTile(edge, bead, tileConfig2, degModifier);
			}
		}
		edge.transitTilesCreated = true;
	}

	private void MakeNodeTile(Node node, NeighborFlags neighborFlags, TransitTileset tileset)
	{
		Label template = Label.NULL;
		int num = 0;
		switch (neighborFlags)
		{
		case NeighborFlags.North:
			template = tileset.EndNodeTmpl;
			break;
		case NeighborFlags.East:
			template = tileset.EndNodeTmpl;
			num = 90;
			break;
		case NeighborFlags.South:
			template = tileset.EndNodeTmpl;
			num = 180;
			break;
		case NeighborFlags.West:
			template = tileset.EndNodeTmpl;
			num = 270;
			break;
		case NeighborFlags.NorthSouth:
			template = (node.IsOnWater ? tileset.BridgeTrussTmpl : tileset.IIntersectionTmpl);
			break;
		case NeighborFlags.EastWest:
			template = (node.IsOnWater ? tileset.BridgeTrussTmpl : tileset.IIntersectionTmpl);
			num = 90;
			break;
		case NeighborFlags.NorthEast:
			template = tileset.LIntersectionTmpl;
			break;
		case NeighborFlags.SouthEast:
			template = tileset.LIntersectionTmpl;
			num = 90;
			break;
		case NeighborFlags.SouthWest:
			template = tileset.LIntersectionTmpl;
			num = 180;
			break;
		case NeighborFlags.NorthWest:
			template = tileset.LIntersectionTmpl;
			num = 270;
			break;
		case NeighborFlags.NorthEastSouth:
			template = tileset.TIntersectionTmpl;
			break;
		case NeighborFlags.EastSouthWest:
			template = tileset.TIntersectionTmpl;
			num = 90;
			break;
		case NeighborFlags.NorthSouthWest:
			template = tileset.TIntersectionTmpl;
			num = 180;
			break;
		case NeighborFlags.NorthEastWest:
			template = tileset.TIntersectionTmpl;
			num = 270;
			break;
		case NeighborFlags.NorthEastSouthWest:
			template = tileset.FourWayIntersectionTmpl;
			break;
		}
		if (template.IsSet)
		{
			MakeNodeTileWithPotentialProps(node, template, neighborFlags, num);
		}
	}

	private RoadTileType GetTileTypeFromFlags(NeighborFlags flags)
	{
		RoadTileType result = RoadTileType.None;
		switch (flags)
		{
		case NeighborFlags.North:
		case NeighborFlags.East:
		case NeighborFlags.South:
		case NeighborFlags.West:
			result = RoadTileType.End;
			break;
		case NeighborFlags.NorthSouth:
		case NeighborFlags.EastWest:
			result = RoadTileType.TwoStraight;
			break;
		case NeighborFlags.NorthEast:
		case NeighborFlags.SouthEast:
		case NeighborFlags.NorthWest:
		case NeighborFlags.SouthWest:
			result = RoadTileType.Corner;
			break;
		case NeighborFlags.NorthEastSouth:
		case NeighborFlags.NorthEastWest:
		case NeighborFlags.NorthSouthWest:
		case NeighborFlags.EastSouthWest:
			result = RoadTileType.ThreeWay;
			break;
		case NeighborFlags.NorthEastSouthWest:
			result = RoadTileType.FourWay;
			break;
		}
		return result;
	}

	private void MakeNodeTilePlain(Node node, Label template, RoadTileType nodeTileType, float degOffset = 0f)
	{
		EntityConfig template2 = Game.ctx.entityman.FindTemplate(template);
		float deg = degOffset + node.deg;
		GridTransform transform = new GridTransform
		{
			pos = node.pos,
			deg = deg
		};
		Entity entity = Game.ctx.board.CreateEntityAtStartup(template2, transform);
		if (nodeTileType != RoadTileType.None)
		{
			RoadTileInfo item = new RoadTileInfo
			{
				tileType = nodeTileType,
				entity = entity
			};
			_ctx.transitTileData.rails.Add(item);
		}
	}

	private void MakeNodeTileWithPotentialProps(Node node, Label template, NeighborFlags neighborFlags = NeighborFlags.None, float degOffset = 0f)
	{
		if (node.IsOnWater)
		{
			return;
		}
		EntityConfig template2 = Game.ctx.entityman.FindTemplate(template);
		float deg = degOffset + node.deg;
		GridTransform transform = new GridTransform
		{
			pos = node.pos,
			deg = deg
		};
		Entity entity = Game.ctx.board.CreateEntityAtStartup(template2, transform);
		if (!node.IsOnWater)
		{
			if (!node.HasRail && node.HasRoad)
			{
				RoadTileType tileTypeFromFlags = GetTileTypeFromFlags(neighborFlags);
				RoadTileInfo item = new RoadTileInfo
				{
					tileType = tileTypeFromFlags,
					entity = entity
				};
				_ctx.transitTileData.roads.Add(item);
			}
			else if (node.HasRail && !node.HasRoad)
			{
				RoadTileType tileTypeFromFlags2 = GetTileTypeFromFlags(neighborFlags);
				RoadTileInfo item2 = new RoadTileInfo
				{
					tileType = tileTypeFromFlags2,
					entity = entity
				};
				_ctx.transitTileData.rails.Add(item2);
			}
		}
	}

	private void MakeBeadTile(NodeEdge edge, TransitBead bead, EntityConfig tileConfig, float degModifier = 0f)
	{
		float num = (float)(DirectionUtil.IsHorizontal(edge.abDir) ? 90 : 0) + bead.deg + degModifier;
		if (edge.abDir == Direction.S || edge.abDir == Direction.W)
		{
			num += 180f;
		}
		GridTransform transform = new GridTransform
		{
			pos = bead.pos,
			deg = num
		};
		Game.ctx.board.CreateEntityAtStartup(tileConfig, transform);
	}

	private void MakeRoadForEdge(NodeEdge edge, TransitTileset tileset)
	{
		Node node = _nodes.GetNode(edge.a);
		Node node2 = _nodes.GetNode(edge.b);
		WorldPos worldPos = node.pos + node.GenerateDirectionDelta(edge.abDir);
		WorldPos worldPos2 = node2.pos + node2.GenerateDirectionDelta(edge.baDir);
		WorldPos worldPos3 = new WorldPos(Vector3.Lerp(worldPos.AsVector3XZ, worldPos2.AsVector3XZ, 0.5f));
		RoadBead bead = new RoadBead();
		float num = float.MaxValue;
		foreach (RoadBead roadBead in edge.roadBeads)
		{
			if (roadBead.placementStyle != RoadBeadPlacement.None)
			{
				float magnitudeSquared = (worldPos3 - roadBead.pos).MagnitudeSquared;
				if (magnitudeSquared < num)
				{
					bead = roadBead;
					num = magnitudeSquared;
				}
			}
		}
		if (num == float.MaxValue)
		{
			return;
		}
		EntityConfig entityConfig = Game.ctx.entityman.FindTemplate(tileset.VerticalBetweenGridsTmpl);
		if (edge.IsRail)
		{
			WorldPos worldPos4 = worldPos2 - worldPos;
			int num2 = Mathf.FloorToInt(worldPos4.Magnitude / entityConfig.board.lotsize.height);
			Debug.DrawLine(worldPos2.AsVector3XZ + Vector3.up, worldPos.AsVector3XZ + Vector3.up, Color.green, 10000f);
			float deg = Vector3.SignedAngle(Vector3.forward, worldPos4.AsVector3XZ.normalized, Vector3.up);
			EntityConfig template = Game.ctx.entityman.FindTemplate(tileset.VerticalEdgeTmpl);
			WorldPos a = worldPos;
			WorldPos b = worldPos + worldPos4.Normalized * entityConfig.board.lotsize.height;
			WorldPos a2 = worldPos2;
			WorldPos b2 = worldPos + worldPos4.Normalized * (num2 - 1) * entityConfig.board.lotsize.height;
			Debug.DrawLine(a.AsVector3XZ, b.AsVector3XZ, Color.red, 10000f);
			Debug.DrawLine(a2.AsVector3XZ, b2.AsVector3XZ, Color.blue, 10000f);
			for (int i = 1; i < num2; i++)
			{
				WorldPos pos = worldPos + worldPos4.Normalized * ((float)i * entityConfig.board.lotsize.height);
				GridTransform transform = new GridTransform
				{
					pos = pos,
					deg = deg
				};
				Game.ctx.board.CreateEntityAtStartup(template, transform);
			}
			WorldPos pos2 = WorldPos.Lerp(a, b, 0.5f);
			GridTransform transform2 = new GridTransform
			{
				pos = pos2,
				deg = 0f
			};
			Entity entity = Game.ctx.board.CreateEntityAtStartup(entityConfig, transform2);
			GameObject model = entity.components.model.Proxy.GetSubProxy(0).model;
			model.GetComponent<LODGroup>().enabled = false;
			for (int j = 0; j < model.transform.childCount; j++)
			{
				model.transform.GetChild(j).gameObject.SetActive(j == 0);
			}
			Vector3 asVector3XZ = Game.ctx.board.nodes.GetNode(edge.a).pos.AsVector3XZ;
			Vector3 asVector3XZ2 = Game.ctx.board.nodes.GetNode(edge.a).GenerateDirectionDelta(edge.abDir).AsVector3XZ;
			Vector3 asVector3XZ3 = b.AsVector3XZ;
			RotateVertsOnFrontHalf(asVector3XZ, asVector3XZ2, asVector3XZ3, entity);
			pos2 = WorldPos.Lerp(a2, b2, 0.5f);
			transform2 = new GridTransform
			{
				pos = pos2,
				deg = 0f
			};
			entity = Game.ctx.board.CreateEntityAtStartup(entityConfig, transform2);
			Vector3 asVector3XZ4 = Game.ctx.board.nodes.GetNode(edge.b).pos.AsVector3XZ;
			asVector3XZ2 = Game.ctx.board.nodes.GetNode(edge.b).GenerateDirectionDelta(edge.baDir).AsVector3XZ;
			asVector3XZ3 = b2.AsVector3XZ;
			RotateVertsOnFrontHalf(asVector3XZ4, asVector3XZ2, asVector3XZ3, entity);
		}
		else if (edge.IsRoad)
		{
			GridTransform transform3 = new GridTransform
			{
				pos = worldPos3,
				deg = 0f
			};
			Entity entity2 = Game.ctx.board.CreateEntityAtStartup(entityConfig, transform3);
			RotateVertsOnEntity(edge, bead, entity2);
			GameObject mainModel = entity2.components.model.GetMainModel();
			DynamicMeshHandle handle = Game.ctx.models.CombinedRoadsMesh.PushMesh(mainModel);
			entity2.components.model.SetHandle(handle);
			entity2.components.board.AttachToBead(edge, bead);
			RoadTileInfo item = new RoadTileInfo
			{
				tileType = RoadTileType.TwoStraight,
				entity = entity2
			};
			_ctx.transitTileData.roads.Add(item);
		}
	}

	private static void RotateVertsOnFrontHalf(Vector3 startPos, Vector3 dir, Vector3 endPos, Entity road)
	{
		Vector3 asVector3XZ = road.data.board.worldpos.AsVector3XZ;
		Vector3 vector = startPos + dir;
		Vector3 modelForward = road.components.model.GetModelForward();
		Vector3 normalized = (vector - asVector3XZ).normalized;
		float num = Vector3.SignedAngle(normalized, modelForward, Vector3.up) * ((float)Math.PI / 180f);
		float num2 = (float)Math.PI / 180f * Vector3.SignedAngle(normalized, -dir, Vector3.up);
		Mesh meshUnshared = road.components.model.GetMeshUnshared();
		Vector3[] vertices = meshUnshared.vertices;
		for (int i = 0; i < vertices.Length; i++)
		{
			Vector3 vector2 = vertices[i];
			Vector3 vector3 = vector2;
			float x = vector3.x;
			if (vector3.z < 0f)
			{
				Vector3 vector4 = endPos + Vector3.Cross(Vector3.up, normalized) * x + normalized * road.config.board.lotsize.height / 2f - asVector3XZ - vector2;
				vector4.y = 0f;
				vector2 += vector4;
			}
			else
			{
				vector2 = RotateXZAroundPoint(0f, 0f, num, vector2);
				vector2 = RotateXZAroundPoint(0f, 1f, 0f - num2, vector2);
				Vector3 vector5 = vector + Vector3.Cross(Vector3.up, dir) * (0f - x) - asVector3XZ - vector2;
				vector5.y = 0f;
				vector2 += vector5;
			}
			vector2 = RotateXZAroundPoint(0f, 0f, 0f - num, vector2);
			vertices[i] = vector2;
		}
		meshUnshared.vertices = vertices;
		meshUnshared.RecalculateBounds();
		Vector3 vector6 = endPos - vector;
		vector6.y = 0f;
		float num3 = 0f - Vector3.SignedAngle(Vector3.forward, vector6.normalized, Vector3.up);
		float magnitude = vector6.magnitude;
		road.components.board.LotSizeOverride = new WorldSize(road.components.board.Config.lotsize.width, magnitude);
		road.components.board.Move(new WorldPos(asVector3XZ), 0f - num3 + 180f);
	}

	public static void RotateVertsOnEntity(NodeEdge edge, RoadBead bead, Entity road)
	{
		Vector3 asVector3XZ = road.data.board.worldpos.AsVector3XZ;
		Node node = Game.ctx.board.nodes.GetNode(edge.a);
		Node node2 = Game.ctx.board.nodes.GetNode(edge.b);
		Vector3 asVector3XZ2 = node.GenerateDirectionDelta(edge.abDir).AsVector3XZ;
		Vector3 asVector3XZ3 = node2.GenerateDirectionDelta(edge.baDir).AsVector3XZ;
		Vector3 vector = node.pos.AsVector3XZ + asVector3XZ2;
		Vector3 vector2 = node2.pos.AsVector3XZ + asVector3XZ3;
		Vector3 modelForward = road.components.model.GetModelForward();
		Vector3 normalized = (vector - asVector3XZ).normalized;
		float num = Vector3.SignedAngle(normalized, modelForward, Vector3.up) * ((float)Math.PI / 180f);
		float num2 = (float)Math.PI / 180f * Vector3.SignedAngle(normalized, -asVector3XZ2, Vector3.up);
		float num3 = (float)Math.PI / 180f * Vector3.SignedAngle(-normalized, -asVector3XZ3, Vector3.up);
		Vector3 vector3 = asVector3XZ + normalized;
		Vector3 vector4 = asVector3XZ - normalized;
		_ = -normalized;
		_ = (vector - vector3).magnitude;
		_ = (vector2 - vector4).magnitude;
		Mesh meshUnshared = road.components.model.GetMeshUnshared();
		Vector3[] vertices = meshUnshared.vertices;
		for (int i = 0; i < vertices.Length; i++)
		{
			Vector3 vector5 = vertices[i];
			Vector3 vector6 = vector5;
			float x = vector6.x;
			vector5 = RotateXZAroundPoint(0f, 0f, num, vector5);
			if (vector6.z < 0f)
			{
				vector5 = RotateXZAroundPoint(0f, -1f, 0f - num3, vector5);
				Vector3 vector7 = vector2 + Vector3.Cross(Vector3.up, asVector3XZ3) * x - asVector3XZ - vector5;
				vector7.y = 0f;
				vector5 += vector7;
			}
			else
			{
				vector5 = RotateXZAroundPoint(0f, 1f, 0f - num2, vector5);
				Vector3 vector8 = vector + Vector3.Cross(Vector3.up, asVector3XZ2) * (0f - x) - asVector3XZ - vector5;
				vector8.y = 0f;
				vector5 += vector8;
			}
			vector5 = RotateXZAroundPoint(0f, 0f, 0f - num, vector5);
			vertices[i] = vector5;
		}
		meshUnshared.vertices = vertices;
		meshUnshared.RecalculateBounds();
		Vector3 vector9 = vector2 - vector;
		vector9.y = 0f;
		float num4 = 0f - Vector3.SignedAngle(Vector3.forward, vector9.normalized, Vector3.up);
		float magnitude = vector9.magnitude;
		road.components.board.LotSizeOverride = new WorldSize(road.components.board.Config.lotsize.width, magnitude);
		road.components.board.Move(new WorldPos(asVector3XZ), 0f - num4 + 180f);
	}

	public static Vector3 RotateXZAroundPoint(float cx, float cz, float angle, Vector3 point)
	{
		float num = Mathf.Sin(angle);
		float num2 = Mathf.Cos(angle);
		point.x -= cx;
		point.z -= cz;
		float num3 = point.x * num2 - point.z * num;
		float num4 = point.x * num + point.z * num2;
		point.x = num3 + cx;
		point.z = num4 + cz;
		return point;
	}
}

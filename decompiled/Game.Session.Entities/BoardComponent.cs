using System.Collections.Generic;
using Game.Core;
using Game.Session.Board;
using UnityEngine;

namespace Game.Session.Entities;

public sealed class BoardComponent : BaseComponent, ILoadObserverComponent, IUpgradableComponent
{
	private class UpgradeData
	{
		public LotBeadHandle bead;
	}

	public WorldSize? LotSizeOverride;

	public BoardConfig Config => _baseConfig as BoardConfig;

	public WorldSize GetLotSizeWithOverride => LotSizeOverride ?? Config.lotsize;

	public bool CanBeKnown => Config.discoverable;

	public override void OnAfterEntityCreated(bool loaded)
	{
		base.OnAfterEntityCreated(loaded);
	}

	public override void OnBeforeEntityDestroyed(bool shutdown)
	{
		base.OnBeforeEntityDestroyed(shutdown);
		DetachFromBead(shutdown);
		Remove(shutdown);
	}

	public void OnAfterLoading()
	{
		BoardData board = _entity.data.board;
		Game.ctx.board.spacecache.AddOrMoveEntity(_entity);
		_entity.components?.model.MoveModel(board.worldpos, board.deg);
	}

	public void Insert(GridTransform transform)
	{
		_entity.data.board.SetPos(transform.pos, transform.deg);
		Game.ctx.board.spacecache.AddOrMoveEntity(_entity);
		_entity.components?.model?.MoveModel(transform.pos, transform.deg);
	}

	public void Remove(bool shutdown)
	{
		if (!shutdown)
		{
			Game.ctx.board.spacecache.TryRemoveEntity(_entity);
			_entity.data.board.ClearPos();
		}
	}

	public bool IsPointInsideFootprint(WorldPos testpos)
	{
		WorldSize lotSize = LotSizeOverride ?? Config.lotsize;
		GridTransform transform = _entity.data.board.Transform;
		return BoardConfig.IsPointInsideFootprint(lotSize, transform, testpos);
	}

	public void GetFootprintCorners(float p, List<WorldPos> outPoints)
	{
		WorldSize lotSize = LotSizeOverride ?? Config.lotsize;
		GridTransform transform = _entity.data.board.Transform;
		BoardConfig.GetFootprintTestPoints(lotSize, transform, p, outPoints, includeInnerPoints: false);
	}

	public void GetFootprintInsidePoints(float p, List<WorldPos> outPoints)
	{
		WorldSize lotSize = LotSizeOverride ?? Config.lotsize;
		GridTransform transform = _entity.data.board.Transform;
		BoardConfig.GetFootprintPoints(lotSize, transform, p, outPoints);
	}

	internal Node GetNode()
	{
		return _entity.data.board.bead.nodeId.FindNode();
	}

	internal NodeID GetNodeID()
	{
		return _entity.data.board.bead.nodeId;
	}

	public static GridTransform FindPositionWhenAttachedToBead(WorldSize lotsize, NodeEdge edge, LotBead bead, bool left)
	{
		WorldPos worldPos = new WorldPos(lotsize.width / 2f, 0f - (lotsize.height / 2f + 1f));
		Node node = Game.ctx.board.nodes.GetNode(edge.a);
		Node node2 = Game.ctx.board.nodes.GetNode(edge.b);
		WorldPos worldPos2 = node.pos + node.GenerateDirectionDelta(edge.abDir);
		WorldPos worldPos3 = node2.pos + node2.GenerateDirectionDelta(edge.baDir) - worldPos2;
		float num = Vector3.SignedAngle(Vector3.right, worldPos3.AsVector3XZ.normalized, Vector3.up);
		if (left)
		{
			num += 180f;
		}
		float num2 = (num + 360f) % 360f;
		WorldPos worldPos4 = worldPos.Rotate(num2);
		WorldPos pos = bead.pos + worldPos4;
		num2 = (num2 + 180f) % 360f;
		return new GridTransform(pos, num2);
	}

	public bool AttachAndMoveToBeadPrecalculated(GridTransform tr, NodeEdge edge, LotBead bead, bool left)
	{
		if (!Game.ctx.board.IsValid(tr.pos))
		{
			return false;
		}
		DetachFromBead(shutdown: false);
		Remove(shutdown: false);
		Insert(tr);
		AttachToBead(edge, bead, left);
		return true;
	}

	public bool AttachAndMoveToBead(NodeEdge edge, LotBead bead, bool left)
	{
		GridTransform tr = FindPositionWhenAttachedToBead(Config.lotsize, edge, bead, left);
		return AttachAndMoveToBeadPrecalculated(tr, edge, bead, left);
	}

	public void Move(WorldPos position, float degrees)
	{
		Remove(shutdown: false);
		GridTransform transform = new GridTransform
		{
			pos = position,
			deg = degrees
		};
		Insert(transform);
	}

	private void DetachFromBead(bool shutdown)
	{
		if (!shutdown)
		{
			Game.ctx.board.nodes.TryDetachEntity(_entity);
			_entity.data.board.bead = LotBeadHandle.INVALID;
		}
	}

	private void AttachToBead(NodeEdge edge, LotBead bead, bool left)
	{
		_ = _entity.data.board.bead;
		LotBeadHandle bead2 = Game.ctx.board.nodes.AttachOrReattachEntity(_entity, edge, bead, left);
		_entity.data.board.bead = bead2;
	}

	public void AttachToBead(NodeEdge edge, RoadBead bead)
	{
		DetachFromBead(shutdown: false);
		_ = _entity.data.board.bead;
		LotBeadHandle bead2 = Game.ctx.board.nodes.AttachOrReattachEntity(_entity, edge, bead);
		_entity.data.board.bead = bead2;
	}

	public bool IsKnown(PlayerID pid)
	{
		if (!Config.discoverable)
		{
			return true;
		}
		return _entity.data.board.known?.Get(pid) ?? false;
	}

	public void SetKnown(PlayerID pid, bool value)
	{
		PlayerFlags playerFlags = _entity.data.board.known;
		if (playerFlags == null)
		{
			playerFlags = (_entity.data.board.known = new PlayerFlags());
		}
		playerFlags.Set(pid, value);
		_entity.components.SendEvent(EntityEventType.EntityKnownChanged);
	}

	public object GenerateUpgradeData()
	{
		return new UpgradeData
		{
			bead = _entity.data.board.bead
		};
	}

	public void ConsumeUpgradeData(EntityID previousId, object value)
	{
		UpgradeData upgradeData = value as UpgradeData;
		if (upgradeData.bead.IsValid)
		{
			NodeEdge nodeEdge = upgradeData.bead.edgeId.FindEdge();
			LotBead bead = nodeEdge.lotBeads[upgradeData.bead.beadIndex];
			AttachAndMoveToBead(nodeEdge, bead, upgradeData.bead.left);
		}
	}

	public void OnAfterUpgrade(EntityID previousId)
	{
	}
}

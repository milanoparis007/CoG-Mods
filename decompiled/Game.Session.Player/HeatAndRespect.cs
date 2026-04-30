using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Assets;
using Game.Session.Board;
using Game.Session.Data;
using SomaSim.Util;

namespace Game.Session.Player;

public class HeatAndRespect
{
	private PlayerID _pid;

	private static Dictionary<Node, Fixnum> _tmpValues = new Dictionary<Node, Fixnum>();

	public void Initialize(PlayerTerritory territory)
	{
		_pid = territory.PID;
	}

	public void RespectFromCheatsIncrement(Node node, Fixnum delta)
	{
		node.respect.GetOrAdd(_pid).staticFromCheats += delta;
		RecomputeRespect(node, force: true);
	}

	public void AddRespectBuff(Node node, Label id, EntityID crewpeep, ModQuery query)
	{
		node.respect.GetOrAdd(_pid).AddBuff(id, query, crewpeep);
	}

	public void AddHeatBuff(Node node, Label id, EntityID crewpeep, ModQuery query)
	{
		node.heat.GetOrAdd(_pid).AddBuff(id, query, crewpeep);
	}

	public void RemoveRespectBuff(Node node, Label id)
	{
		node.respect.GetOrNull(_pid)?.RemoveBuff(id);
	}

	public void RemoveHeatBuff(Node node, Label id)
	{
		node.heat.GetOrNull(_pid)?.RemoveBuff(id);
	}

	public bool ContainsRespectBuff(Node node, Label id)
	{
		return node.respect.GetOrNull(_pid)?.ContainsBuff(id) ?? false;
	}

	public bool ContainsHeatBuff(Node node, Label id)
	{
		return node.heat.GetOrNull(_pid)?.ContainsBuff(id) ?? false;
	}

	public void RecomputeRespect(Node node, bool force = false)
	{
		RecomputeRespect(_pid, node, force);
	}

	public void RecomputeHeat(Node node, bool force = false)
	{
		RecomputeHeat(_pid, node, force);
	}

	public static void RecomputeRespectForAllPlayers(Node node, bool initial)
	{
		List<Respect> data = node.respect.data;
		if (data == null || data.Count == 0)
		{
			return;
		}
		foreach (Respect item in data)
		{
			RecomputeRespect(item.pid, node, initial);
		}
	}

	public static void RecomputeHeatForAllPlayers(Node node)
	{
		List<Heat> data = node.heat.data;
		if (data == null || data.Count == 0)
		{
			return;
		}
		foreach (Heat item in data)
		{
			RecomputeHeat(item.pid, node, forceCurrent: false);
		}
	}

	private static void RecomputeRespect(PlayerID pid, Node node, bool forceCurrent)
	{
		Respect orNull = node.respect.GetOrNull(pid);
		if (orNull != null)
		{
			RespectSettings respect = Game.serv.globals.settings.people.social.respect;
			ModValue finalRespectMultiplier = respect.finalRespectMultiplier;
			ModValue velocityPerTurn = respect.velocityPerTurn;
			var (newGoal, newCurrent) = StepPValue(new ModQuery(pid, node), forceCurrent, orNull, finalRespectMultiplier, velocityPerTurn);
			AdjustValCheckThresh(pid, node, orNull, newGoal, newCurrent);
		}
	}

	private static void RecomputeHeat(PlayerID pid, Node node, bool forceCurrent)
	{
		Heat orNull = node.heat.GetOrNull(pid);
		if (orNull != null)
		{
			HeatSettings heat = Game.serv.globals.settings.people.social.heat;
			ModValue finalHeatMultiplier = heat.finalHeatMultiplier;
			ModValue velocityPerTurn = heat.velocityPerTurn;
			var (newGoal, newCurrent) = StepPValue(new ModQuery(pid, node), forceCurrent, orNull, finalHeatMultiplier, velocityPerTurn);
			AdjustValSimple(orNull, newGoal, newCurrent);
		}
	}

	private static (Fixnum goal, Fixnum curr) StepPValue(ModQuery query, bool force, ProportionalValue val, ModValue mulval, ModValue velval)
	{
		Fixnum fixnum = val.CalculateBaseValue(query);
		Fixnum fixnum2 = mulval.Evaluate(query);
		Fixnum fixnum3 = fixnum * fixnum2;
		Fixnum fixnum4 = velval.Evaluate(query);
		Fixnum item = (force ? fixnum3 : (val.current + Fixnum.Clamp(fixnum3 - val.current, -fixnum4, fixnum4)));
		return (goal: fixnum3, curr: item);
	}

	private static void AdjustValCheckThresh(PlayerID pid, Node node, Respect respect, Fixnum newGoal, Fixnum newCurrent)
	{
		Fixnum current = respect.current;
		respect.goal = newGoal;
		respect.current = newCurrent;
		RespectSettings respect2 = Game.serv.globals.settings.people.social.respect;
		PlayerID nodeOwner = GetNodeOwner(node);
		ModQuery query = new ModQuery(pid, node);
		Fixnum fixnum = respect2.lossThreshold.Evaluate(pid);
		if (current > fixnum && newCurrent <= fixnum && nodeOwner == pid)
		{
			SwitchNodeOwnership(node, PlayerID.INVALID);
		}
		Fixnum fixnum2 = respect2.gainThreshold.Evaluate(query);
		if (current < fixnum2 && newCurrent >= fixnum2 && nodeOwner.IsNotValid)
		{
			SwitchNodeOwnership(node, pid);
		}
		else if (newCurrent >= fixnum2 && nodeOwner != pid && nodeOwner.IsValid && node.respect.GetHighestCurrentRespect().pid == pid)
		{
			SwitchNodeOwnership(node, pid);
		}
	}

	private static void AdjustValSimple(ProportionalValue heat, Fixnum newGoal, Fixnum newCurrent)
	{
		heat.goal = newGoal;
		heat.current = newCurrent;
	}

	private static void SwitchNodeOwnership(Node node, PlayerID newOwner)
	{
		PlayerID nodeOwner = GetNodeOwner(node);
		bool isValid = nodeOwner.IsValid;
		bool isValid2 = newOwner.IsValid;
		if (isValid)
		{
			PlayerTerritory.ClearNodeOwner(node, nodeOwner, newOwner);
		}
		if (isValid2)
		{
			PlayerTerritory.SetNodeOwner(node, newOwner);
		}
		MaybeShowSwitchNodeVFX(node, nodeOwner, newOwner);
	}

	private static void MaybeShowSwitchNodeVFX(Node node, PlayerID oldOwner, PlayerID newOwner)
	{
		if (Game.ctx.IsInteractive)
		{
			if (newOwner.IsHumanPlayer)
			{
				Game.ctx.vfx.PlayOneShotPFX(PFXType.GainTerritoryFX, node.pos, newOwner, 2f);
				Game.ctx.sfx.PlayTerritoryChanged(gained: true);
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.TERR_EXPAND, TickerTitle.TERR_EXPAND, Loc.Get("ui.tickers.new-corner-control"), node.id);
				Game.ctx.simman.hints.ShowCornerHint();
			}
			else if (oldOwner.IsHumanPlayer)
			{
				Game.ctx.vfx.PlayOneShotPFX(PFXType.LoseTerritoryFX, node.pos, newOwner, 2f);
				Game.ctx.sfx.PlayTerritoryChanged(gained: false);
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.TERR_CONTRACT, TickerTitle.TERR_CONTRACT, Loc.Get("ui.tickers.corner-control-lost"), node.id);
			}
		}
	}

	public void RespectFromNeighbors(List<(Node, Fixnum)> total, Fixnum vIn, Fixnum vOut, List<NodeID> owned)
	{
		_tmpValues.Clear();
		foreach (NodeID item in owned)
		{
			Node node = item.FindNode();
			NodeEdgeID[] edges = node.edges;
			for (int i = 0; i < edges.Length; i++)
			{
				NodeEdgeID edgeId = edges[i];
				if (!edgeId.IsNotValid)
				{
					NodeEdge nodeEdge = edgeId.FindEdge();
					if (nodeEdge.IsRoad)
					{
						Node node2 = nodeEdge.FindOtherNode(node);
						bool flag = node2.owner.Is(_pid);
						Fixnum value = _tmpValues.FindOrDefault(node2, Fixnum.ZERO) + (flag ? vIn : vOut);
						_tmpValues[node2] = value;
					}
				}
			}
		}
		total.Clear();
		foreach (KeyValuePair<Node, Fixnum> tmpValue in _tmpValues)
		{
			total.Add((tmpValue.Key, tmpValue.Value));
		}
		_tmpValues.Clear();
	}

	private static PlayerID GetNodeOwner(Node node)
	{
		return node.owner.Get();
	}
}

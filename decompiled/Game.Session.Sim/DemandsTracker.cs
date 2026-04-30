using System.Collections;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session;
using SomaSim.SION;
using SomaSim.Util;

namespace Game.Session.Sim;

public class DemandsTracker : ISystemTurnSubManager<SimulationManager>, ISubManager<SimulationManager>, ISaveLoadProvider
{
	public struct BribeResult
	{
		public bool hasData;

		public Price price;

		public Fixnum percent;

		public bool canPlayerPay;

		public BribeResult(bool hasData, Price price, Fixnum percent, bool canPlayerPay)
		{
			this.hasData = hasData;
			this.price = price;
			this.percent = percent;
			this.canPlayerPay = canPlayerPay;
		}
	}

	private enum Transition
	{
		CompliesByRequest,
		CompliesByForce,
		CompliesByBribe,
		AttackedWhileComplied,
		ExpiredFromComplied,
		DoesNotComply
	}

	public DemandsTrackerPersistedData data = new DemandsTrackerPersistedData();

	private DemandsSettings Settings => Game.serv.globals.settings.people.social.demands;

	public void Initialize(SimulationManager _)
	{
	}

	public void Release()
	{
	}

	public void OnSystemTurn()
	{
	}

	private static bool LinkMatches(Demand link, PlayerID source, Demand.Target target)
	{
		if (link.source != source)
		{
			return false;
		}
		if (!Demand.Target.Equals(link.target, target))
		{
			return false;
		}
		return true;
	}

	private int FindIndex(PlayerID source, Demand.Target target)
	{
		int i = 0;
		for (int count = data.entries.Count; i < count; i++)
		{
			if (LinkMatches(data.entries[i], source, target))
			{
				return i;
			}
		}
		return -1;
	}

	public Demand FindOrNull(PlayerID source, PlayerID target)
	{
		return FindOrNull(source, Demand.Target.MakeForPlayer(target));
	}

	public Demand FindOrNull(VisitState visit)
	{
		return FindOrNull(visit.pid, MakeTargetForVisit(visit));
	}

	public Demand FindOrNull(PlayerID source, Demand.Target target)
	{
		int num = FindIndex(source, target);
		if (num < 0)
		{
			return null;
		}
		return data.entries[num];
	}

	public Demand FindOrMake(PlayerID source, Demand.Target target)
	{
		int num = FindIndex(source, target);
		if (num < 0)
		{
			data.entries.Add(new Demand(source, target));
			num = data.entries.Count - 1;
		}
		return data.entries[num];
	}

	public bool DemandExists(PlayerID source, Demand.Target target)
	{
		return FindOrNull(source, target) != null;
	}

	public bool DemandExists(PlayerID source, Demand.Target target, Demand.Type demand)
	{
		return FindOrNull(source, target)?.HasDemandSet(demand) ?? false;
	}

	public bool DemandHasState(PlayerID source, Demand.Target target, Demand.State state)
	{
		Demand demand = FindOrNull(source, target);
		if (demand == null)
		{
			return false;
		}
		return demand.state == state;
	}

	public (bool present, Demand.State state) FindDemandState(VisitState visit)
	{
		return FindDemandState(visit.pid, MakeTargetForVisit(visit));
	}

	public (bool present, Demand.State state) FindDemandState(PlayerID source, Demand.Target target)
	{
		Demand demand = FindOrNull(source, target);
		return (present: demand != null, state: demand?.state ?? Demand.State.Neutral);
	}

	public bool CanPlaceDemand(VisitState visit)
	{
		return CanPlaceDemand(visit.pid, MakeTargetForVisit(visit));
	}

	public bool CanPlaceDemand(PlayerID source, Demand.Target target)
	{
		return FindOrNull(source, target)?.IsStateNeutral ?? true;
	}

	public bool IsCompliant(VisitState visit)
	{
		return FindDemandState(visit).state == Demand.State.Compliant;
	}

	public bool IsDefiant(VisitState visit)
	{
		return FindDemandState(visit).state == Demand.State.Defiant;
	}

	public bool CanTradeInTerritory(PlayerID candidate, PlayerID nodeOwner)
	{
		Demand demand = FindOrNull(candidate, Demand.Target.MakeForPlayer(nodeOwner));
		if (demand != null && demand.IsStateCompliant)
		{
			return demand.HasDemandSet(Demand.Type.AllowBizAccess);
		}
		return false;
	}

	public void ExtendDemandToAdd(PlayerID source, Demand.Target target, Demand.Type type, EntityID crewpeep)
	{
		Demand demand = FindOrNull(source, target);
		SetStateAndAdd(demand, null, type, crewpeep);
		ProduceExplanationTicker(demand, Transition.CompliesByRequest);
	}

	private void SetStateAndAdd(Demand demand, Demand.State? state, Demand.Type type, EntityID crewpeep)
	{
		if (state.HasValue)
		{
			demand.SetState(state.Value);
		}
		if (type != Demand.Type.None)
		{
			demand.AddDemand(type);
			if (demand.IsStateCompliant)
			{
				PerformSocialOnSuccess(demand, type, crewpeep);
			}
			InformAIAboutNewDemand(demand);
		}
	}

	private static void InformAIAboutNewDemand(Demand demand)
	{
		if (demand.target.IsPlayer)
		{
			PlayerInfo playerInfo = demand.target.pid.FindPlayer();
			if (playerInfo != null && playerInfo.IsGangOrGoon)
			{
				playerInfo.ai.OnAddedDemand(demand);
			}
		}
	}

	private void PerformSocialOnSuccess(Demand demand, Demand.Type type, EntityID crewpeep)
	{
		DemandDef.Social social = FindDemandDefinition(type)?.onSuccess;
		if (social != null)
		{
			PlayerInfo playerInfo = demand.source.FindPlayer();
			if (social.npcSocial.IsSet)
			{
				playerInfo.social.PerformSocialActionOn(social.npcSocial, demand.target.peep, crewpeep);
			}
			if (social.playerSocial.IsSet)
			{
				EntityID item = FindPlayerPeepForTarget(demand.target).peepId;
				playerInfo.social.PerformSocialActionOn(social.playerSocial, item, crewpeep);
			}
		}
	}

	private (PlayerID pid, EntityID peepId) FindPlayerPeepForTarget(Demand.Target target)
	{
		if (target.IsPlayer)
		{
			return FindPlayerPeepFor(target.pid);
		}
		if (target.IsOwner)
		{
			return FindPlayerPeepFor(target.FindNode().owner.pid);
		}
		return (pid: target.pid, peepId: EntityID.INVALID);
		static (PlayerID pid, EntityID peepId) FindPlayerPeepFor(PlayerID pid)
		{
			return (pid: pid, peepId: pid.FindPlayer()?.social.PlayerPeepId ?? EntityID.INVALID);
		}
	}

	public Demand.Target MakeTargetForVisit(VisitState visit)
	{
		if (visit.npc?.data?.person != null)
		{
			AgentData agent = visit.npc.data.agent;
			if (agent != null && agent.pid.IsAnyPlayer)
			{
				return Demand.Target.MakeForPlayer(visit.npc.data.agent.pid);
			}
			if (visit.npc.data.person.business.IsValid)
			{
				return Demand.Target.MakeForBizOwner(visit.npc);
			}
		}
		return Demand.Target.EMPTY;
	}

	public DemandDef FindDemandDefinition(Demand.Type type)
	{
		return Settings.FindOrNullDemand(type);
	}

	public DemandTransition FindDemandTransition(Demand.Target target)
	{
		return Settings.FindTransition(target);
	}

	public (bool canDemand, Fixnum percent) FindRequestToComplyPercentage(PlayerID source, Demand.Target target)
	{
		if (!CanPlaceDemand(source, target))
		{
			return (canDemand: false, percent: default(Fixnum));
		}
		ModQuery query = new ModQuery(source, target.peep, target.FindNodeId());
		Fixnum item = FindDemandTransition(target).requestToComplyPercent?.Evaluate(query) ?? Fixnum.ZERO;
		return (canDemand: true, percent: item);
	}

	public BribeResult FindBribeToComplyData(PlayerID source, Demand.Target target)
	{
		BribeResult result = default(BribeResult);
		ModQuery query = new ModQuery(source, target.peep, target.FindNodeId());
		DemandTransition demandTransition = FindDemandTransition(target);
		if (demandTransition.bribeToComplyAmount == null || demandTransition.bribeToComplyPercent == null)
		{
			return result;
		}
		Fixnum fixnum = demandTransition.bribeToComplyAmount.Evaluate(query);
		Price price = new Price(-fixnum);
		Fixnum percent = demandTransition.bribeToComplyPercent.Evaluate(query);
		bool canPlayerPay = source.FindPlayer().finances.CanChangeMoneyOnPlayer(price);
		return new BribeResult(hasData: true, price, percent, canPlayerPay);
	}

	public (bool isDefiant, Fixnum percent) FindForceToComplyPercentage(PlayerID source, Demand.Target target)
	{
		var (flag, state) = FindDemandState(source, target);
		if (!flag || state != Demand.State.Defiant)
		{
			return (isDefiant: false, percent: 0);
		}
		ModQuery query = new ModQuery(source, target.peep, target.FindNodeId());
		Fixnum item = FindDemandTransition(target).forceToComplyPercent?.Evaluate(query) ?? Fixnum.ZERO;
		return (isDefiant: true, percent: item);
	}

	public Demand PerformRequestOwnerToComply(VisitState visit, Demand.Type type)
	{
		return PerformRequestToComply(visit.pid, visit.crew.peepId, MakeTargetForVisit(visit), type, visit.GetBldgNodeID());
	}

	public Demand PerformRequestAIToComply(PlayerID source, PlayerID target, Demand.Type type, NodeID nodeId)
	{
		return PerformRequestToComply(source, EntityID.INVALID, Demand.Target.MakeForPlayer(target), type, nodeId);
	}

	private Demand PerformRequestToComply(PlayerID source, EntityID sourcePeep, Demand.Target target, Demand.Type type, NodeID nodeId)
	{
		Demand demand = FindOrMake(source, target);
		bool flag = demand.IsStateNeutral;
		if (flag)
		{
			(bool, Fixnum) tuple = FindRequestToComplyPercentage(source, target);
			if (tuple.Item1)
			{
				float probability = (float)tuple.Item2 / 100f;
				flag = MakeTurnBasedRNG(target.peep).CheckProbability(probability);
			}
		}
		if (flag)
		{
			SetStateAndAdd(demand, Demand.State.Compliant, type, sourcePeep);
		}
		else
		{
			demand.ResetDemandsTo(Demand.Type.None);
			SetStateAndAdd(demand, Demand.State.Defiant, type, sourcePeep);
		}
		Transition tt = ((!flag) ? Transition.DoesNotComply : Transition.CompliesByRequest);
		FinishComplianceAttempt(demand, tt, usedForce: false, nodeId, sourcePeep);
		return demand;
	}

	public Demand PerformBribeToComply(VisitState visit, Demand.Type type)
	{
		return PerformBribeToComply(visit.pid, visit.crew.peepId, MakeTargetForVisit(visit), type);
	}

	private Demand PerformBribeToComply(PlayerID source, EntityID sourcePeep, Demand.Target target, Demand.Type type)
	{
		PlayerInfo playerInfo = source.FindPlayer();
		Demand demand = FindOrMake(source, target);
		BribeResult bribeResult = FindBribeToComplyData(source, target);
		if (!bribeResult.hasData || !bribeResult.canPlayerPay)
		{
			return demand;
		}
		float probability = (float)bribeResult.percent / 100f;
		if (MakeTurnBasedRNG(target.peep).CheckProbability(probability))
		{
			SetStateAndAdd(demand, Demand.State.Compliant, type, sourcePeep);
			playerInfo.finances.DoChangeMoneyOnPlayerPeep(bribeResult.price, MoneyReason.Bribe);
		}
		return demand;
	}

	public Demand PerformForceOwnerToComply(VisitState visit, Demand.Type type)
	{
		return PerformForceToComply(visit.pid, visit.crew.peepId, MakeTargetForVisit(visit), type, visit.GetBldgNodeID());
	}

	public Demand PerformForceAIToComply(PlayerID source, PlayerID target, Demand.Type type, NodeID nodeId)
	{
		return PerformForceToComply(source, EntityID.INVALID, Demand.Target.MakeForPlayer(target), type, nodeId);
	}

	private Demand PerformForceToComply(PlayerID source, EntityID sourcePeep, Demand.Target target, Demand.Type type, NodeID nodeId)
	{
		Demand demand = FindOrMake(source, target);
		(bool isDefiant, Fixnum percent) tuple = FindForceToComplyPercentage(source, target);
		bool item = tuple.isDefiant;
		Fixnum item2 = tuple.percent;
		float probability = (item ? ((float)item2 / 100f) : 0f);
		bool flag = MakeTurnBasedRNG(target.peep).CheckProbability(probability);
		if (Game.ctx.tutorial.ShowingTutorial)
		{
			flag = true;
		}
		if (flag)
		{
			SetStateAndAdd(demand, Demand.State.Compliant, type, sourcePeep);
		}
		Transition tt = (flag ? Transition.CompliesByForce : Transition.DoesNotComply);
		FinishComplianceAttempt(demand, tt, usedForce: true, nodeId, sourcePeep);
		return demand;
	}

	private void FinishComplianceAttempt(Demand demand, Transition tt, bool usedForce, NodeID nodeId, EntityID crewpeep)
	{
		if (!demand.source.IsHumanPlayer)
		{
			return;
		}
		DemandTransition demandTransition = Settings.FindTransition(demand.target);
		if (demandTransition.explainCompliance)
		{
			ProduceExplanationTicker(demand, tt);
		}
		if (usedForce && demandTransition.photos != null)
		{
			Game.serv.ui.AddPopup(new PhotoPopup(demandTransition.photos, null));
		}
		if (usedForce && demandTransition.forceToComplySocial.IsSet)
		{
			PlayerSocial social = demand.source.FindPlayer().social;
			EntityID targetId = demand.target.FindTargetPeepOrPlayerPeep();
			social.PerformSocialActionOn(demandTransition.forceToComplySocial, targetId, crewpeep);
		}
		if (usedForce && demandTransition.forceToComplyHeatBuff.IsSet)
		{
			PlayerTerritory territory = demand.source.FindPlayer().territory;
			Node node = nodeId.FindNode();
			if (node != null)
			{
				territory.AddHeatBuff(query: new ModQuery(demand.source, EntityID.INVALID, crewpeep, nodeId), node: node, id: demandTransition.forceToComplyHeatBuff, crewpeep: crewpeep);
				territory.RecomputeHeat(node, forceCurrent: true);
			}
		}
	}

	public void ForceExpireAllDemands(VisitState visit)
	{
		Demand demand = FindOrMake(visit.pid, MakeTargetForVisit(visit));
		demand.ResetDemandsTo(Demand.Type.None);
		demand.SetState(Demand.State.Neutral);
	}

	public Demand PerformAttackWhileCompliant(PlayerID source, Demand.Target target)
	{
		Demand demand = FindOrMake(source, target);
		if (true)
		{
			demand.SetState(Demand.State.Defiant);
			ProduceExplanationTicker(demand, Transition.AttackedWhileComplied);
		}
		return demand;
	}

	private void ProduceExplanationTicker(Demand demand, Transition t)
	{
		Demand.Target target = demand.target;
		string text = "";
		string text2 = (target.IsOwner ? target.peep.FindEntity().data.person.FullName : target.pid.FindPlayer().social.PlayerGroupName);
		switch (t)
		{
		case Transition.CompliesByRequest:
			text = (target.IsOwner ? Loc.Get("demand.explain.complies-request.si", "name", text2) : Loc.Get("demand.explain.complies-request.plu", "name", text2));
			break;
		case Transition.CompliesByForce:
			text = (target.IsOwner ? Loc.Get("demand.explain.complies-force.si", "name", text2) : Loc.Get("demand.explain.complies-force.plu", "name", text2));
			break;
		case Transition.CompliesByBribe:
			text = (target.IsOwner ? Loc.Get("demand.explain.complies-bribe.si", "name", text2) : Loc.Get("demand.explain.complies-bribe.plu", "name", text2));
			break;
		case Transition.AttackedWhileComplied:
			text = (target.IsOwner ? Loc.Get("demand.explain.defiant-attacked.si", "name", text2) : Loc.Get("demand.explain.defiant-attacked.plu", "name", text2));
			break;
		case Transition.ExpiredFromComplied:
			text = (target.IsOwner ? Loc.Get("demand.explain.defiant-expired.si", "name", text2) : Loc.Get("demand.explain.defiant-expired.plu", "name", text2));
			break;
		case Transition.DoesNotComply:
			text = (target.IsOwner ? Loc.Get("demand.explain.defiant-basic.si", "name", text2) : Loc.Get("demand.explain.defiant-basic.plu", "name", text2));
			break;
		default:
			Logger.Warning("Unknown type: " + t);
			break;
		}
		string text3 = "";
		if (t == Transition.CompliesByRequest || t == Transition.CompliesByForce)
		{
			string text4 = demand.EnumerateSetTypes().SelectToString((Demand.Type tt) => Loc.Get("demand.types.listel", "type", Loc.GetDemandType(tt)), "\n");
			text4 = (string.IsNullOrWhiteSpace(text4) ? Loc.Get("demand.types.list-empty") : text4);
			text3 = Loc.Get("demand.types.list-header", "types", text4);
		}
		string message = Loc.Get("demand.types.list-wrapper", "header", text, "rest", text3).Trim();
		TickerIcon icon = ((demand.IsStateCompliant && demand.HasDemandSet(Demand.Type.Protection)) ? TickerIcon.DEMANDS_PROTECTION : (demand.IsStateCompliant ? TickerIcon.DEMANDS_COMPLIANT : (demand.IsStateDefiant ? TickerIcon.DEMANDS_DEFIANT : TickerIcon.DEMANDS_GENERIC)));
		Game.ctx.hud.tickers.AddTextTicker(icon, TickerTitle.DEMANDS, message, target.FindNodeId());
	}

	private IRandom MakeTurnBasedRNG(EntityID eid)
	{
		return new SplitMix64((uint)(Game.ctx.clock.Now.days ^ eid.GetHashCode()));
	}

	public void Save(Serializer s, ConcurrentSaveTable results)
	{
		results.Set("data", s.Serialize(data));
	}

	public IEnumerator Load(Hashtable data)
	{
		SaveLoadUtils.DeserializeSingleKey(data, "data", delegate(DemandsTrackerPersistedData result)
		{
			this.data = result;
		});
		yield break;
	}
}

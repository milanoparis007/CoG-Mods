using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim;
using SomaSim.Util;

namespace Game.Session.Player.AI;

public sealed class SocialAdvisor : AIAdvisor
{
	public struct RequestParams
	{
		public bool accept;

		public int days;

		public int cost;

		public RequestParams(bool accept, int days, int cost)
		{
			this.accept = accept;
			this.days = days;
			this.cost = cost;
		}
	}

	private SocialAdvisorData _data;

	private SocialAdvisorConfig _def;

	public bool IsConvoInitiativeEnabled => _def.convoInitiative?.enabled ?? false;

	public bool HasConvoInitiative => _data.convoInitiative.IsValid;

	public SocialAdvisor(PlayerAI manager, NPCDefinition def)
		: base(manager, def)
	{
		_def = FindAdvisorConfig<SocialAdvisorConfig>(def.social);
		_data = _manager.Data.social ?? new SocialAdvisorData();
	}

	public override void Initialize()
	{
		base.Initialize();
		if (Game.ctx.IsSessionFromNewGame)
		{
			MeetNearbyOwners(printDebug: false);
		}
	}

	public override void OnTurnUpdate()
	{
		if (IsConvoInitiativeEnabled)
		{
			TryExpireConvoInitiative();
		}
	}

	public override void ProduceRequests(List<AdvisorRequest> results)
	{
		results.AddIfNotNull(ProduceSingleRequest());
	}

	private AdvisorRequest ProduceSingleRequest()
	{
		AdvisorRequest advisorRequest = null;
		if (advisorRequest == null && IsConvoInitiativeEnabled)
		{
			ConvoInitiative convoInitiative = _data.convoInitiative;
			if (convoInitiative.IsValid && !convoInitiative.IsExpired && convoInitiative.NeedToSendPeep())
			{
				advisorRequest = new AdvisorRequest(this, ScriptNames.GOTO_MEETING_POINT, AdvisorRequest.Priority.GoToMeetingPoint, new Deictics
				{
					targetNode = convoInitiative.meetingPoint
				});
			}
		}
		return advisorRequest;
	}

	public ModValue GetWrongFootValueOrNull()
	{
		return _def.wrongFootChance;
	}

	private void MeetNearbyOwners(bool printDebug)
	{
		SocialAdvisorConfig.KnownByOwners def = _def.knownByNearbyOwners;
		if (def == null || def.maxRadius <= 0f || def.chancePerOwner <= 0f)
		{
			return;
		}
		Node source = _player.territory.GetHeadquartersNode();
		if (source != null)
		{
			Game.ctx.board.nodes.VisitNeighborhoodBFS(source, 100, MeetNearbyOwnersAt, (Node node) => (node.pos - source.pos).Magnitude <= def.maxRadius, null, null, onlyBizNodes: true);
		}
		void MeetNearbyOwnersAt(Node node)
		{
			float chancePerOwner = _def.knownByNearbyOwners.chancePerOwner;
			foreach (EntityID item in node.interesting)
			{
				Entity entity = BuildingUtil.FindOwnerForAnyBuilding(item);
				if (entity != null && _data.rng.CheckProbability(chancePerOwner))
				{
					_player.social.MeetBuildingOwner(entity.Id, oldfriends: false, _player.social.PlayerPeepId);
					_ = printDebug;
				}
			}
		}
	}

	public void OnBeingAttacked(CombatResults results)
	{
		EntityID id = results.attacker.peep.Id;
		PlayerID pid = results.attacker.peep.data.agent.pid;
		if (IsCompliantTowards(pid))
		{
			RespondToAttackWhileCompliant(pid, id);
		}
		else if (IsDefiantTowards(pid))
		{
			RespondToAttackWhileDefiant(pid, results.nodeId);
			if (IsCompliantTowards(pid))
			{
				ShowCompliantAfterAttackFeedback(results.nodeId);
			}
		}
		if (results.target.IsDead)
		{
			RememberSocialActionOnMe(SocialConstants.GANG_DEATH, pid);
		}
		else if (!results.target.FindHealthType().canwork)
		{
			RememberSocialActionOnMe(SocialConstants.GANG_INJURY, pid);
		}
		_player.ai?.social?.ProcessAttackedBy(pid);
		void ShowCompliantAfterAttackFeedback(NodeID nodeId)
		{
			string message = "Your violent ways have prevailed, at least for now.\n\n" + _player.social.PlayerGroupName + " are no longer defiant, and will comply with your demands.";
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.DEMANDS_COMPLIANT, TickerTitle.DEMANDS, message, nodeId);
		}
	}

	public void RememberSocialActionOnMe(Label socialAction, PlayerID actor)
	{
		if (!(actor == PlayerID.System))
		{
			actor.FindPlayer().social.PerformSocialActionOn(socialAction, _player.social.PlayerPeepId, EntityID.INVALID);
		}
	}

	private bool IsDefiantTowards(PlayerID attacker)
	{
		return Game.ctx.simman.demands.FindOrNull(attacker, _pid)?.IsStateDefiant ?? false;
	}

	private bool IsNeutralOrUnmetTowards(PlayerID attacker)
	{
		return Game.ctx.simman.demands.FindOrNull(attacker, _pid)?.IsStateNeutral ?? true;
	}

	private bool IsCompliantTowards(PlayerID attacker)
	{
		return Game.ctx.simman.demands.FindOrNull(attacker, _pid)?.IsStateCompliant ?? false;
	}

	public bool RespondToRequestToComply(PlayerID requester, Demand.Type type, NodeID nodeId)
	{
		if (!IsNeutralOrUnmetTowards(requester))
		{
			return false;
		}
		Demand demand = Game.ctx.simman.demands.PerformRequestAIToComply(requester, _pid, type, nodeId);
		_ = demand.IsStateCompliant;
		return demand.IsStateCompliant;
	}

	public void RespondToAttackWhileDefiant(PlayerID attacker, NodeID nodeId)
	{
		if (IsDefiantTowards(attacker))
		{
			_ = Game.ctx.simman.demands.PerformForceAIToComply(attacker, _pid, Demand.Type.None, nodeId).IsStateCompliant;
		}
	}

	public void RespondToAttackWhileCompliant(PlayerID attacker, EntityID _)
	{
		if (IsCompliantTowards(attacker))
		{
			bool isStateDefiant = Game.ctx.simman.demands.PerformAttackWhileCompliant(attacker, Demand.Target.MakeForPlayer(_pid)).IsStateDefiant;
		}
	}

	public ConvoInitiative GetConvoInitiativeUnsafe()
	{
		return _data.convoInitiative;
	}

	public void StartConvoInitiative(PlayerID pid, ConvoInitiative.Topic topic, EntityID target)
	{
		NodeID meetingPoint = FindMeetingPoint(_player, pid.FindPlayer());
		_data.convoInitiative.Set(topic, pid, target, meetingPoint, _def.convoInitiative.timeoutTurns);
		string message = Loc.Get("ui.tickers.gangrequest.started", "groupname", _player.social.FindPlayerGroupNameColorized(), "date", Loc.FormatDateLong(_data.convoInitiative.expiration));
		Game.ctx.hud.tickers.AddTextTicker(TickerIcon.GANG_REQUEST.WrapInPlayerColor(_pid), TickerTitle.GANG_REQUEST, message, _data.convoInitiative.meetingPoint);
	}

	public void FinishConvoInitiative()
	{
		ClearConvoInitiative(success: true);
	}

	private void TryExpireConvoInitiative()
	{
		if (_data.convoInitiative.IsValid && _data.convoInitiative.IsExpired)
		{
			ClearConvoInitiative(success: false);
		}
	}

	private void ClearConvoInitiative(bool success)
	{
		if (!success)
		{
			string message = Loc.Get("ui.tickers.gangrequest.expired", "groupname", _player.social.FindPlayerGroupNameColorized());
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.GANG_REQUEST.WrapInPlayerColor(_pid), TickerTitle.GANG_REQUEST, message, _data.convoInitiative.meetingPoint);
		}
		_data.convoInitiative.Reset();
	}

	private NodeID FindMeetingPoint(PlayerInfo us, PlayerInfo them)
	{
		WorldPos pos = us.territory.GetHeadquartersNode().pos;
		WorldPos pos2 = them.territory.GetHeadquartersNode().pos;
		WorldPos pos3 = pos + (pos2 - pos) / 2f;
		Node source = Game.ctx.board.nodes.FindNearestNodeAround(pos3, 100f);
		Node neutral = null;
		Game.ctx.board.nodes.VisitNeighborhoodBFS(source, 50, delegate(Node node)
		{
			if (node.HasRoad && node.owner.IsNotSet)
			{
				neutral = node;
			}
		}, (Node node) => node.HasRoad, null, (Node node) => neutral != null, onlyBizNodes: true);
		if (neutral != null)
		{
			return neutral.id;
		}
		return us.territory.GetHeadquartersNode().id;
	}

	internal void InformOnCommandWaiting(PlayerID pid, NodeID nid, bool _)
	{
		_data.convoInitiative.MarkPeepAsSent();
	}

	internal bool ShouldContinueWaiting()
	{
		if (_data.convoInitiative.IsValid)
		{
			return !_data.convoInitiative.IsExpired;
		}
		return false;
	}

	public RequestParams ComputeRequestParametersForHumanAskingUs(VisitState state, ConvoInitiative.Topic requestType, object ctx = null)
	{
		return ComputeRequestParameters(state.pid, _pid, requestType, ctx);
	}

	public RequestParams ComputeRequestParametersForUsAskingHuman(PlayerID recipient, ConvoInitiative.Topic requestType, object ctx = null)
	{
		return ComputeRequestParameters(_pid, recipient, requestType, ctx);
	}

	public (bool accept, int days) ComputeRequestParametersForAIAskingUs(PlayerID sender, ConvoInitiative.Topic requestType, object ctx = null)
	{
		RequestParams requestParams = ComputeRequestParameters(sender, _pid, requestType, ctx);
		return (accept: requestParams.accept, days: requestParams.days);
	}

	private RequestParams ComputeRequestParameters(PlayerID sender, PlayerID recipient, ConvoInitiative.Topic requestType, object ctx = null)
	{
		var (fixnum, fixnum2, probability) = CalculateForAccepting(sender, recipient, requestType, ctx);
		return new RequestParams(Game.ctx.scenario.MakeSeededRng((uint)((_pid.id << 16) | Game.ctx.clock.CurrentTurn)).CheckProbability(probability), fixnum.IntFloor(), fixnum2.RoundCoarse());
	}

	private (Fixnum lengthDays, Fixnum acceptCost, Fixnum acceptProb) CalculateForAccepting(PlayerID sender, PlayerID recipient, ConvoInitiative.Topic requestType, object ctx = null)
	{
		switch (requestType)
		{
		case ConvoInitiative.Topic.TruceRequest:
			return _player.ai.combat.CalculateForAcceptingTruce(sender, recipient);
		case ConvoInitiative.Topic.JointWar:
		{
			PlayerID target = (PlayerID)ctx;
			return _player.ai.combat.CalculateForAcceptingJointWar(sender, recipient, target);
		}
		case ConvoInitiative.Topic.ExpansionHalt:
			return _player.ai.territory.CalculateForAcceptingExpansionTreaty(sender, recipient);
		case ConvoInitiative.Topic.StolenOutpost:
			return _player.ai.territory.CalculateForAcceptingOutpostAgreement(sender, recipient);
		default:
			return (lengthDays: 0, acceptCost: 0, acceptProb: 0);
		}
	}

	public void ProcessAttackedBy(PlayerID other)
	{
		_player.ai?.territory?.ProcessExpansionTreatyBreakingActionBy(other);
		_player.ai?.territory?.ProcessOutpostStealAgreementBreakingActionBy(other);
		_player.ai?.combat?.ProcessJointWarBreakingActionBy(other);
		_player.ai?.combat?.ProcessTruceBreakingActionBy(other);
	}
}

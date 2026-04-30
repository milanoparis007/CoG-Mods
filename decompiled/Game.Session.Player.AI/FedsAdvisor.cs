using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Assets;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using Game.UI.Session;
using SomaSim.Util;

namespace Game.Session.Player.AI;

public sealed class FedsAdvisor : AIAdvisor
{
	private FedsAdvisorData _data;

	private FedsAdvisorConfig _def;

	private FedInvestigation _pending;

	public FedsAdvisor(PlayerAI manager, NPCDefinition def)
		: base(manager, def)
	{
		_def = Game.serv.globals.settings.npc.FindAdvisorConfig<FedsAdvisorConfig>(def.feds);
		_data = _manager.Data.feds;
	}

	public override void OnTurnUpdate()
	{
	}

	public override void ProduceRequests(List<AdvisorRequest> results)
	{
		if (GetFakeHeadquartersNodeID().IsNotValid)
		{
			return;
		}
		if (_pending == null && _data.queue.Count > 0)
		{
			_pending = _data.queue.RemoveAndReturn(0);
		}
		if (_pending != null)
		{
			results.Add(new AdvisorRequest(this, ScriptNames.FED_INVESTIGATE, AdvisorRequest.Priority.PoliceOrFedVisit, new Deictics
			{
				targetNode = _pending.target,
				number = _pending.duration.deltadays,
				mySafehouse = _pending.FindPrecinctStation()
			}));
			AILog.LogAIDecision(_pid, this, $"Feds investigate at {_pending.target}");
		}
		if (_pending != null)
		{
			return;
		}
		if (_data.rng.CheckProbability(0.6f))
		{
			results.Add(new AdvisorRequest(this, ScriptNames.FED_SLEEP, AdvisorRequest.Priority.Default, new Deictics
			{
				mySafehouse = FedInvestigation.FindFirstPrecinctStation()
			}));
			return;
		}
		_data.precincts = (from p in Game.ctx.players.all
			where p.IsJustCop
			select p.ai.precinct.StationBuilding).ToList();
		results.Add(new AdvisorRequest(this, ScriptNames.FED_GOTO, AdvisorRequest.Priority.Default, new Deictics
		{
			mySafehouse = GetNextPrecinctStationToIdle()
		}));
	}

	public override void OnRequestDispatched(AdvisorRequest req, bool dispatched)
	{
		if (dispatched && req.script == ScriptNames.FED_INVESTIGATE)
		{
			_pending = null;
		}
	}

	internal EntityID GetNextPrecinctStationToIdle()
	{
		WorldPos agentpos = GetAgent().components.agent.GetNode().pos;
		List<EntityID> list = _data.precincts.Where((EntityID p) => p.IsValid).OrderBy(DistanceToAgent).Take(4)
			.ToList();
		return _data.rng.PickElement(list);
		float DistanceToAgent(EntityID x)
		{
			return (x.FindEntity().components.board.GetNode().pos - agentpos).Magnitude;
		}
	}

	public EntityID GetAgentID()
	{
		return _player.social.PlayerPeepId;
	}

	public Entity GetAgent()
	{
		return _player.social.PlayerPeepId.FindEntity();
	}

	internal NodeID GetFakeHeadquartersNodeID()
	{
		return Game.ctx.players.all.FirstOrDefault((PlayerInfo p) => p.IsJustCop)?.ai.precinct.GetPrecinctBuildingNodeID() ?? NodeID.INVALID;
	}

	public Node RequestInvestigation(PlayerID pid)
	{
		List<EntityID> allControlledBuildingsUnsafe = pid.FindPlayer().territory.GetAllControlledBuildingsUnsafe();
		Node node = _data.rng.PickElement(allControlledBuildingsUnsafe).FindEntity().components.board.GetNode();
		RequestInvestigation(node);
		return node;
	}

	internal void RequestInvestigation(Node node)
	{
		ModQuery query = new ModQuery(_pid, node);
		int days = _def.investigation.durationDayz.Evaluate(query).IntCeiling();
		FedInvestigation item = new FedInvestigation(node.id, SimTimeSpan.FromDays(days));
		_data.queue.Add(item);
	}

	internal void MarkInvestigationStart(NodeID nodeId, EntityID agentId)
	{
		Node node = nodeId.FindNode();
		Entity target = SafehouseUtils.FindAnyControlledBuildingAtNode(node);
		PlayerInfo playerInfo = target?.data.building.controlled?.Get().FindPlayer();
		if (playerInfo == null || playerInfo.PID.IsNotAnyPlayer)
		{
			return;
		}
		ModulesUtil.GetInventory(target).data.contents.Clear();
		Game.ctx.simman.cops.TryArrestSomeCrew(playerInfo, target);
		bool flag = playerInfo.PID.IsAIPlayer && Game.ctx.players.Human.meetings.IsPlayerMet(playerInfo.PID);
		bool isHuman = playerInfo.IsHuman;
		if (flag)
		{
			string text = playerInfo.social.FindPlayerGroupNameColorized();
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.COP_UPDATE, TickerTitle.COP_UPDATE, Loc.Get("ui.tickers.feds-raid-enemy", "groupname", text), target.Id);
		}
		if (isHuman)
		{
			List<PhotoConfig> raidPhotos = Game.serv.globals.settings.people.social.police.feds.raidPhotos;
			Game.serv.ui.AddPopup(new PhotoPopup(raidPhotos, SFXType.EventPoliceRaid, delegate
			{
				PersonInfoUtil.TweenCameraToEntity(target);
			}));
			string text2 = BuildingUtil.FindBuildingName(target);
			Game.ctx.hud.tickers.AddTextTicker(TickerIcon.COP_UPDATE, TickerTitle.COP_UPDATE, Loc.Get("ui.tickers.feds-raid-human", "bizname", text2), target.Id, TickerPersistType.Persist);
		}
		if (flag || isHuman)
		{
			Game.ctx.vfx.PlayOneShotPFX(PFXType.AttackFX, node.pos, PlayerID.HumanPlayer, 1f);
		}
	}

	internal void MarkInvestigationEnd()
	{
	}
}

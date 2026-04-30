using System.Collections;
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

namespace Game.Session.Player;

public sealed class PlayerMeetings : PlayerSubmanager
{
	private struct AggroResult
	{
		public PlayerInfo player;

		public Fixnum score;

		public static int LowestFirst(AggroResult a, AggroResult b)
		{
			return a.score.scaled - b.score.scaled;
		}
	}

	private PlayerVizData _vizdata;

	public override void OnPostSetDataSource(bool loaded)
	{
		_vizdata = _data.viz;
	}

	public override void OnPostInitialize()
	{
		base.OnPostInitialize();
		if (_pid.IsHumanPlayer)
		{
			Game.ctx.events.AddListener(SessionEventType.OnAfterAIInitNewGame, OnNewGame);
		}
	}

	private void OnNewGame(SessionEvent obj)
	{
		_player.meetings.MarkPlayersAsMetOneWay(_player);
		if (!Game.serv.globals.settings.general.debug.showGoonsAtStartup)
		{
			return;
		}
		foreach (PlayerInfo item in _manager.all)
		{
			if (item.IsGangOrGoon)
			{
				MarkPlayersAsMutuallyMet(item.PID);
			}
		}
	}

	public override void OnPreRelease()
	{
		if (_pid.IsHumanPlayer)
		{
			Game.ctx.events.RemoveListener(SessionEventType.OnAfterAIInitNewGame, OnNewGame);
		}
		base.OnPreRelease();
	}

	public bool IsNodeKnown(Node node)
	{
		return node.known.Get(_pid);
	}

	public void MarkNodeAsKnown(Node node, bool expectedSeen, bool instant)
	{
		MeetOtherPlayersAtNode(node);
		if (node.known.Get(_pid) == expectedSeen)
		{
			return;
		}
		node.known.Set(_pid, value: true);
		MeetOtherPlayersAroundNode(node);
		if (_pid.IsHumanPlayer)
		{
			Game.ctx.fogofwar.RevealNodeRegion(node, isFirstCall: true);
		}
		if (_pid.IsHumanPlayer && !instant)
		{
			bool interesting = node.interesting.Count > 0;
			Game.ctx.sfx.PlayCornerReveal(interesting);
		}
		foreach (EntityID item in node.contained)
		{
			MarkBuildingAsKnown(item.FindEntity(), expectedSeen, instant);
		}
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.PlayerExploredNode, EntityID.INVALID, _pid, node));
	}

	private void MeetOtherPlayersAroundNode(Node node)
	{
		NodeEdgeID[] edges = node.edges;
		for (int i = 0; i < edges.Length; i++)
		{
			NodeEdgeID edgeId = edges[i];
			if (edgeId.IsNotValid)
			{
				continue;
			}
			NodeEdge nodeEdge = edgeId.FindEdge();
			if (nodeEdge.IsRoad)
			{
				Node node2 = nodeEdge.FindOtherNode(node);
				if (node2 != null && node2 != node && !IsNodeKnown(node2))
				{
					MeetOtherPlayersAtNode(node2);
				}
			}
		}
	}

	private void MeetOtherPlayersAtNode(Node node)
	{
		PlayerID nodeOwner = PlayerTerritory.GetNodeOwner(node);
		if (nodeOwner.IsValid)
		{
			MarkPlayersAsMutuallyMet(nodeOwner, introduceLeadersToCrew: true);
		}
		foreach (EntityID item in Game.ctx.transit.GetAllAgentsAtNodeUnsafe(node.id))
		{
			PlayerID pid = item.FindEntity().data.agent.pid;
			if (pid != _pid && pid.IsAnyPlayer && pid != nodeOwner)
			{
				MarkPlayersAsMutuallyMet(pid, introduceLeadersToCrew: true);
			}
		}
	}

	private void MarkBuildingAsKnown(Entity e, bool seen, bool instant)
	{
		BoardComponent board = e?.components?.board;
		if (board == null)
		{
			Logger.Warning("Missing board component in ", e);
			return;
		}
		if (instant)
		{
			board.SetKnown(_pid, seen);
			return;
		}
		float seconds = e.components.ident.GetIdentityHashAsFloat() * 0.5f;
		TimerUtil.RunAfterTime(delegate
		{
			board.SetKnown(_pid, seen);
			Game.ctx.vfx.PlayOneShotPFX(PFXType.ExploreFX, e.data.board.worldpos, _pid, 1f);
		}, seconds);
	}

	public bool IsPlayerMet(PlayerID other)
	{
		return _vizdata.meetings.ContainsKey(other);
	}

	public IEnumerable<PlayerID> GetPlayersAlreadyMet()
	{
		return _vizdata.meetings.Keys;
	}

	public Dictionary<PlayerID, PlayerVizData.MeetingInfo> GetAllMeetingsUnsafe()
	{
		return _vizdata.meetings;
	}

	private void AddMeeting(PlayerID other, SimTime time)
	{
		_vizdata.meetings.Add(other, new PlayerVizData.MeetingInfo
		{
			pid = other,
			time = time
		});
	}

	public int CountGangsAlreadyMet()
	{
		return (from pid in GetPlayersAlreadyMet()
			where pid.FindPlayer().IsJustGang
			select pid).Count();
	}

	public void MarkPlayersAsMutuallyMet(PlayerID otherPid, bool introduceLeadersToCrew = false)
	{
		bool num = !IsPlayerMet(otherPid);
		PlayerInfo playerInfo = otherPid.FindPlayer();
		MarkPlayersAsMetOneWay(playerInfo);
		playerInfo.meetings.MarkPlayersAsMetOneWay(_player);
		if (introduceLeadersToCrew)
		{
			IntroducePlayerPeepToOtherCrew(_pid, otherPid);
			IntroducePlayerPeepToOtherCrew(otherPid, _pid);
		}
		if (num)
		{
			ApplyFirstImpressionsDebuffs(otherPid, playerInfo);
		}
	}

	private void ApplyFirstImpressionsDebuffs(PlayerID otherPid, PlayerInfo other)
	{
		if (!base.PlayerInfo.IsHuman || !Game.ctx.IsInteractive || !other.IsGangOrGoon || Game.ctx.tutorial.ShowingLesson)
		{
			return;
		}
		Xorshift rng = other.ai.Data.social.rng;
		ModValue modValue = other.ai?.social?.GetWrongFootValueOrNull();
		if (modValue != null)
		{
			Fixnum probability = modValue.Evaluate(otherPid);
			if (rng.CheckProbability(probability))
			{
				other.social.GetRelationshipFromPlayerTo(_player.PID).AddBuffNoCrew(BuffConstants.RELBUFF_WRONG_FOOT);
			}
		}
	}

	private void MarkPlayersAsMetOneWay(PlayerInfo other)
	{
		if (other != null && !IsPlayerMet(other.PID))
		{
			AddMeeting(other.PID, Game.ctx.clock.Now);
			Node headquartersNode = other.territory.GetHeadquartersNode(ignoreWarnings: true);
			if (headquartersNode != null)
			{
				MarkNodeAsKnown(headquartersNode, expectedSeen: true, instant: false);
			}
			if (_player.IsHuman)
			{
				RevealAllUnitsOfPlayer(other.PID);
				Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.PlayerVizChanged, EntityID.INVALID, other.PID));
				Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.HumanPlayerMetPlayer, EntityID.INVALID, _pid, other.PID));
				MaybeShowTicker();
			}
		}
		void MaybeShowTicker()
		{
			if (Game.ctx.IsInteractive && other.IsAnyAIPlayer && !Game.ctx.hud.console.IsShowing)
			{
				string key = (other.IsJustGang ? "ui.tickers.newplayer.gang.message" : (other.IsJustGoon ? "ui.tickers.newplayer.goon.message" : "ui.tickers.newplayer.cops.message"));
				string text = other.social.FindPlayerGroupNameColorized();
				string message = Loc.Get(key, "groupname", text);
				EntityID safehouse = other.territory.Safehouse;
				Game.ctx.hud.tickers.AddTextTicker(TickerIcon.NEW_PLAYER, TickerTitle.NEW_PLAYER, message, safehouse);
				SFXType fx = (other.IsJustGang ? SFXType.EventMetGang : SFXType.EventMetGoon);
				Game.serv.audio.PlayUISFX(fx);
			}
		}
	}

	public static void RevealAllUnitsOfPlayer(PlayerID pid)
	{
		ModelManager models = Game.ctx.models;
		foreach (EntityID car in Game.ctx.transit.data.cars)
		{
			Entity entity = car.FindEntity();
			if (entity.data.mobile.pid == pid)
			{
				models.RevealModelIfHidden(entity);
			}
		}
	}

	public void EnsureMetPlayersKnowCrewSymmetric()
	{
		foreach (KeyValuePair<PlayerID, PlayerVizData.MeetingInfo> meeting in _vizdata.meetings)
		{
			IntroducePlayerPeepToOtherCrew(_pid, meeting.Key);
			IntroducePlayerPeepToOtherCrew(meeting.Key, _pid);
		}
	}

	public static void IntroducePlayerPeepToOtherCrew(PlayerID a, PlayerID b, bool force = false)
	{
		if ((!Game.ctx.IsInteractive && !force) || !a.IsValid || !b.IsValid || !(a != b))
		{
			return;
		}
		PlayerSocial social = a.FindPlayer().social;
		foreach (CrewAssignment item in b.FindPlayer().crew.AllCrew)
		{
			social.FindOrMakeRelationshipsWith(item.peepId);
		}
	}

	public bool IsAttackingMe(PlayerInfo player)
	{
		return player.ai?.combat?.IsAttackAllowed(_pid) == true;
	}

	public void ProducePlayersAttackingMe(List<PlayerInfo> results)
	{
		results.Clear();
		foreach (PlayerInfo item in Game.ctx.players.all)
		{
			if (item.IsAnyAIPlayer && IsAttackingMe(item))
			{
				results.Add(item);
			}
		}
	}

	public PlayerInfo FindPotentialHitmanTarget(PlayerID goonPid)
	{
		using ListPool<PlayerInfo>.PooledBlockList pooledBlockList = ListPool<PlayerInfo>.Allocate();
		using ListPool<AggroResult>.PooledBlockList pooledBlockList2 = ListPool<AggroResult>.Allocate();
		ProduceMutualsWhoAreAggroOnMe(goonPid, pooledBlockList);
		ProduceAggroScores(pooledBlockList, pooledBlockList2);
		pooledBlockList2.StableSort((AggroResult a, AggroResult b) => AggroResult.LowestFirst(a, b));
		return pooledBlockList2.FirstOrDefaultFast().player;
	}

	private void ProduceMutualsWhoAreAggroOnMe(PlayerID goonPid, List<PlayerInfo> results)
	{
		PlayerInfo goon = goonPid.FindPlayer();
		ProducePlayersAttackingMe(results);
		for (int num = results.Count - 1; num >= 0; num--)
		{
			bool num2 = GoonKnows(goon, results[num]);
			bool isCrewDefeated = results[num].crew.IsCrewDefeated;
			if (!num2 || isCrewDefeated)
			{
				results.RemoveAt(num);
			}
		}
	}

	private void ProduceAggroScores(List<PlayerInfo> players, List<AggroResult> results)
	{
		foreach (PlayerInfo player in players)
		{
			Fixnum score = player.social.GetRelationshipFromSourceToPlayer(_pid)?.Evaluate().current ?? ((Fixnum)0);
			results.Add(new AggroResult
			{
				player = player,
				score = score
			});
		}
	}

	private static bool GoonKnows(PlayerInfo goon, PlayerInfo candidate)
	{
		if (goon != candidate)
		{
			return goon.social.GetRelationshipFromPlayerTo(candidate.social.PlayerPeepId) != null;
		}
		return false;
	}

	protected override void InitializeConsoleEntries()
	{
		base.InitializeConsoleEntries();
		Game.ctx.console.Add(this, new DebugConsoleEntry("reveal", "all-buildings", CheatRevealAll));
		Game.ctx.console.Add(this, new DebugConsoleEntry("reveal", "no-buildings", CheatRevealNone));
		Game.ctx.console.Add(this, new DebugConsoleEntry("reveal", "player-existence", CheatRevealPlayer));
		foreach (EntityConfig value in Game.ctx.entityman.GetAllTemplatesUnsafe().Values)
		{
			if (value.residence != null || value.biz != null)
			{
				string second = value.Template.String;
				Game.ctx.console.Add(this, new DebugConsoleEntry("reveal", "template", second, CheatRevealTemplates));
			}
		}
		foreach (IModuleConfig item in ModulesUtil.FindAllModuleDefsExpensive())
		{
			Game.ctx.console.Add(this, new DebugConsoleEntry("reveal", "module", item.Id.String, CheatRevealModuleId));
		}
		foreach (Label allTag in Game.serv.globals.settings.tags.allTags)
		{
			Game.ctx.console.Add(this, new DebugConsoleEntry("reveal", "taggedmodule", allTag.String, CheatRevealModuleTag));
		}
	}

	protected override void ReleaseConsoleEntries()
	{
		Game.ctx.console.Remove(this);
		base.ReleaseConsoleEntries();
	}

	internal void DebugRevealAllBuildings()
	{
		CheatRevealAll(null);
	}

	private string CheatRevealAll(string[] arg)
	{
		Game.serv.sequencer.StartCoroutineTask(CheatRevealAllCoroutine());
		return "Marking all buildings as revealed";
	}

	private IEnumerator CheatRevealAllCoroutine()
	{
		Game.ctx.fogofwar.RevealEntireMap();
		List<Node> copy = new List<Node>(Game.ctx.board.nodes.GetAllNodesUnsafe());
		for (int i = 0; i < copy.Count; i++)
		{
			Node node = copy[i];
			MarkNodeAsKnown(node, expectedSeen: true, instant: true);
			if (i % 10 == 0)
			{
				yield return null;
			}
		}
	}

	private string CheatRevealNone(string[] arg)
	{
		foreach (Node item in Game.ctx.board.nodes.GetAllNodesUnsafe())
		{
			MarkNodeAsKnown(item, expectedSeen: false, instant: true);
		}
		return "Marked all buildings as unknown";
	}

	private string CheatRevealPlayer(string[] args)
	{
		if (args.Length != 3 || !short.TryParse(args[2], out var result))
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<player-id>");
		}
		PlayerID playerID = new PlayerID(result);
		if (playerID.IsNotValid || playerID.id <= 0 || playerID.id >= Game.ctx.players.all.Count)
		{
			return $"Invalid player id, expected a value in [ 1, {Game.ctx.players.all.Count - 1} ]";
		}
		MarkPlayersAsMutuallyMet(playerID);
		HUDUtil.GoTo(Game.ctx.players.WithID(playerID).territory.Safehouse.FindEntity().data.board.worldpos, zoomIn: false, showFx: true);
		return $"Marked player {playerID} as met.";
	}

	private string CheatRevealTemplates(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<module-id>");
		}
		string text = args[2];
		foreach (Entity item in Game.ctx.entityman.GetCachedEntitiesByTemplateUnsafe((Label)text))
		{
			Entity e = item;
			if (item.config.biz != null)
			{
				e = item.components.biz.BuildingID.FindEntity();
			}
			MarkBuildingAsKnown(e, seen: true, instant: true);
		}
		return "Marked all " + text + " as revealed";
	}

	private string CheatRevealModuleId(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<module-id>");
		}
		Label label = (Label)args[2];
		foreach (Entity item in Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe())
		{
			ModulesComponent modules = item.components.modules;
			if (modules != null && modules.HasModuleInstalled(label))
			{
				MarkBuildingAsKnown(item, seen: true, instant: true);
			}
		}
		return $"Revealed all buildings with module {label}";
	}

	private string CheatRevealModuleTag(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<module-id>");
		}
		Label label = (Label)args[2];
		foreach (Entity item in Game.ctx.entityman.GetCachedEntitiesBuildingsUnsafe())
		{
			ModulesComponent modules = item.components.modules;
			if (modules != null && modules.HasModulesByTag(label))
			{
				MarkBuildingAsKnown(item, seen: true, instant: true);
			}
		}
		return $"Revealed all buildings with modules tagged with {label}";
	}
}

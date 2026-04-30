using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim;
using Game.UI.Session;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Player;

public sealed class PlayerSocial : PlayerSubmanager
{
	private PlayerSocialData _socdata;

	private Dictionary<NodeID, ListPool<Fixnum>.PooledBlockList> _tmpNodeRelationships = new Dictionary<NodeID, ListPool<Fixnum>.PooledBlockList>();

	public TransactionHistory Transactions
	{
		get
		{
			if (!_pid.IsHumanPlayer)
			{
				return null;
			}
			return _socdata.transactions;
		}
	}

	public Label PlayerEthnicity => _data.social.eth;

	public string PlayerFullName => _data.social.fullname;

	public string PlayerLastName => _data.social.lastname;

	public EntityID PlayerPeepId => _data.social.peepId;

	public string PlayerGroupName => _data.social.groupname;

	public EntityID FrancineID => _socdata.francine;

	public bool DebugLogAllHistory => _socdata.historyledger != null;

	public override void OnPostSetDataSource(bool loaded)
	{
		_socdata = _data.social;
		if (_pid.IsHumanPlayer)
		{
			_socdata.transactions = _socdata.transactions ?? new TransactionHistory();
		}
	}

	protected override void InitializeConsoleEntries()
	{
		base.InitializeConsoleEntries();
		Game.ctx.console.Add(this, new DebugConsoleEntry("ai", "log-history-leger", CheatLogAllHistory));
	}

	protected override void ReleaseConsoleEntries()
	{
		Game.ctx.console.Remove(this);
		base.ReleaseConsoleEntries();
	}

	public Relationship GetRelationshipFromPlayerTo(PlayerID targetId)
	{
		return GetRelationshipFromPlayerTo(targetId.FindPlayer().social.PlayerPeepId);
	}

	public Relationship GetRelationshipFromPlayerTo(EntityID targetId)
	{
		return Game.ctx.simman.rels.GetOrNull(PlayerPeepId, targetId);
	}

	public Relationship GetRelationshipFromSourceToPlayer(PlayerID sourceId)
	{
		return GetRelationshipFromSourceToPlayer(sourceId.FindPlayer().social.PlayerPeepId);
	}

	public Relationship GetRelationshipFromSourceToPlayer(EntityID sourceId)
	{
		return Game.ctx.simman.rels.GetOrNull(sourceId, PlayerPeepId);
	}

	public Relationship GetRelationshipFromSourceToPlayer(VisitState visit)
	{
		return GetRelationshipFromSourceToPlayer(visit?.npc?.Id ?? EntityID.INVALID);
	}

	public (Relationship to, Relationship from) FindOrMakeRelationshipsWith(PlayerID targetId, RelationshipType type = RelationshipType.Acquaintance, bool warnOnExisting = false)
	{
		return FindOrMakeRelationshipsWith(targetId.FindPlayer().social.PlayerPeepId, type, warnOnExisting);
	}

	public (Relationship to, Relationship from) FindOrMakeRelationshipsWith(EntityID targetId, RelationshipType type = RelationshipType.Acquaintance, bool warnOnExisting = false)
	{
		return Game.ctx.simman.rels.GetOrMakeSymmetrical(PlayerPeepId, targetId, type, warnOnExisting);
	}

	public Fixnum EvaluateRelationshipFromPlayerTo(PlayerID targetId)
	{
		return EvaluateRelationshipFromPlayerTo(targetId.FindPlayer().social.PlayerPeepId);
	}

	public Fixnum EvaluateRelationshipFromPlayerTo(EntityID targetId)
	{
		return GetRelationshipFromPlayerTo(targetId)?.Evaluate().current ?? Fixnum.ZERO;
	}

	public Fixnum EvaluateRelationshipFromSourceToPlayer(EntityID sourceId)
	{
		return GetRelationshipFromSourceToPlayer(sourceId)?.Evaluate().current ?? Fixnum.ZERO;
	}

	public void ResetRelationshipSymmetrical(EntityID targetId)
	{
		if (!PlayerPeepId.IsNotValid && !targetId.IsNotValid)
		{
			var (relationship, relationship2) = Game.ctx.simman.rels.GetOrNullSymmetrical(PlayerPeepId, targetId);
			relationship?.RemoveAllBuffs();
			relationship2?.RemoveAllBuffs();
		}
	}

	public void PerformSocialActionOn(Label socialAction, PlayerID targetId, EntityID sourceId, ExtendHistoryInfo ext = null)
	{
		PerformSocialActionOn(socialAction, targetId.FindPlayer().social.PlayerPeepId, sourceId, ext);
	}

	public void PerformSocialActionOn(Label socialAction, EntityID targetId, EntityID sourceId, ExtendHistoryInfo ext = null)
	{
		PerformSocialActionOn(socialAction, targetId, sourceId, QuestUUID.EMPTY, ext);
	}

	public void PerformSocialActionOn(Label socialAction, EntityID targetId, EntityID sourceId, QuestUUID quuid, ExtendHistoryInfo ext = null)
	{
		Relationship item = FindOrMakeRelationshipsWith(targetId).from;
		item.GetOrCreateHistory().InformOfSocialAction(socialAction, item, targetId, sourceId, quuid, inferred: false, ext);
	}

	public void RemoveAllSocialActionsOn(EntityID targetId)
	{
		Relationship item = FindOrMakeRelationshipsWith(targetId).from;
		item.GetHistoryOrNull()?.RemoveAllSocialActions(item);
	}

	public bool ContainsSocialActionBy(PlayerID instigator, Label action)
	{
		return GetRelationshipFromPlayerTo(instigator)?.GetHistoryOrNull()?.ContainsAction(action) == true;
	}

	public Entity GetPlayerPeep()
	{
		return _data.social.peepId.FindEntity();
	}

	public EthnicityDef FindPlayerEthnicityDef()
	{
		return Game.serv.globals.settings.ethnicities.FindEthnicityDef(_data.social.eth);
	}

	public string FindPlayerGroupNameColorized(float brightness = 0.5f)
	{
		return WrapInPlayerColor(PlayerGroupName, brightness);
	}

	public string FindPlayerPeepNameColorized(float brightness = 0.5f)
	{
		return WrapInPlayerColor(PlayerFullName, brightness);
	}

	public string WrapInPlayerColor(string message, float brightness = 0.5f)
	{
		Color colorForPlayerText = Game.ctx.mapdisplay.GetColorForPlayerText(_player, brightness);
		return TextUtil.ColorWrap(message, colorForPlayerText.ToTMProFormat());
	}

	public string FindDemandTargetDescOrNull(PlayerID source)
	{
		Demand demand = Game.ctx.simman.demands.FindOrNull(source, _pid);
		if (demand != null)
		{
			if (!demand.IsStateCompliant)
			{
				if (!demand.IsStateDefiant)
				{
					return null;
				}
				return Loc.Get("demand.state.defiant.paren");
			}
			return Loc.Get("demand.state.compliant.paren");
		}
		return null;
	}

	public void SetBossInfo(Entity peep, bool rerollGroupName = true)
	{
		_data.social.peepId = peep.Id;
		_data.social.fullname = peep.data.person.FullName;
		_data.social.lastname = peep.data.person.LastName;
		_data.social.eth = peep.data.person.Ethnicity;
		if (rerollGroupName)
		{
			string key = NameUtils.FindNamePattern(_player.GetNpcDef()?.type ?? PlayerType.HumanPlayer);
			_data.social.groupname = NameUtils.LocWithContext(key, peep);
		}
	}

	public void ForceGroupNameIfValid(string groupname)
	{
		if (!string.IsNullOrWhiteSpace(groupname))
		{
			_data.social.groupname = groupname;
		}
	}

	public bool IsBoss(Entity peep)
	{
		return _data.social.peepId == peep.Id;
	}

	public void OnConvoStartedWith(Entity npc)
	{
		GetRelationshipFromSourceToPlayer(npc.Id)?.IncrementConvoCount();
	}

	public void ExploreFamilyAtStartup(Entity peep)
	{
		foreach (Relationship datum in Game.ctx.simman.rels.GetListOrNull(PlayerPeepId).data)
		{
			if (datum.IsAnyFamily && !datum.IsSelf)
			{
				EntityID to = datum.to;
				AddFamilyRelBuffAtStartup(PlayerPeepId, to);
				DiscoverWorkplace(to, instant: true);
				MeetFamilyAtStartup(to, peep.Id);
			}
		}
	}

	public Entity FindFrancine()
	{
		return _socdata.francine.FindEntity();
	}

	internal void AddFrancineRelBuff(EntityID peepId, EntityID francineId)
	{
		_socdata.francine = francineId;
		Game.ctx.simman.rels.GetOrNullSymmetrical(peepId, francineId).fromTarget?.AddBuff(BuffConstants.STARTING_BUFF_FRANCINE, peepId);
	}

	internal void AddFamilyRelBuffAtStartup(EntityID peepId, EntityID relativeId)
	{
		var (relationship, relationship2) = Game.ctx.simman.rels.GetOrNullSymmetrical(peepId, relativeId);
		if (relationship == null || relationship2 == null)
		{
			Logger.Warning($"Family relationship missing between {peepId} and {relativeId}; fwd={relationship}, inv={relationship2}");
		}
		else if (relationship.IsCloseFamily && relationship2.IsCloseFamily)
		{
			relationship.AddBuff(BuffConstants.STARTING_BUFF_FAMILY, peepId);
			relationship2.AddBuff(BuffConstants.STARTING_BUFF_FAMILY, peepId);
		}
		else if (relationship.IsExtendedFamily && relationship2.IsExtendedFamily)
		{
			relationship.AddBuff(BuffConstants.STARTING_BUFF_FAMILY_EXT, peepId);
			relationship2.AddBuff(BuffConstants.STARTING_BUFF_FAMILY_EXT, peepId);
		}
		else if (!relationship.IsAnyFamily || !relationship2.IsAnyFamily)
		{
			Logger.Warning($"Failed to find family relationships! fwd={relationship}, inv={relationship2}");
		}
		else
		{
			Logger.Warning($"Asymmetrical family relationships! fwd={relationship}, inv={relationship2}");
		}
	}

	public void DiscoverSomeoneAndTheirWorkplace(EntityID peepId)
	{
		FindOrMakeRelationshipsWith(peepId);
		DiscoverWorkplace(peepId, instant: false);
	}

	private void DiscoverWorkplace(EntityID peepId, bool instant)
	{
		Entity entity = peepId.FindEntity();
		if (entity != null && entity.data.person.IsEmployed)
		{
			Entity entity2 = BuildingUtil.FindBuildingForBizOwner(entity);
			if (entity2 != null)
			{
				Node node = entity2.data.board.bead.nodeId.FindNode();
				_player.territory.ScopeOutBuilding(entity2, instant, setControlled: false);
				_player.meetings.MarkNodeAsKnown(node, expectedSeen: true, instant);
			}
		}
	}

	public void MeetCrew(EntityID crewId, bool self)
	{
		if (self)
		{
			Game.ctx.simman.rels.GetOrCreate(crewId, crewId, RelationshipType.Self, warnOnExisting: true).AddBuffNoCrew(BuffConstants.STARTING_BUFF_SELF);
			return;
		}
		var (relationship, relationship2) = MeetSymmetrical(crewId, RelationshipType.Acquaintance, oldfriends: false, PlayerPeepId);
		relationship.AddBuff(BuffConstants.RELBUFF_PLAYER_TO_CREW, crewId);
		relationship2.AddBuff(BuffConstants.RELBUFF_CREW_TO_PLAYER, crewId);
	}

	public void UnmeetCrew(EntityID crewId)
	{
		var (relationship, relationship2) = FindOrMakeRelationshipsWith(crewId);
		relationship.RemoveBuff(BuffConstants.RELBUFF_PLAYER_TO_CREW);
		relationship2.RemoveBuff(BuffConstants.RELBUFF_CREW_TO_PLAYER);
	}

	public void MeetBuildingOwner(EntityID ownerId, bool oldfriends, EntityID crew)
	{
		MeetSymmetrical(ownerId, RelationshipType.Acquaintance, oldfriends, crew);
	}

	public void MeetFamilyAtStartup(EntityID npc, EntityID crew)
	{
		MeetSymmetrical(npc, RelationshipType.Acquaintance, oldfriends: false, crew);
	}

	private (Relationship relto, Relationship relfrom) MeetSymmetrical(EntityID otherId, RelationshipType type, bool oldfriends, EntityID crew)
	{
		var (relationship, relationship2) = FindOrMakeRelationshipsWith(otherId, type);
		ApplyBuffsOnMeeting(relationship2, oldfriends, crew);
		ApplyBuffsOnMeeting(relationship, oldfriends, crew);
		return (relto: relationship, relfrom: relationship2);
	}

	private static void ApplyBuffsOnMeeting(Relationship rel, bool oldfriends, EntityID crew)
	{
		rel.AddBuff(BuffConstants.BUFF_ON_SCOPEOUT_NPC, crew);
		if (oldfriends && !rel.HasBuff(BuffConstants.STARTING_BUFF_SELF) && !rel.HasBuff(BuffConstants.STARTING_BUFF_FAMILY) && !rel.HasBuff(BuffConstants.STARTING_BUFF_FAMILY_EXT))
		{
			rel.AddBuff(BuffConstants.STARTING_BUFF_OLDFRIENDS, crew);
		}
		if (rel.from.FindEntity().data.person.eth == rel.to.FindEntity().data.person.eth)
		{
			rel.AddBuff(BuffConstants.BUFF_ON_SCOPEOUT_SAMEETH, crew);
		}
	}

	public List<Relationship> GetAllPlayerRelationshipsUnsafe()
	{
		return Game.ctx.simman.rels.GetListOrNull(PlayerPeepId)?.data;
	}

	public void ProduceAverageRelPerNode(List<(NodeID, Fixnum)> results)
	{
		results.Clear();
		List<Relationship> allPlayerRelationshipsUnsafe = GetAllPlayerRelationshipsUnsafe();
		if (allPlayerRelationshipsUnsafe == null || allPlayerRelationshipsUnsafe.Count == 0)
		{
			return;
		}
		foreach (Relationship item2 in allPlayerRelationshipsUnsafe)
		{
			Entity entity = BuildingUtil.FindBuildingForBizOwner(item2.to);
			if (entity == null)
			{
				continue;
			}
			NodeID nodeId = entity.data.board.bead.nodeId;
			if (!nodeId.IsNotValid)
			{
				if (!_tmpNodeRelationships.TryGetValue(nodeId, out var value))
				{
					ListPool<Fixnum>.PooledBlockList pooledBlockList = (_tmpNodeRelationships[nodeId] = ListPool<Fixnum>.Allocate());
					value = pooledBlockList;
				}
				Fixnum current2 = item2.Evaluate().current;
				value.Add(current2);
			}
		}
		foreach (KeyValuePair<NodeID, ListPool<Fixnum>.PooledBlockList> tmpNodeRelationship in _tmpNodeRelationships)
		{
			Fixnum item = tmpNodeRelationship.Value.AverageFast() ?? Fixnum.ZERO;
			results.Add((tmpNodeRelationship.Key, item));
		}
		foreach (var result in results)
		{
			ListPool<Fixnum>.Free(_tmpNodeRelationships[result.Item1]);
		}
		_tmpNodeRelationships.Clear();
	}

	public bool AreIllegalItemsLocked(Entity owner, Entity building)
	{
		BusinessSettings.BuySellIllegalEtc buySellIllegalEtc = Game.serv.globals.settings.people.businessSettings.buySellIllegalEtc;
		NodeID nodeId = building?.components.board.GetNodeID() ?? NodeID.INVALID;
		Fixnum fixnum = buySellIllegalEtc.minrel.Evaluate(new ModQuery(_pid, owner.Id, nodeId));
		return (GetRelationshipFromSourceToPlayer(owner.Id)?.Evaluate().high ?? Fixnum.ZERO) < fixnum;
	}

	public int GetSocialTicketsAvailable(EntityID peepId)
	{
		return GetRelationshipFromSourceToPlayer(peepId)?.GetTicketsAvailable() ?? 0;
	}

	public int GetSocialTicketsSpent(EntityID peepId)
	{
		return GetRelationshipFromSourceToPlayer(peepId)?.GetTicketsSpent() ?? 0;
	}

	internal List<Entity> TicketActionFindIntroTargets(VisitState visit)
	{
		Entity playerPeep = GetPlayerPeep();
		Entity npc = visit.npc;
		return SocQ.OrderBy(from peep in SocQ.FindPeople(playerPeep, npc, SocQ.IsNotKnownToPlayer)
			where peep.data.person.IsEmployed
			select peep, playerPeep, npc, SocQ.DistanceToPlayer).ToList();
	}

	internal List<Entity> TicketActionFindBoostTargets(Entity owner)
	{
		Entity playerPeep = GetPlayerPeep();
		return SocQ.OrderBy(from e in SocQ.FindPeople(playerPeep, owner, SocQ.IsKnownToPlayerAndOther)
			where IsValidBoostTarget(e)
			select e, playerPeep, owner, SocQ.RelationshipToPlayer).ToList();
	}

	private static bool IsValidBoostTarget(Entity peep)
	{
		if (peep.data.person.IsEmployed)
		{
			return true;
		}
		if (CopUtil.IsCop(peep))
		{
			return true;
		}
		return false;
	}

	internal List<(Entity peep, float val)> TicketActionFindGoonIntroTickets(VisitState visit)
	{
		Entity player = GetPlayerPeep();
		Entity npc = visit.npc;
		return (from e in SocQ.FindPeople(player, npc, SocQ.IsKnownToPlayerAndOther).Where(delegate(Entity peep)
			{
				if (!peep.data.person.IsAlive)
				{
					return false;
				}
				PlayerInfo playerInfo = peep.data.agent?.pid.FindPlayer();
				return playerInfo != null && playerInfo.IsJustGoon && playerInfo.social.PlayerPeepId == peep.Id;
			}).Select(delegate(Entity peep)
			{
				float item = (float)(Game.ctx.simman.rels.GetOrNull(peep.Id, player.Id)?.Evaluate().current ?? ((Fixnum)0));
				return (peep: peep, val: item);
			})
			orderby e.val
			select e).ToList();
	}

	internal bool TicketActionPerformBoost(EntityID targetId)
	{
		AddBuffFrom(targetId, BuffConstants.TICKET_NPCBOOST);
		Game.ctx.events.SendImmediate(new SessionEvent(SessionEventType.AchieveRelBoost, targetId, Game.ctx.players.Human.PID));
		PersonInfoUtil.TweenCameraToEntity(targetId);
		return true;
	}

	public bool AddBuffFrom(EntityID peepId, Label buffId)
	{
		return FindOrMakeRelationshipsWith(peepId).from.AddBuff(buffId, PlayerPeepId);
	}

	public bool RemoveBuffFrom(EntityID peepId, Label buffId)
	{
		return GetRelationshipFromSourceToPlayer(peepId)?.RemoveBuff(buffId) ?? false;
	}

	public bool SpendTickets(VisitState visit, int count = 1)
	{
		Relationship relationshipFromSourceToPlayer = visit.GetPlayer().social.GetRelationshipFromSourceToPlayer(visit.npc.Id);
		if (relationshipFromSourceToPlayer == null || relationshipFromSourceToPlayer.GetTicketsAvailable() <= 0)
		{
			Logger.Error("Missing rel tickets for " + visit);
			return false;
		}
		bool num = relationshipFromSourceToPlayer.DoSpendTickets(count);
		if (num)
		{
			visit.crew.GetPeep()?.components.agent.IncrementStat(CrewStats.FavorsRedeemed, count);
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.PlayerUsedSocialTicket, visit.npc.Id, _pid));
			Game.ctx.events.SendImmediate(SessionEventType.AchieveUseTickets);
		}
		return num;
	}

	public void GrantFreebieTickets(EntityID other, int count = 1)
	{
		FindOrMakeRelationshipsWith(other).from.GrantFreebieTickets(count);
		Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.PlayerUsedSocialTicket, other, _pid));
	}

	public IntroductionType GetRandomIntroductionType(VisitState visit)
	{
		uint identityHash = visit.npc.components.ident.GetIdentityHash();
		DateTime dateTime = Game.ctx.clock.Now.ToDate();
		SplitMix64 rng = new SplitMix64((uint)((int)identityHash + dateTime.Month + dateTime.Year));
		return Game.serv.globals.settings.people.social.intros.GetRandomIntroType(rng);
	}

	public static bool IsEligibleCrewMember(SimTime now, Entity person)
	{
		PersonData person2 = person.data.person;
		if (person2.IsAlive && person2.GetAge(now).YearsFloat >= 20f && person2.business.IsNotValid && person2.resassigned.IsNotValid && Game.ctx.simman.politics.GetPoliticianData(person.Id) == null)
		{
			return person.data.agent.pid.id == 0;
		}
		return false;
	}

	public static bool IsEligiblePotentialBizOwner(SimTime now, Entity person)
	{
		PersonData person2 = person.data.person;
		if (person2.IsAlive && !Game.ctx.players.Human.gambling.IsGamblingSomewhere(person) && person2.GetAge(now).YearsFloat >= 35f && person2.business.IsNotValid && person2.resassigned.IsNotValid)
		{
			return person.data.agent.pid.id == 0;
		}
		return false;
	}

	public static bool IsEligibleGambler(SimTime now, Entity person)
	{
		return IsEligiblePotentialBizOwner(now, person);
	}

	public static EntityID FindRelativeToOwnBiz(VisitState visit)
	{
		return FindConnectionToOwnBiz(visit.npc, closeFamilyOnly: true);
	}

	public static EntityID FindAIConnectionToOwnBiz(PlayerInfo player)
	{
		foreach (CrewAssignment item in player.crew.AllCrew)
		{
			Entity peep = item.GetPeep();
			if (peep != null)
			{
				EntityID result = FindConnectionToOwnBiz(peep, closeFamilyOnly: false);
				if (result.IsValid)
				{
					return result;
				}
			}
		}
		return FindAnyoneToOwnBiz();
	}

	private static EntityID FindConnectionToOwnBiz(Entity npc, bool closeFamilyOnly)
	{
		SimTime now = Game.ctx.clock.Now;
		foreach (Relationship datum in Game.ctx.simman.rels.GetListOrNull(npc.Id).data)
		{
			if (!closeFamilyOnly || datum.IsCloseFamily)
			{
				Entity entity = datum.to.FindEntity();
				if (IsEligiblePotentialBizOwner(now, entity))
				{
					return entity.Id;
				}
			}
		}
		return EntityID.INVALID;
	}

	public static EntityID FindAnyoneToOwnBiz()
	{
		SimTime now = Game.ctx.clock.Now;
		foreach (Entity item in Game.ctx.entityman.GetCachedEntitiesPersonsUnsafe())
		{
			if (IsEligiblePotentialBizOwner(now, item))
			{
				return item.Id;
			}
		}
		return EntityID.INVALID;
	}

	public static IEnumerable<EntityID> ProduceFamily(EntityID peepId, bool onlyclose)
	{
		RelationshipList listOrNull = Game.ctx.simman.rels.GetListOrNull(peepId);
		foreach (Relationship datum in listOrNull.data)
		{
			if (onlyclose ? datum.IsCloseFamily : datum.IsAnyFamily)
			{
				yield return datum.to;
			}
		}
	}

	public List<PlayerInfo> GetAllKnownCops()
	{
		IEnumerable<PlayerInfo> enumerable = Game.ctx.players.all.Where((PlayerInfo x) => x.IsJustCop);
		List<PlayerInfo> list = new List<PlayerInfo>();
		foreach (PlayerInfo item in enumerable)
		{
			if (_player.meetings.IsPlayerMet(item.PID))
			{
				list.Add(item);
			}
		}
		return list;
	}

	public List<PlayerInfo> GetAllGangsAggroOnMe()
	{
		return Game.ctx.players.all.Where((PlayerInfo x) => x.IsJustGang && x.ai.combat.IsAggroAnyType(_player.PID)).ToList();
	}

	public static (PlayerID evaluator, EntityID targetPeep, EntityID evaluatorPeep) GetEvaluatorAndTargetForAcceptingGangCooperation(PlayerID sender, PlayerID recipient)
	{
		EntityID playerPeepId = sender.FindPlayer().social.PlayerPeepId;
		EntityID playerPeepId2 = recipient.FindPlayer().social.PlayerPeepId;
		bool isHumanPlayer = recipient.IsHumanPlayer;
		PlayerID item = (isHumanPlayer ? sender : recipient);
		EntityID item2 = (isHumanPlayer ? playerPeepId : playerPeepId2);
		EntityID item3 = (isHumanPlayer ? playerPeepId2 : playerPeepId);
		return (evaluator: item, targetPeep: item3, evaluatorPeep: item2);
	}

	public static (PlayerID evaluator, EntityID targetPeep, EntityID evaluatorPeep) GetEvaluatorAndTargetForAskingGangCooperation(PlayerID sender, PlayerID recipient)
	{
		EntityID playerPeepId = sender.FindPlayer().social.PlayerPeepId;
		EntityID playerPeepId2 = recipient.FindPlayer().social.PlayerPeepId;
		return (evaluator: sender, targetPeep: playerPeepId2, evaluatorPeep: playerPeepId);
	}

	private string CheatLogAllHistory(string[] _)
	{
		_socdata.historyledger = _socdata.historyledger ?? new List<HistoryLedgerItem>();
		return "All social actions will be saved in a list called 'historyledger'.";
	}

	public static void DebugLogSocialHistory(Relationship rel, SocialActionInfo info, ExtendHistoryInfo fn = null)
	{
		PlayerSocial social = Game.ctx.players.Human.social;
		if (social.DebugLogAllHistory)
		{
			HistoryLedgerItem historyLedgerItem = new HistoryLedgerItem
			{
				time = Game.ctx.clock.Now,
				from = rel.from,
				to = rel.to,
				info = info,
				eventname = info.defid.String,
				target = info.entityCtx
			};
			if (fn != null)
			{
				historyLedgerItem = fn(historyLedgerItem);
			}
			social._socdata.historyledger.Add(historyLedgerItem);
		}
	}

	public static void DebugLogAIHistory(PlayerID pid, PlayerID target, Entity ctx, Node node, string eventname, string method)
	{
		PlayerSocial social = Game.ctx.players.Human.social;
		if (social.DebugLogAllHistory)
		{
			EntityID valueOrDefault = (pid.FindPlayer()?.social?.PlayerPeepId).GetValueOrDefault();
			EntityID valueOrDefault2 = (target.FindPlayer()?.social?.PlayerPeepId).GetValueOrDefault();
			HistoryLedgerItem item = new HistoryLedgerItem
			{
				time = Game.ctx.clock.Now,
				eventname = eventname,
				method = method,
				from = valueOrDefault,
				to = valueOrDefault2,
				target = (ctx?.Id ?? default(EntityID)),
				node = (node?.id ?? default(NodeID))
			};
			social._socdata.historyledger.Add(item);
		}
	}
}

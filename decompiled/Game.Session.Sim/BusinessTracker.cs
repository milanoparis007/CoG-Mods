using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Session.HUD;
using Game.UI.Session.Tickers;
using SomaSim.SION;
using SomaSim.Util;

namespace Game.Session.Sim;

public sealed class BusinessTracker : ISystemTurnSubManager<SimulationManager>, ISubManager<SimulationManager>, ISaveLoadProvider, ILoadObserver
{
	private struct FriendCandidate
	{
		public Entity owner;

		public Entity building;

		public Label eth;

		public WorldPos pos;
	}

	public class EventHistoryListItem : ItemListDialog.IEntry, IComparable
	{
		public TickerTarget target;

		public string title;

		public string icon;

		public string description;

		public SimTime date;

		public EventHistoryListItem()
		{
		}

		public EventHistoryListItem(TickerData data)
		{
			title = data.title.GetText();
			icon = data.icon.GetIcon();
			description = data.message;
			date = data.date;
			target = data.target;
		}

		public string GetDebug()
		{
			return "Event " + title;
		}

		public string GetName()
		{
			return title;
		}

		public string GetIcon()
		{
			return icon;
		}

		public string GetDescription()
		{
			return Loc.Get("ui.tickers-list.entry", "title", title, "date", Loc.FormatDateLong(date), "description", description);
		}

		public bool ShowGoTo()
		{
			return target.IsSet;
		}

		public void OnGoTo()
		{
			target.TweenCamera();
		}

		public int CompareTo(object obj)
		{
			if (!(obj is EventHistoryListItem eventHistoryListItem))
			{
				return 0;
			}
			return date.days - eventHistoryListItem.date.days;
		}
	}

	public BusinessTrackerPersistedData data;

	private SimulationManager _mgr;

	private HashSet<Entity> _bizCacheAll;

	private HashSet<Entity> _bizWithoutOwners;

	private BusinessPotentialOwnersCache _potentialOwnersCache;

	private const int eventHistoryDisplayTime = 30;

	public const float MIN_BIZ_AGE = 18f;

	public const float MAX_BIZ_AGE = 60f;

	public void Initialize(SimulationManager manager)
	{
		_mgr = manager;
		data = new BusinessTrackerPersistedData();
		_bizCacheAll = new HashSet<Entity>();
		_bizWithoutOwners = new HashSet<Entity>();
		_potentialOwnersCache = new BusinessPotentialOwnersCache();
		_mgr.peoplegen.OnBeforePersonDeath.Add(OnPersonDeath);
		Game.ctx.events.AddListener(SessionEventType.OnAfterAIInitNewGame, OnSetupOrchestratorFinished);
	}

	public void Release()
	{
		Game.ctx.events.RemoveListener(SessionEventType.OnAfterAIInitNewGame, OnSetupOrchestratorFinished);
		_mgr.peoplegen.OnBeforePersonDeath.Remove(OnPersonDeath);
		foreach (Entity item in _bizCacheAll)
		{
			DestroyBusinessAtShutdown(item);
		}
		_potentialOwnersCache.Clear();
		_bizWithoutOwners.Clear();
		_bizCacheAll.Clear();
		_mgr = null;
	}

	private void DestroyBusinessAtShutdown(Entity business)
	{
		bool shutdown = true;
		ClearOwner(business, shutdown);
		DetachBusinessFromBuilding(business, shutdown);
		DestroyBusinessUnattached(business, shutdown);
	}

	internal Entity CreateBusinessUnattached(EntityConfig config)
	{
		Entity entity = Game.ctx.entityman.CreateByTemplate(config);
		_bizCacheAll.Add(entity);
		_bizWithoutOwners.Add(entity);
		return entity;
	}

	internal void AttachBusinessToBuilding(Entity business, Entity building)
	{
		building.components.building.AttachBusiness(business);
	}

	private bool FindAndAssignRealOwner(Entity business, Entity building, Entity oldOwner)
	{
		Entity entity = FindBestOwner(business, building, oldOwner);
		if (entity == null)
		{
			return false;
		}
		business.components.biz.AssignRealOwner(entity);
		_bizWithoutOwners.Remove(business);
		return true;
	}

	private bool FindAndAssignFakeOwner(Entity business, Entity building, bool checkCityGen)
	{
		Node node = building.components.board.GetNode();
		Label mainEthnicityAtNode = Game.ctx.board.GetMainEthnicityAtNode(node);
		business.components.biz.AssignFakeOwner(mainEthnicityAtNode);
		_bizWithoutOwners.Remove(business);
		return true;
	}

	public void ClearOwner(Entity business, bool shutdown)
	{
		business.components.biz.ClearOwner(shutdown);
		if (!shutdown)
		{
			_bizWithoutOwners.Add(business);
		}
	}

	public void DetachBusinessFromBuilding(Entity business, bool shutdown)
	{
		Entity entity = business.data.biz.building.FindEntity();
		if (entity != null)
		{
			DetachBusinessFromBuilding(business, entity, shutdown);
		}
		else
		{
			Logger.Warning("Unattached business " + business);
		}
	}

	public void DetachBusinessFromBuilding(Entity business, Entity building, bool shutdown)
	{
		building.components.building.DetachBusiness(shutdown);
	}

	public void DestroyBusinessUnattached(Entity business, bool shutdown)
	{
		Game.ctx.entityman.DestroyEntity(business, shutdown);
		if (!shutdown)
		{
			_bizCacheAll.Remove(business);
			_bizWithoutOwners.Remove(business);
		}
	}

	public void RunNewGameBusinessAssignments()
	{
		using (new BlockStopwatch("sim", "Assigned owners"))
		{
			TryAssignOwnersAsNeeded();
		}
		_ = _bizWithoutOwners.Count;
		_ = 0;
		CountAllFakeBusinesses();
		CountAllRealBusinesses();
		List<Entity> list = new List<Entity>();
		SimTime now = Game.ctx.clock.Now;
		_mgr.peoplegen.ProducePeopleWhere((Entity person) => CanPersonOwnBusiness(person, now), list, clearFirst: true);
		list.Clear();
	}

	private void OnSetupOrchestratorFinished(SessionEvent sev)
	{
		if (sev.type == SessionEventType.OnAfterAIInitNewGame)
		{
			TickBusinessesAndRespect(initial: true);
			MakeBusinessOwnerFriendships();
		}
	}

	public void OnSystemTurn()
	{
		_potentialOwnersCache.Clear();
		if (_bizWithoutOwners.Count > 0 && Game.ctx.IsInteractive)
		{
			TryAssignOwnersAsNeeded();
		}
		if (Game.ctx.IsInteractive)
		{
			using (new BlockStopwatch("respect", "Respect update"))
			{
				TickBusinessesAndRespect(initial: false);
			}
		}
	}

	public void OnNPCSafehouseCreation()
	{
		TryAssignOwnersAsNeeded();
	}

	private static void TickBusinessesAndRespect(bool initial)
	{
		BusinessUpdate.Tick(initial);
	}

	public IEnumerable<Entity> GetAllBizWithOwnersUnsafe()
	{
		return _bizCacheAll.Where((Entity e) => e.data.biz.owner.IsOwnerSet);
	}

	public int CountAllRealBusinesses()
	{
		return _bizCacheAll.Where((Entity e) => e.data.biz.owner.IsReal).Count();
	}

	public int CountAllFakeBusinesses()
	{
		return _bizCacheAll.Where((Entity e) => e.data.biz.owner.IsFake).Count();
	}

	private void TryAssignOwnersAsNeeded()
	{
		if (_bizWithoutOwners.Count == 0)
		{
			return;
		}
		_ = _bizWithoutOwners.Count;
		List<Entity> list = new List<Entity>(_bizWithoutOwners);
		list.Sort((Entity a, Entity b) => a.Id.index - b.Id.index);
		data.rng.Shuffle(list);
		while (list.Count > 0)
		{
			Entity biz = list.RemoveLast();
			if (!AssignOwnerToBusiness(biz))
			{
				break;
			}
		}
		_ = list.Count;
	}

	private bool AssignOwnerToBusiness(Entity biz, Entity previousOwner = null)
	{
		Entity entity = BuildingUtil.FindBuildingForBiz(biz);
		if (entity == null)
		{
			return true;
		}
		if (entity.data.building.interesting || entity.components.building.IsSafehouse)
		{
			return FindAndAssignRealOwner(biz, entity, previousOwner);
		}
		return FindAndAssignFakeOwner(biz, entity, checkCityGen: true);
	}

	public void ForceAssignOwner(Entity business, Entity owner)
	{
		if (BuildingUtil.FindBuildingForBiz(business) != null)
		{
			business.components.biz.AssignRealOwner(owner);
			_bizWithoutOwners.Remove(business);
		}
	}

	public void RefreshOwnerAfterLosingControlled(Entity biz, Entity building)
	{
		FindAndAssignFakeOwner(biz, building, checkCityGen: false);
	}

	private List<FriendCandidate> GetBizOwnersForScoring()
	{
		EntityManager entityman = Game.ctx.entityman;
		List<FriendCandidate> list = new List<FriendCandidate>();
		foreach (Entity item in _bizCacheAll)
		{
			BizData biz = item.data.biz;
			Entity entity = entityman.Find(biz.owner.id);
			if (entity != null)
			{
				Entity entity2 = entityman.Find(biz.building);
				WorldPos worldpos = entity2.data.board.worldpos;
				Label eth = entity.data.person.eth;
				list.Add(new FriendCandidate
				{
					owner = entity,
					building = entity2,
					pos = worldpos,
					eth = eth
				});
			}
		}
		return list;
	}

	private bool FillFriendCandidateScores(FriendCandidate me, List<FriendCandidate> candidates, List<float> scores)
	{
		float num = 0f;
		int i = 0;
		for (int count = candidates.Count; i < count; i++)
		{
			float num2 = 0f;
			FriendCandidate friendCandidate = candidates[i];
			if (me.owner != friendCandidate.owner)
			{
				float magnitude = (friendCandidate.pos - me.pos).Magnitude;
				num2 = MathUtil.ClampMin(30f - magnitude, 0f);
				if (me.eth == friendCandidate.eth)
				{
					num2 *= 10f;
				}
			}
			num += num2;
			scores[i] = num2;
		}
		if (num > 0f)
		{
			int j = 0;
			for (int count2 = candidates.Count; j < count2; j++)
			{
				scores[j] /= num;
			}
		}
		return num > 0f;
	}

	private void MakeBusinessOwnerFriendships()
	{
		List<FriendCandidate> bizOwnersForScoring = GetBizOwnersForScoring();
		List<FriendCandidate> list = new List<FriendCandidate>(bizOwnersForScoring);
		List<float> list2 = ListGenerators.ListOfDefaultValues<float>(list.Count);
		foreach (FriendCandidate item in bizOwnersForScoring)
		{
			if (data.rng.CheckProbability(1f) && FillFriendCandidateScores(item, list, list2))
			{
				FriendCandidate friendCandidate = data.rng.PickElementOrLast(list, list2, normalized: true);
				Game.ctx.simman.rels.GetOrMakeSymmetrical(item.owner.Id, friendCandidate.owner.Id, RelationshipType.Acquaintance, warnOnExisting: false);
			}
		}
	}

	private bool HasOwner(Entity business)
	{
		return business.data.biz.owner.IsOwnerSet;
	}

	private bool HasNoOwner(Entity business)
	{
		return business.data.biz.owner.IsOwnerNotSet;
	}

	private Entity FindBestOwner(Entity _, Entity building, Entity excludeOwner)
	{
		Node node = building.components.board.GetNode();
		return _potentialOwnersCache.Find(node, data.rng, excludeOwner);
	}

	internal static bool CanPersonOwnBusiness(Entity person, SimTime now)
	{
		PersonData person2 = person.data.person;
		if (!person2.IsAlive)
		{
			return false;
		}
		float yearsFloat = person2.GetAge(now).YearsFloat;
		if (yearsFloat <= 18f || yearsFloat > 60f)
		{
			return false;
		}
		if (person2.business.IsValid)
		{
			return false;
		}
		return true;
	}

	private void OnPersonDeath(Entity person)
	{
		EntityID business = person.data.person.business;
		if (business.IsValid)
		{
			SwitchBusinessOwnersOnDeath(person, business);
		}
	}

	private void SwitchBusinessOwnersOnDeath(Entity previousOwner, EntityID ownedBiz)
	{
		BuildingAndBusinessData buildingAndBusinessData = BuildingUtil.FindDataForBiz(ownedBiz);
		ClearOwner(buildingAndBusinessData.biz, shutdown: false);
		AssignOwnerToBusiness(buildingAndBusinessData.biz, previousOwner);
		BizOwner owner = buildingAndBusinessData.biz.data.biz.owner;
		foreach (PlayerInfo item in Game.ctx.players.all)
		{
			Relationship relationshipFromSourceToPlayer = item.social.GetRelationshipFromSourceToPlayer(previousOwner.Id);
			if (relationshipFromSourceToPlayer != null && relationshipFromSourceToPlayer.type == RelationshipType.Acquaintance)
			{
				item.social.FindOrMakeRelationshipsWith(owner.id);
			}
		}
	}

	public void Save(Serializer s, ConcurrentSaveTable results)
	{
		results.Set("data", s.Serialize(data));
	}

	public IEnumerator Load(Hashtable data)
	{
		SaveLoadUtils.DeserializeSingleKey(data, "data", delegate(BusinessTrackerPersistedData result)
		{
			this.data = result;
		});
		yield break;
	}

	public void OnAfterManagerLoad()
	{
	}

	public void OnAfterEntityLoad()
	{
		_bizCacheAll = new HashSet<Entity>(Game.ctx.entityman.GetCachedEntitiesBizUnsafe());
		_bizWithoutOwners = new HashSet<Entity>(_bizCacheAll.Where(HasNoOwner));
		_potentialOwnersCache.Clear();
	}

	internal void AddEvent(EventHistoryListItem eData)
	{
		data.eventHistory.Insert(0, eData);
		CheckEventRecency();
	}

	public void CheckEventRecency()
	{
		data.eventHistory.Sort();
		data.eventHistory.Reverse();
		for (int i = 0; i < data.eventHistory.Count; i++)
		{
			if (data.eventHistory[i].date.days < Game.ctx.clock.Now.days - 30)
			{
				data.eventHistory.RemoveRange(i, data.eventHistory.Count - i);
				break;
			}
		}
	}

	public void ShowEventHistoryList()
	{
		CheckEventRecency();
		List<ItemListDialog.IEntry> entries = data.eventHistory.Cast<ItemListDialog.IEntry>().ToList();
		Game.ctx.hud.itemList.ShowEntries(Loc.Get("ui.tickers-list.title"), Loc.Get("ui.tickers-list.subtitle"), entries);
	}

	internal void HideEventHistoryList()
	{
		Game.ctx.hud.itemList.Hide();
	}
}

using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Player.AI;

internal sealed class BusinessCandidateCache
{
	internal sealed class Controlled
	{
		public Entity building;

		public Node node;

		public List<MfgItem> allItems;

		public Controlled(Entity building, List<MfgItem> allItems)
		{
			this.building = building;
			node = building.components.board.GetNode();
			this.allItems = allItems;
		}
	}

	internal sealed class Candidate
	{
		public Entity biz;

		public Entity building;

		public Node node;

		public List<MfgItem> allItems;

		public Candidate(Entity biz, Entity building, List<MfgItem> allItems)
		{
			this.biz = biz;
			this.building = building;
			node = building.components.board.GetNode();
			this.allItems = allItems;
		}
	}

	internal sealed class ScopedOut
	{
		public Entity building;

		public Node node;

		public List<MfgItem> unlockedItems = new List<MfgItem>();

		public ScopedOut(Entity building)
		{
			this.building = building;
			node = building.components.board.GetNode();
		}
	}

	public Dictionary<EntityID, Candidate> candidates;

	public Dictionary<EntityID, ScopedOut> scoped;

	public Dictionary<EntityID, Controlled> controlled;

	private PlayerID _pid;

	private PlayerInfo _player;

	private BusinessAdvisorConfig _def;

	public BusinessCandidateCache(PlayerInfo player, BusinessAdvisorConfig def)
	{
		candidates = new Dictionary<EntityID, Candidate>(new EntityIDEqualityComparer());
		scoped = new Dictionary<EntityID, ScopedOut>(new EntityIDEqualityComparer());
		controlled = new Dictionary<EntityID, Controlled>(new EntityIDEqualityComparer());
		_player = player;
		_pid = player.PID;
		_def = def;
	}

	public void PopulateCacheAtStartup()
	{
		UpdatePlayerControlledBuildings();
		candidates.Clear();
		foreach (Entity item in Game.ctx.entityman.GetCachedEntitiesBizUnsafe())
		{
			AddCandidateAtStartup(item);
		}
	}

	private void AddCandidateAtStartup(Entity biz)
	{
		if (!biz.data.biz.owner.IsReal)
		{
			return;
		}
		Entity entity = biz.data.biz.building.FindEntity();
		List<MfgItem> list = FindBizModuleItems(entity);
		if (list.Count != 0)
		{
			Candidate value = new Candidate(biz, entity, list);
			candidates.Add(entity.Id, value);
			if (_player.territory.IsScoped(entity))
			{
				UpdateBuilding(entity);
			}
		}
	}

	public void FindAllScopedBuildingsToTradeWith(List<ScopedOut> results)
	{
		Fixnum maxdist = _def.trades.maxDistanceFromBuilding.Evaluate(new ModQuery(_pid));
		results.ClearAndAddRange(scoped.Values.Where((ScopedOut e) => IsAvailableToTrade(e, maxdist)));
	}

	private bool IsAvailableToTrade(ScopedOut scoped, Fixnum maxdist)
	{
		if (scoped.building.components.building.GetControllingPlayer() != _pid && IsNotInAnothersTerritory(scoped))
		{
			return IsWithinRadiusToControlled(scoped, maxdist);
		}
		return false;
	}

	private bool IsNotInAnothersTerritory(ScopedOut scoped)
	{
		PlayerSingleFlagWithHistory owner = scoped.node.owner;
		if (!owner.IsNotSet)
		{
			return owner.pid == _pid;
		}
		return true;
	}

	private bool IsWithinRadiusToControlled(ScopedOut scoped, Fixnum max)
	{
		foreach (Controlled value in controlled.Values)
		{
			if ((Fixnum)(value.node.pos - scoped.node.pos).Magnitude <= max)
			{
				return true;
			}
		}
		return false;
	}

	public void UpdateOnBuildingStateChange(Entity building)
	{
		UpdateBuilding(building);
	}

	public void UpdateOnSkillUnlock()
	{
		foreach (ScopedOut value in scoped.Values)
		{
			UpdateBuilding(value.building);
		}
	}

	private void UpdateScopedStatus(Entity building)
	{
		if (candidates.ContainsKey(building.Id))
		{
			bool num = _player.territory.IsScoped(building);
			bool flag = scoped.ContainsKey(building.Id);
			if (!num && flag)
			{
				scoped.Remove(building.Id);
			}
			if (num && !flag)
			{
				scoped[building.Id] = new ScopedOut(building);
			}
		}
	}

	private void UpdateBuilding(Entity building)
	{
		if (building.components.police != null)
		{
			return;
		}
		UpdateScopedStatus(building);
		ScopedOut scopedOut = scoped.FindOrNull(building.Id);
		if (scopedOut != null)
		{
			scopedOut.unlockedItems.Clear();
			Candidate candidate = candidates.FindOrNull(building.Id);
			if (candidate != null)
			{
				FillWithUnlockedItems(candidate.allItems, scopedOut.unlockedItems);
			}
		}
	}

	public void UpdatePlayerControlledBuildings()
	{
		controlled.Clear();
		foreach (EntityID item in _player.territory.GetAllControlledBuildingsUnsafe())
		{
			Entity entity = item.FindEntity();
			List<MfgItem> allItems = FindBizModuleItems(entity);
			Controlled value = new Controlled(entity, allItems);
			controlled[entity.Id] = value;
		}
	}

	private List<MfgItem> FindBizModuleItems(Entity building)
	{
		List<IBizModule> bizModules = ModulesUtil.GetBizModules(building);
		if (bizModules == null)
		{
			return new List<MfgItem>(0);
		}
		List<MfgItem> list = new List<MfgItem>(8);
		foreach (IBizModule item in bizModules)
		{
			foreach (MfgItem item2 in item.ProduceAllItemsInCurrentRecipe())
			{
				if (!list.Contains(item2))
				{
					list.Add(item2);
				}
			}
		}
		return list;
	}

	public void FillWithUnlockedItems(List<MfgItem> allItems, List<MfgItem> results)
	{
		List<Label> unlockedResourcesUnsafe = _player.skills.GetUnlockedResourcesUnsafe();
		results.Clear();
		foreach (MfgItem allItem in allItems)
		{
			if (unlockedResourcesUnsafe.Contains(allItem.id))
			{
				results.Add(allItem);
			}
		}
	}
}

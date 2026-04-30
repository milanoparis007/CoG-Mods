using System;
using Game.Core;
using Game.Services.Store;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Entities;

public sealed class BuildingComponent : BaseComponent, IEntityEventObserverComponent
{
	[Flags]
	public enum BuildingTypeFlags
	{
		None = 0,
		Business = 1,
		Residence = 2,
		Police = 4,
		Civic = 8,
		CivicPolice = 0xC
	}

	public BuildingConfig Config => _baseConfig as BuildingConfig;

	public BuildingData Data => _entity.data.building;

	public bool HasBizOwner => _entity.data.building.business.IsValid;

	public bool IsBusinessBuildingType => IsBusiness(GetBuildingType());

	public bool IsResidenceBuildingType => IsResidence(GetBuildingType());

	public bool IsPoliceBuildingType => IsPolice(GetBuildingType());

	public bool IsCivicBuildingType => IsCivic(GetBuildingType());

	public bool IsPoliceStation
	{
		get
		{
			if (_entity.components.police != null)
			{
				return _entity.components.police.HasStation;
			}
			return false;
		}
	}

	public PlayerID SafehouseOwner => Data.safehouse;

	public bool IsSafehouse => Data.safehouse.IsAnyPlayer;

	public PlayerID OutpostOwner => Data.outpost;

	public bool IsOutpost => Data.outpost.IsAnyPlayer;

	public bool IsInteresting => Data.interesting;

	public bool IsPotential => Data.potential;

	internal bool IsPickSuppressed => Data.suppressPick;

	public bool IsPoliticalWard
	{
		get
		{
			CivicConfig civic = _entity.config.civic;
			if (civic != null && civic.assignment == CivicConfig.CivicAssignment.PoliticalWard)
			{
				return Game.serv.store.IsPackInstalled(PackID.ShadowGovernment);
			}
			return false;
		}
	}

	public void AttachBusiness(Entity business)
	{
		Entity entity = _entity;
		entity.data.building.business = business.Id;
		business.data.biz.building = entity.Id;
		InstallModulesFromBusiness(business, entity);
	}

	public void DetachBusiness(bool shutdown)
	{
		Entity entity = _entity;
		Entity entity2 = entity.data.building.business.FindEntity();
		RemoveModulesFromBusiness(entity2, entity, shutdown);
		entity.data.building.business = 0uL;
		entity2.data.biz.building = 0uL;
	}

	public EntityID GetAttachedBusiness()
	{
		return _entity.data.building.business;
	}

	private void InstallModulesFromBusiness(Entity business, Entity building)
	{
		building.components.modules.InstallModules(business.data.biz.modules, Game.ctx.clock.Now);
	}

	private void RemoveModulesFromBusiness(Entity business, Entity building, bool shutdown)
	{
		building.components.modules.RemoveModules(business.data.biz.modules, shutdown);
	}

	public void OnEntityEvent(EntityEventType type)
	{
		if (type == EntityEventType.EntityActivationChanged)
		{
			Game.ctx.selection.ProcessBuildingActivation(_entity, _entity.components.selection.IsActive);
		}
	}

	public static bool HasBuildingType(BuildingTypeFlags flags, BuildingTypeFlags mask)
	{
		return (mask & flags) == mask;
	}

	public static bool IsBusiness(BuildingTypeFlags flags)
	{
		return HasBuildingType(flags, BuildingTypeFlags.Business);
	}

	public static bool IsResidence(BuildingTypeFlags flags)
	{
		return HasBuildingType(flags, BuildingTypeFlags.Residence);
	}

	public static bool IsPolice(BuildingTypeFlags flags)
	{
		return HasBuildingType(flags, BuildingTypeFlags.Police);
	}

	public static bool IsCivic(BuildingTypeFlags flags)
	{
		return HasBuildingType(flags, BuildingTypeFlags.Civic);
	}

	public BuildingTypeFlags GetBuildingType()
	{
		BuildingTypeFlags buildingTypeFlags = BuildingTypeFlags.None;
		if (_entity.data.building.business.IsValid)
		{
			buildingTypeFlags |= BuildingTypeFlags.Business;
		}
		if (_entity.components.police != null)
		{
			buildingTypeFlags |= BuildingTypeFlags.Police;
		}
		if (_entity.components.residence != null)
		{
			buildingTypeFlags |= BuildingTypeFlags.Residence;
		}
		if (_entity.components.civic != null)
		{
			buildingTypeFlags |= BuildingTypeFlags.Civic;
		}
		return buildingTypeFlags;
	}

	public void SetSafehouseOwner(PlayerID pid)
	{
		Data.safehouse = pid;
	}

	public void ClearSafehouse()
	{
		Data.safehouse = PlayerID.System;
	}

	public bool IsSafehouseOf(PlayerID pid)
	{
		if (Data.safehouse.IsAnyPlayer)
		{
			return Data.safehouse == pid;
		}
		return false;
	}

	public bool IsSafehouseNotOf(PlayerID pid)
	{
		if (Data.safehouse.IsAnyPlayer)
		{
			return Data.safehouse != pid;
		}
		return false;
	}

	public void SetOutpost(PlayerID pid)
	{
		Data.outpost = pid;
	}

	public void ClearOutpost()
	{
		Data.outpost = PlayerID.System;
	}

	public bool IsOutpostOf(PlayerID pid)
	{
		if (Data.outpost.IsAnyPlayer)
		{
			return Data.outpost == pid;
		}
		return false;
	}

	public bool IsOutpostNotOf(PlayerID pid)
	{
		if (Data.outpost.IsAnyPlayer)
		{
			return Data.outpost != pid;
		}
		return false;
	}

	public void SetInteresting(bool interesting)
	{
		Data.interesting = interesting;
	}

	public void SetPotential(bool potential)
	{
		Data.potential = potential;
	}

	internal void SetScopedBy(PlayerID pid, bool scoped)
	{
		Data.scoped.Set(pid, scoped);
	}

	internal bool IsScopedBy(PlayerID pid)
	{
		return Data.scoped.Get(pid);
	}

	internal PlayerID GetControllingPlayer()
	{
		return Data.controlled.Get();
	}

	internal bool IsControlledBy(PlayerID pid)
	{
		return Data.controlled.Get() == pid;
	}

	internal bool IsControlledByAnyPlayer()
	{
		return Data.controlled.Get().IsAnyPlayer;
	}

	internal bool IsControlledNotBy(PlayerID pid)
	{
		if (IsControlledByAnyPlayer())
		{
			return !IsControlledBy(pid);
		}
		return false;
	}

	internal void SetControlledBy(PlayerID pid)
	{
		Data.controlled.Set(pid);
	}

	internal void ClearControlledBy()
	{
		Data.controlled.Clear();
	}

	internal BuildingData.Health GetHealthDataOrNull()
	{
		return Data.health;
	}

	internal BuildingData.Health GetHealthDataOrAdd()
	{
		BuildingData data = Data;
		BuildingData.Health obj = Data.health ?? BuildingData.Health.Make(_entity, Data.controlled.pid);
		BuildingData.Health result = obj;
		data.health = obj;
		return result;
	}

	internal void SetHealth(Fixnum value)
	{
		GetHealthDataOrAdd().current = value;
	}

	internal Fixnum GetHealth()
	{
		return GetHealthDataOrNull()?.current ?? Fixnum.ZERO;
	}

	internal void RemoveHealth()
	{
		Data.health = null;
	}

	internal bool HasDamage()
	{
		return GetHealthDataOrNull()?.IsNotMax ?? false;
	}

	internal bool IsSafehouseOrControlledBy(PlayerID pid)
	{
		if (!IsSafehouseOf(pid))
		{
			return IsControlledBy(pid);
		}
		return true;
	}

	internal bool IsSafehouseOrControlledByAny()
	{
		if (!IsSafehouse)
		{
			return IsControlledByAnyPlayer();
		}
		return true;
	}

	internal bool IsSafehouseOrControlledNotBy(PlayerID pid)
	{
		if (IsSafehouseOrControlledByAny())
		{
			return !IsSafehouseOrControlledBy(pid);
		}
		return false;
	}

	internal void TogglePickSuppression(bool value)
	{
		Data.suppressPick = value;
	}

	public bool IsAnyCrewAtThisNode(PlayerID pid)
	{
		return Game.ctx.players.WithID(pid).crew.FindFirstCrewAtLocation(_entity.data.board.bead.nodeId).IsValid;
	}

	public bool CanBeScopedOutByPlayer(PlayerID pid)
	{
		if (pid.FindPlayer().territory.ScopeOutReserved(_entity.Id))
		{
			return false;
		}
		if (IsScopedBy(pid))
		{
			return false;
		}
		if (!IsInteresting && !IsSafehouseNotOf(pid) && !IsControlledNotBy(pid) && !IsPoliceStation)
		{
			return IsPoliticalWard;
		}
		return true;
	}

	public bool IsInteractableByPlayer(PlayerID pid)
	{
		bool flag = IsScopedBy(pid);
		if (flag && IsSafehouseNotOf(pid))
		{
			if (SafehouseUtils.CanRaidSafehouse(_entity, pid))
			{
				return true;
			}
			if (SafehouseUtils.WasSafehouseRaided(_entity))
			{
				return true;
			}
		}
		if (flag && IsPoliceStation)
		{
			return true;
		}
		if (flag && IsInteresting)
		{
			return true;
		}
		if (pid.FindPlayer().territory.IsControlled(_entity))
		{
			return true;
		}
		if (flag && GetControllingPlayer().IsAIPlayer && !IsSafehouse)
		{
			return true;
		}
		ResidenceComponent residence = _entity.components.residence;
		if (residence != null && residence.IsBuildingPickInteractable)
		{
			return true;
		}
		if (flag)
		{
			CivicComponent civic = _entity.components.civic;
			if (civic != null && civic.IsBuildingPickInteractable)
			{
				return true;
			}
		}
		return false;
	}

	public bool CanBeScopedByPlayerCrew(PlayerID pid)
	{
		if (IsAnyCrewAtThisNode(pid))
		{
			return CanBeScopedOutByPlayer(pid);
		}
		return false;
	}

	public bool CanBeRaidedByPlayerCrew(PlayerID pid)
	{
		if (IsAnyCrewAtThisNode(pid))
		{
			if (!SafehouseUtils.CanRaidSafehouse(_entity, pid))
			{
				return SafehouseUtils.WasSafehouseRaided(_entity);
			}
			return true;
		}
		return false;
	}

	public bool IsRaidTarget(PlayerID pid)
	{
		if (!SafehouseUtils.CanRaidSafehouse(_entity, pid))
		{
			return SafehouseUtils.WasSafehouseRaided(_entity);
		}
		return true;
	}

	public bool HasTag(Label tag)
	{
		return _entity.data.building?.tags?.Contains(tag) == true;
	}

	public void AddTag(Label tag)
	{
		BuildingData building = _entity.data.building;
		building.tags = building.tags ?? new TagList();
		if (!building.tags.Contains(tag))
		{
			building.tags.Add(tag);
		}
	}

	public void RemoveTag(Label tag)
	{
		BuildingData building = _entity.data.building;
		if (building.tags != null && building.tags.Contains(tag))
		{
			building.tags.Remove(tag);
		}
		if (building.tags != null && building.tags.Count == 0)
		{
			building.tags = null;
		}
	}
}

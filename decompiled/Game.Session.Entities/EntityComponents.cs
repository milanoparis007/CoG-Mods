using System.Collections.Generic;
using Game.Core;
using SomaSim.Util;

namespace Game.Session.Entities;

public sealed class EntityComponents
{
	public IdentityComponent ident;

	public AgentComponent agent;

	public BizComponent biz;

	public BoardComponent board;

	public BuildingComponent building;

	public CivicComponent civic;

	public CornerComponent corner;

	public DefModuleComponent defmodule;

	public DeliveryComponent delivery;

	public LotComponent lot;

	public MobileComponent mobile;

	public ModelComponent model;

	public ModulesComponent modules;

	public PersonComponent person;

	public PoliceStationComponent police;

	public ResidenceComponent residence;

	public ScriptComponent script;

	public SelectionComponent selection;

	public WeaponComponent weapon;

	public List<BaseComponent> all;

	public List<IAnimatedComponent> updated = new List<IAnimatedComponent>();

	public List<IEntityEventObserverComponent> eventObservers = new List<IEntityEventObserverComponent>();

	public bool HasUpdatedComponents => updated.Count > 0;

	private IEnumerable<IUpgradableComponent> allUpgradables => all.WhereTypeIs<IUpgradableComponent>();

	public void AddComponents(Entity entity)
	{
		List<BaseConfig> list = entity.config.all;
		all = new List<BaseComponent>(list.Count);
		foreach (BaseConfig item3 in list)
		{
			BaseComponent baseComponent = item3.CreateComponent(this);
			all.Add(baseComponent);
			if (baseComponent is IAnimatedComponent item)
			{
				updated.Add(item);
			}
			if (baseComponent is IEntityEventObserverComponent item2)
			{
				eventObservers.Add(item2);
			}
			baseComponent.OnAfterComponentCreated(entity, item3);
		}
	}

	public void RemoveComponents()
	{
		foreach (BaseComponent item in all)
		{
			item.OnBeforeComponentRemoved();
		}
		eventObservers.Clear();
		updated.Clear();
		all.Clear();
		TypeUtils.RemoveMemberInstances<BaseComponent>(this);
	}

	public void SendEvent(EntityEventType eet)
	{
		foreach (IEntityEventObserverComponent eventObserver in eventObservers)
		{
			eventObserver.OnEntityEvent(eet);
		}
	}

	public void GenerateUpgradeData(EntityID entityId, UpgradeData result)
	{
		result.Reset();
		result.source = entityId;
		foreach (IUpgradableComponent allUpgradable in allUpgradables)
		{
			result.databag.Add(allUpgradable.GetType(), allUpgradable.GenerateUpgradeData());
		}
	}

	public void ConsumeUpgradeData(UpgradeData data)
	{
		EntityID source = data.source;
		foreach (IUpgradableComponent allUpgradable in allUpgradables)
		{
			object obj = data.databag.FindOrNull(allUpgradable.GetType());
			if (obj != null)
			{
				allUpgradable.ConsumeUpgradeData(source, obj);
			}
		}
		foreach (IUpgradableComponent allUpgradable2 in allUpgradables)
		{
			allUpgradable2.OnAfterUpgrade(source);
		}
		data.Reset();
	}
}

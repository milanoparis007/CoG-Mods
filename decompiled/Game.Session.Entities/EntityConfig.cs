using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Entities;

public sealed class EntityConfig
{
	public IdentityConfig ident;

	public AgentConfig agent;

	public BizConfig biz;

	public BoardConfig board;

	public BuildingConfig building;

	public CivicConfig civic;

	public CornerConfig corner;

	public DefModuleConfig defmodule;

	public DeliveryConfig delivery;

	public LotConfig lot;

	public MobileConfig mobile;

	public ModelConfig model;

	public ModulesConfig modules;

	public PersonConfig person;

	public PoliceStationConfig police;

	public ResidenceConfig residence;

	public ScriptConfig script;

	public SelectionConfig selection;

	public WeaponConfig weapon;

	public List<BaseConfig> all;

	public Label Template => ident.template;

	public T GetConfig<T>() where T : BaseConfig
	{
		return GetConfig(typeof(T)) as T;
	}

	public BaseConfig GetConfig(Type t)
	{
		foreach (BaseConfig item in all)
		{
			if (item.GetType() == t)
			{
				return item;
			}
		}
		return null;
	}

	public override string ToString()
	{
		return $"[config {Template}]";
	}

	internal void VerityConfigDependencies()
	{
		foreach (BaseConfig item in all)
		{
			List<Type> requiresConfigs = item.RequiresConfigs;
			if (requiresConfigs == null || requiresConfigs.Count == 0)
			{
				continue;
			}
			foreach (Type item2 in requiresConfigs)
			{
				GetConfig(item2);
			}
		}
	}
}

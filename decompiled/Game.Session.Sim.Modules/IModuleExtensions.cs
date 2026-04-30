using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;

namespace Game.Session.Sim.Modules;

public static class IModuleExtensions
{
	private static EntityConfig FindModuleEntityConfig(IModule module)
	{
		return Game.ctx.entityman.FindTemplate(module.ModuleConfig.Id);
	}

	public static ModuleExpansions FindExpansionDefs(this IModule module)
	{
		return FindModuleEntityConfig(module)?.defmodule?.expansions;
	}

	public static List<ModuleLevelupConfig> FindLevelupDefs(this IModule module)
	{
		return FindModuleEntityConfig(module)?.defmodule?.levelups;
	}

	public static List<ModuleExpansionConfig> FindExpansionsInstalled(this IModule module, VisitState _)
	{
		ModuleExpansions moduleExpansions = module.FindExpansionDefs();
		if (moduleExpansions == null)
		{
			return new List<ModuleExpansionConfig>();
		}
		List<Label> installedIds = module.ModuleData.Expansions ?? new List<Label>();
		return moduleExpansions.configs.Where((ModuleExpansionConfig def) => installedIds.Contains(def.id)).ToList();
	}

	public static List<ModuleExpansionConfig> FindExpansionsToOffer(this IModule module, VisitState visit)
	{
		ModuleExpansions moduleExpansions = module.FindExpansionDefs();
		if (moduleExpansions == null)
		{
			return new List<ModuleExpansionConfig>();
		}
		List<Label> installedIds = module.ModuleData.Expansions ?? new List<Label>();
		return moduleExpansions.configs.Where((ModuleExpansionConfig def) => !installedIds.Contains(def.id) && def.visreqs.AllPass(visit)).ToList();
	}

	public static (int installed, int max) GetExpansionCounts(this IModule module, VisitState visit)
	{
		int item = module.FindExpansionDefs()?.FindMaxExpansions(visit) ?? 0;
		return (installed: module.ModuleData.Expansions?.Count ?? 0, max: item);
	}

	public static bool ContainsExpansion(this IModule module, Label id)
	{
		return module.ModuleData.Expansions?.Contains(id) ?? false;
	}

	public static void AddExpansion(this IModule module, Label id)
	{
		IModuleData moduleData = module.ModuleData;
		moduleData.Expansions = moduleData.Expansions ?? new List<Label>();
		moduleData.Expansions.Add(id);
	}

	public static bool RemoveExpansion(this IModule module, Label id)
	{
		IModuleData moduleData = module.ModuleData;
		bool result = moduleData.Expansions?.Remove(id) ?? false;
		List<Label> expansions = moduleData.Expansions;
		if (expansions != null && expansions.Count == 0)
		{
			moduleData.Expansions = null;
		}
		return result;
	}
}

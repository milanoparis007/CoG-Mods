using System.Collections.Generic;
using System.Linq;
using Game.Core;
using SomaSim.Util;

namespace Game.Services;

public sealed class ResourceSettings : IValidatingSettings, ISettingsLoadObserver
{
	public LabelDictionary<UnitDef> units;

	public LabelDictionary<Resource> definitions;

	public LabelDictionary<ResourceCategory> categories;

	public LabelDictionary<EthnicAlcoholReplacements> ethPackAlcohols;

	public List<Label> moduleGroupResources;

	public bool IsValidResource(Label resid)
	{
		return definitions.ContainsKey(resid);
	}

	public Resource FindResource(Label resid)
	{
		return definitions.FindOrNull(resid);
	}

	public UnitDef FindUnit(Label id)
	{
		return units.FindOrNull(id);
	}

	public void OnAfterSettingsLoaded()
	{
		foreach (KeyValuePair<Label, UnitDef> unit in units)
		{
			unit.Value.unitid = unit.Key;
		}
		foreach (KeyValuePair<Label, Resource> definition in definitions)
		{
			definition.Value.resid = definition.Key;
			definition.Value.unitdef = units.FindOrNull(definition.Value.unitid);
		}
		moduleGroupResources = (from e in definitions
			where e.Value.IsModuleGroup
			select e.Key).ToList();
	}

	public bool IsResourceGroup(Label resid)
	{
		return moduleGroupResources.Contains(resid);
	}

	public bool IsResourceBasic(Label resid)
	{
		return !moduleGroupResources.Contains(resid);
	}

	public void Validate()
	{
		foreach (Label key in definitions.Keys)
		{
			ValidateResourceAfterLoading(key);
		}
	}

	private void ValidateResourceAfterLoading(Label resId)
	{
		Resource resource = FindResource(resId);
		if (resource.IsBasic)
		{
			return;
		}
		foreach (Label groupmember in resource.groupmembers)
		{
			ValidateResourceAfterLoading(groupmember);
		}
	}
}

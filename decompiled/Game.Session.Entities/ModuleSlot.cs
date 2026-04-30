using Game.Core;
using Game.Session.Sim.Modules;

namespace Game.Session.Entities;

public sealed class ModuleSlot
{
	public TagList tags;

	public bool hidden;

	internal Label GetFirstTag()
	{
		if (tags == null || tags.Count <= 0)
		{
			return Label.NULL;
		}
		return tags[0];
	}

	public bool CanSlotHouseThisModule(IModuleConfig moduleConfig)
	{
		TagList tagList = moduleConfig?.Common?.tags;
		if (tagList != null)
		{
			return tags.ContainsAtLeastOneOf(tagList);
		}
		return false;
	}
}

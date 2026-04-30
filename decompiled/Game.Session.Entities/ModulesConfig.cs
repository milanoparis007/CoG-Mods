using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Entities;

public class ModulesConfig : BaseConfig
{
	public List<Label> preinstalled;

	public List<ModuleSlot> slots;

	public override List<Type> RequiresConfigs => null;

	public override BaseComponent CreateComponent(EntityComponents ec)
	{
		return ec.modules = new ModulesComponent();
	}

	public override BaseData MoveOrCreateData(EntityData target, EntityData source)
	{
		ModulesData obj = source?.modules ?? new ModulesData();
		ModulesData result = obj;
		target.modules = obj;
		return result;
	}
}

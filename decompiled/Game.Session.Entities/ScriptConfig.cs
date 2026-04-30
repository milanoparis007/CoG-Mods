using System;
using System.Collections.Generic;

namespace Game.Session.Entities;

public sealed class ScriptConfig : BaseConfig
{
	public override List<Type> RequiresConfigs => null;

	public override BaseComponent CreateComponent(EntityComponents ec)
	{
		return ec.script = new ScriptComponent();
	}

	public override BaseData MoveOrCreateData(EntityData target, EntityData source)
	{
		ScriptData obj = source?.script ?? new ScriptData();
		ScriptData result = obj;
		target.script = obj;
		return result;
	}
}

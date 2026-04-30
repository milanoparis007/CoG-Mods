using System;
using System.Collections.Generic;

namespace Game.Session.Entities;

public sealed class PersonConfig : BaseConfig
{
	public override List<Type> RequiresConfigs => null;

	public override BaseComponent CreateComponent(EntityComponents ec)
	{
		return ec.person = new PersonComponent();
	}

	public override BaseData MoveOrCreateData(EntityData target, EntityData source)
	{
		PersonData obj = source?.person ?? new PersonData();
		PersonData result = obj;
		target.person = obj;
		return result;
	}
}

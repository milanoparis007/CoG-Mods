using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Entities;

public sealed class BuildingConfig : BaseConfig
{
	public ZoneType type;

	public int aoepop;

	public override List<Type> RequiresConfigs => null;

	public override BaseComponent CreateComponent(EntityComponents ec)
	{
		return ec.building = new BuildingComponent();
	}

	public override BaseData MoveOrCreateData(EntityData target, EntityData source)
	{
		BuildingData obj = source?.building ?? new BuildingData();
		BuildingData result = obj;
		target.building = obj;
		return result;
	}
}

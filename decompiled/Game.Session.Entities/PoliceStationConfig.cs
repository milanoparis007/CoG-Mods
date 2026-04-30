using System;
using System.Collections.Generic;

namespace Game.Session.Entities;

public class PoliceStationConfig : BaseConfig
{
	public override List<Type> RequiresConfigs => null;

	public override BaseComponent CreateComponent(EntityComponents ec)
	{
		return ec.police = new PoliceStationComponent();
	}

	public override BaseData MoveOrCreateData(EntityData target, EntityData source)
	{
		PoliceStationData obj = source?.police ?? new PoliceStationData();
		PoliceStationData result = obj;
		target.police = obj;
		return result;
	}
}

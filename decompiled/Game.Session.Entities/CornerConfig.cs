using System;
using System.Collections.Generic;

namespace Game.Session.Entities;

public sealed class CornerConfig : BaseConfig
{
	public override List<Type> RequiresConfigs => null;

	public override BaseComponent CreateComponent(EntityComponents ec)
	{
		return ec.corner = new CornerComponent();
	}

	public override BaseData MoveOrCreateData(EntityData target, EntityData source)
	{
		CornerData obj = source?.corner ?? new CornerData();
		CornerData result = obj;
		target.corner = obj;
		return result;
	}
}

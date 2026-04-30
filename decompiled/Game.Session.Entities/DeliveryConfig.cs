using System;
using System.Collections.Generic;

namespace Game.Session.Entities;

public sealed class DeliveryConfig : BaseConfig
{
	public override List<Type> RequiresConfigs => new List<Type> { typeof(ModulesConfig) };

	public override BaseComponent CreateComponent(EntityComponents ec)
	{
		return ec.delivery = new DeliveryComponent();
	}

	public override BaseData MoveOrCreateData(EntityData target, EntityData source)
	{
		DeliveryData obj = source?.delivery ?? new DeliveryData();
		DeliveryData result = obj;
		target.delivery = obj;
		return result;
	}
}

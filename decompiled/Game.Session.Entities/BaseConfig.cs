using System;
using System.Collections.Generic;

namespace Game.Session.Entities;

public abstract class BaseConfig
{
	public abstract List<Type> RequiresConfigs { get; }

	public abstract BaseComponent CreateComponent(EntityComponents ec);

	public abstract BaseData MoveOrCreateData(EntityData target, EntityData source);

	public virtual void VerifyAfterLoading(EntityConfig config)
	{
	}
}

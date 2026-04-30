using System;
using System.Collections.Generic;
using Game.Session.Data;

namespace Game.Session.Entities;

public sealed class AgentConfig : BaseConfig
{
	public ModValue movesPerTurn;

	public ModValue actionsPerTurn;

	public override List<Type> RequiresConfigs => null;

	public override BaseComponent CreateComponent(EntityComponents ec)
	{
		return ec.agent = new AgentComponent();
	}

	public override BaseData MoveOrCreateData(EntityData target, EntityData source)
	{
		AgentData obj = source?.agent ?? new AgentData();
		AgentData result = obj;
		target.agent = obj;
		return result;
	}
}

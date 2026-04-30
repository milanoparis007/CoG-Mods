using System;
using System.Collections.Generic;
using Game.Services;

namespace Game.Session.Entities;

public sealed class ResidenceConfig : BaseConfig
{
	public int apartments;

	public string locname;

	public string locdesc;

	public override List<Type> RequiresConfigs => new List<Type> { typeof(BuildingConfig) };

	public override BaseComponent CreateComponent(EntityComponents ec)
	{
		return ec.residence = new ResidenceComponent();
	}

	public override BaseData MoveOrCreateData(EntityData target, EntityData source)
	{
		ResidenceData obj = source?.residence ?? new ResidenceData();
		ResidenceData result = obj;
		target.residence = obj;
		return result;
	}

	public string GetName()
	{
		return Loc.Get(locname);
	}

	public string GetDesc()
	{
		return Loc.Get(locdesc);
	}
}

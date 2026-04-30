using System;
using System.Collections.Generic;
using Game.Services;

namespace Game.Session.Entities;

public sealed class CivicConfig : BaseConfig
{
	public enum CivicAssignment
	{
		None,
		PoliticalWard
	}

	public string locname;

	public string locdesc;

	public CivicAssignment assignment;

	public override List<Type> RequiresConfigs => new List<Type> { typeof(BuildingConfig) };

	public bool IsAssigned => assignment != CivicAssignment.None;

	public bool IsNotAssigned => assignment == CivicAssignment.None;

	public bool ShouldHaveNpcResident => assignment == CivicAssignment.PoliticalWard;

	public bool IsWard => assignment == CivicAssignment.PoliticalWard;

	public override BaseComponent CreateComponent(EntityComponents ec)
	{
		return ec.civic = new CivicComponent();
	}

	public override BaseData MoveOrCreateData(EntityData target, EntityData source)
	{
		CivicData obj = source?.civic ?? new CivicData();
		CivicData result = obj;
		target.civic = obj;
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

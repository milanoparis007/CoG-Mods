using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Session.Data;
using Game.Session.Sim;

namespace Game.Session.Entities;

public sealed class PersonComponent : BaseComponent
{
	public PersonConfig Config => _baseConfig as PersonConfig;

	public bool IsResAssigned => _entity.data.person.resassigned.IsValid;

	public IEnumerable<Trait> GetAllTraits()
	{
		TraitList traits = Game.serv.globals.settings.people.traits;
		return _entity.data.person.traitIds.Select((Label id) => traits.Find(id));
	}

	internal FamilyTree FindFamilyTree()
	{
		return Game.ctx.simman.peoplegen.data.FindFamilyTree(_entity.data.person.famId);
	}

	public void SetNickname(string nickname)
	{
		_entity.data.person.SetNickname(nickname);
	}

	public void SetResAssignment(Entity building)
	{
		_entity.data.person.resassigned = building.Id;
	}

	public void ClearResAssignment()
	{
		_entity.data.person.resassigned = EntityID.INVALID;
	}

	public static Entity FindResidenceAssignment(Entity peep)
	{
		return peep?.data.person?.resassigned.FindEntity();
	}

	public bool IsOldEnoughToOwnBiz(SimTime now)
	{
		return _entity.data.person.GetAge(now).YearsFloat >= 18f;
	}
}

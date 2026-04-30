using System.Collections.Generic;
using System.Linq;
using SomaSim.Util;

namespace Game.Services;

public struct LocReplacementContext
{
	public struct StringStringEntry
	{
		public string key;

		public string value;
	}

	public IRandom rng;

	public object[] replacements;

	public LocPersonReplacements? person;

	public string forcedlang;

	public LocReplacementContext(IRandom rng = null, object[] replacements = null, LocPersonReplacements? person = null, string forcedlang = null)
	{
		this.rng = rng;
		this.replacements = replacements;
		this.person = person;
		this.forcedlang = forcedlang;
	}

	public LocReplacementContext SetPerson(LocPersonReplacements newPerson)
	{
		return new LocReplacementContext(rng, replacements, newPerson);
	}

	public LocReplacementContext AddReplacements(string key, string val)
	{
		List<object> obj = ((replacements != null) ? replacements.ToList() : new List<object>());
		obj.Add(key);
		obj.Add(val);
		object[] array = obj.ToArray();
		return new LocReplacementContext(rng, array, person);
	}
}

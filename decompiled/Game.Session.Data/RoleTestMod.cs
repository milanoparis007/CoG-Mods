using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Data;

public abstract class RoleTestMod : BaseDeltaMultiplierModifier
{
	public enum Test
	{
		Any,
		None
	}

	public List<Label> of;

	public Test @is;

	protected abstract string LocKey { get; }

	public override bool DoesPass(ModQuery query)
	{
		Entity entity = FindAgentToTest(query);
		if (entity == null)
		{
			return false;
		}
		RoleDef roleDef = entity.data.agent.xp?.GetCrewRole();
		if (roleDef == null)
		{
			return false;
		}
		bool flag = of.Contains(roleDef.id);
		if (@is != Test.Any)
		{
			return !flag;
		}
		return flag;
	}

	protected abstract Entity FindAgentToTest(ModQuery query);

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get(LocKey, "delta", AbstractModifier.FormatDelta(delta), "role", Loc.Get(FindAgentToTest(query).data.agent.xp?.GetCrewRole().loctitle));
	}
}

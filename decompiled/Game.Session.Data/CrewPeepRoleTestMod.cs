using Game.Session.Entities;

namespace Game.Session.Data;

public sealed class CrewPeepRoleTestMod : RoleTestMod
{
	public override ModQueryElement QueryMustProvide => ModQueryElement.Nothing;

	protected override string LocKey => "mod.crew-role";

	protected override Entity FindAgentToTest(ModQuery query)
	{
		return query.FindCrewPeep();
	}
}

using Game.Session.Entities;

namespace Game.Session.Data;

public sealed class CrewPeepLevelTestMod : LevelTestMod
{
	public override ModQueryElement QueryMustProvide => ModQueryElement.Nothing;

	protected override string LocKey => "mod.crew-experience";

	protected override Entity FindAgentToTest(ModQuery query)
	{
		return query.FindCrewPeep();
	}
}

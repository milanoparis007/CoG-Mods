using Game.Services;

namespace Game.Session.Data;

public class CheckImmigrant : AbstractVisitRequirement
{
	public enum Target
	{
		Player,
		Owner
	}

	public Target @is;

	public bool expected;

	private EthnicityDef GetEthDef(VisitState visit)
	{
		return Game.serv.globals.settings.ethnicities.FindEthnicityDef((@is == Target.Player) ? GetPlayer(visit).social.PlayerEthnicity : visit.npc.data.person.eth);
	}

	public override bool DoesPass(VisitState visit)
	{
		return GetEthDef(visit).immigrant == expected;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		string message = (expected ? Loc.Get("ui.requirements.immigrant.expected") : Loc.Get("ui.requirements.immigrant.unexpected"));
		return new ReqExplanation(DoesPass(visit), message);
	}
}

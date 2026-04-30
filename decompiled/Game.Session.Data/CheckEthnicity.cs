using System.Collections.Generic;
using Game.Core;
using Game.Services;

namespace Game.Session.Data;

public class CheckEthnicity : AbstractVisitRequirement
{
	public enum Target
	{
		Player,
		Owner
	}

	public Target @is;

	public List<Label> oneof;

	private Label GetEth(VisitState visit)
	{
		if (@is != Target.Player)
		{
			return visit.npc.data.person.eth;
		}
		return GetPlayer(visit).social.PlayerEthnicity;
	}

	public override bool DoesPass(VisitState visit)
	{
		return oneof?.Contains(GetEth(visit)) ?? false;
	}

	public override ReqExplanation Explain(VisitState visit)
	{
		return new ReqExplanation(DoesPass(visit), Loc.Get("ui.requirements.ethnicity"));
	}
}

using Game.Core;
using Game.Services;
using Game.Session.Player;
using Game.Session.Sim;

namespace Game.Session.Data;

public class CheckPlayerUnlockedResources : CheckListOfItems
{
	protected override int Count(VisitState visit)
	{
		int num = 0;
		PlayerSkills skills = GetPlayer(visit).skills;
		foreach (Label item in of)
		{
			if (skills.HasResourceUnlocked(item))
			{
				num++;
			}
		}
		return num;
	}

	protected override void VerifyData(VisitState visit)
	{
		SimulationManager simman = Game.ctx.simman;
		foreach (Label item in of)
		{
			simman.FindResource(item);
		}
	}

	protected override string MakeExplanationText()
	{
		return MakeExplanationText(Loc.Get("ui.requirements.resource.expected"), Loc.Get("ui.requirements.resource.unexpected"), Loc.Get("ui.requirements.resource.all"), Loc.Get("ui.requirements.resource.any"), Loc.Get("ui.requirements.resource.none"));
	}

	protected override string IdToName(Label id)
	{
		return Game.ctx.simman.FindResource(id).GetName();
	}
}

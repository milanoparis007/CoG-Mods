using Game.Core;
using Game.Services;
using Game.Session.Player;

namespace Game.Session.Data;

public class CheckPlayerSkills : CheckListOfItems
{
	public enum LockStatus
	{
		Unlocked,
		Locked
	}

	public LockStatus status;

	protected override int Count(VisitState visit)
	{
		int num = 0;
		PlayerSkills skills = GetPlayer(visit).skills;
		bool flag = status == LockStatus.Unlocked;
		foreach (Label item in of)
		{
			if (skills.HasSkill(item) == flag)
			{
				num++;
			}
		}
		return num;
	}

	protected override void VerifyData(VisitState visit)
	{
		PlayerSkills skills = GetPlayer(visit).skills;
		foreach (Label item in of)
		{
			skills.FindSkillDef(item);
		}
	}

	protected override string MakeExplanationText()
	{
		return MakeExplanationText(Loc.Get("ui.requirements.player.skills.expected"), Loc.Get("ui.requirements.player.skills.unexpected"), Loc.Get("ui.requirements.player.skills.all"), Loc.Get("ui.requirements.player.skills.any"), Loc.Get("ui.requirements.player.skills.none"));
	}

	protected override string IdToName(Label id)
	{
		return Game.serv.globals.settings.skills.GetSkill(id).GetName();
	}
}

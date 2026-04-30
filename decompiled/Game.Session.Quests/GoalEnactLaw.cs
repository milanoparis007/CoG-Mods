using Game.Core;
using Game.Services;

namespace Game.Session.Quests;

public sealed class GoalEnactLaw : BaseGoal
{
	public Label lawID;

	public bool enacted;

	public override Type GoalType => Type.HaveSomething;

	public override bool IsRefundable => false;

	public override string LocDescKey => "goal-law";

	public override void OnAdded()
	{
		RegisterProgress(GetIsLawCompliant(), startup: true);
		Game.ctx.events.AddListener(SessionEventType.LawChanged, OnLawChanged);
	}

	public override void OnRemoved()
	{
		Game.ctx.events.RemoveListener(SessionEventType.LawChanged, OnLawChanged);
	}

	public void OnLawChanged(SessionEvent _)
	{
		RegisterProgress(GetIsLawCompliant(), startup: false);
	}

	public int GetIsLawCompliant()
	{
		bool flag = Game.ctx.simman.politics.IsLawEnacted(lawID);
		if (!(enacted && flag) && (enacted || flag))
		{
			return 0;
		}
		return 1;
	}

	public override LocReplacementContext MakeStringReplacements()
	{
		string[] array = new string[4]
		{
			"lawName",
			Loc.Get(Game.serv.globals.settings.politics.FindLawDef(lawID).locname),
			"enactOrRevoke",
			enacted ? Loc.Get("goal-law.enact") : Loc.Get("goal-law.revoke")
		};
		object[] replacements = array;
		return new LocReplacementContext(null, replacements);
	}
}

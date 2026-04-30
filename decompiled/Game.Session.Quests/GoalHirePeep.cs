using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Quests;

public sealed class GoalHirePeep : BaseGoal
{
	public Label ethID;

	public override Type GoalType => Type.HaveSomething;

	public override bool IsRefundable => false;

	public override string LocDescKey => "goal-hire-peeps";

	public override void OnAdded()
	{
		RegisterProgress(GetNumEligibleCrew(), startup: true);
		Game.ctx.events.AddListener(SessionEventType.CrewMemberAdded, OnCrewAdded);
	}

	public override void OnRemoved()
	{
		Game.ctx.events.RemoveListener(SessionEventType.CrewMemberAdded, OnCrewAdded);
	}

	public void OnCrewAdded(SessionEvent _)
	{
		RegisterProgress(GetNumEligibleCrew(), startup: false);
	}

	public int GetNumEligibleCrew()
	{
		return Game.ctx.players.Human.crew.AllCrew.Where((CrewAssignment x) => x.GetPeep().data.person.eth == ethID).Count();
	}

	public override LocReplacementContext MakeStringReplacements()
	{
		string itemName = GetItemName();
		string text = Loc.Percentage(ProduceGoalCompletionFraction());
		string text2 = Loc.FormatNumber(state.current);
		string text3 = Loc.FormatNumber(goal);
		string text4 = Loc.FormatNumber(MathUtil.ClampMin(goal - state.current, 0));
		string ethnicity = Game.serv.globals.settings.ethnicities.FindEthnicityDef(ethID).loc.GetEthnicity();
		string[] array = new string[12]
		{
			"value", text2, "goal", text3, "remains", text4, "percent", text, "item", itemName,
			"eth", ethnicity
		};
		object[] replacements = array;
		return new LocReplacementContext(null, replacements);
	}
}

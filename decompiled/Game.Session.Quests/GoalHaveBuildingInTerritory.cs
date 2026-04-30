using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Quests;

public sealed class GoalHaveBuildingInTerritory : BaseGoal
{
	public Label bizConfig;

	public override Type GoalType => Type.HaveSomething;

	public override bool IsRefundable => false;

	public override string LocDescKey => "goal-have-building-in-territory";

	public override void OnAdded()
	{
		RegisterProgress(GetBuildingInTerritory(), startup: true);
		Game.ctx.events.AddListener(SessionEventType.PlayerTerritoryChanged, OnTerritoryChanged);
	}

	public override void OnRemoved()
	{
		Game.ctx.events.RemoveListener(SessionEventType.PlayerTerritoryChanged, OnTerritoryChanged);
	}

	public void OnTerritoryChanged(SessionEvent _)
	{
		RegisterProgress(GetBuildingInTerritory(), startup: false);
	}

	public int GetBuildingInTerritory()
	{
		return Game.ctx.players.Human.territory.CountModuleAndChildrenInTerritory(bizConfig, mustBeTiedHouse: false, mustHaveTradeHistory: false, mustBeInTerritory: true);
	}

	public override LocReplacementContext MakeStringReplacements()
	{
		string itemName = GetItemName();
		string text = Loc.Percentage(ProduceGoalCompletionFraction());
		string text2 = Loc.FormatNumber(state.current);
		string text3 = Loc.FormatNumber(goal);
		string text4 = Loc.FormatNumber(MathUtil.ClampMin(goal - state.current, 0));
		Label label = bizConfig;
		string text5 = Loc.Get("building." + label.ToString() + ".name");
		string[] array = new string[12]
		{
			"value", text2, "goal", text3, "remains", text4, "percent", text, "item", itemName,
			"building", text5
		};
		object[] replacements = array;
		return new LocReplacementContext(null, replacements);
	}
}

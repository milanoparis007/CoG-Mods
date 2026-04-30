using Game.Services;

namespace Game.Session.Sim;

public abstract class VictorySubgoal
{
	public struct StateFormatted
	{
		public string current;

		public string goal;

		public StateFormatted(string current, string goal)
		{
			this.current = current;
			this.goal = goal;
		}
	}

	public VictorySubgoalState state;

	public readonly string locdesc;

	protected VictorySettings Settings => Game.serv.globals.settings.general.victory;

	public VictorySubgoal(string locdesc)
	{
		this.locdesc = locdesc;
	}

	public (string header, string desc) ExplainSubgoal()
	{
		string text = Loc.Get(locdesc).Trim();
		string item = Loc.Get(state.GetKey(), "line", text).TrimEnd();
		string item2 = ExplainState();
		return (header: item, desc: item2);
	}

	public abstract void RecomputeState();

	public abstract string ExplainState();

	protected string ExplainStateAsRanking()
	{
		string text = Loc.FormatNumber(state.current);
		string text2 = Loc.FormatNumber(state.goal);
		return Loc.Get("victory.explain-ranking", "value", text, "goal", text2);
	}

	protected string ExplainStateAsGoalNumbers()
	{
		string text = Loc.FormatNumber(state.current);
		string text2 = Loc.FormatNumber(state.goal);
		return Loc.Get("victory.explain-current", "value", text, "goal", text2);
	}

	protected string ExplainStateAsGoalPercent()
	{
		string text = Loc.Percentage(state.current);
		string text2 = Loc.Percentage(state.goal);
		return Loc.Get("victory.explain-current", "value", text, "goal", text2);
	}
}

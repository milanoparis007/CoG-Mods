using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Player;

namespace Game.UI.Session.Convo;

public sealed class ConvoDataCopsFedHint : ConvoData
{
	public PlayerID precinct;

	public PlayerID rival;

	public SimTime nextAskDate;

	public bool CanAsk => nextAskDate <= Game.ctx.clock.Now;

	public ConvoDataCopsFedHint()
	{
	}

	public ConvoDataCopsFedHint(PlayerID precinct, PlayerID rival, SimTime nextAskDate)
	{
		this.precinct = precinct;
		this.rival = rival;
		this.nextAskDate = nextAskDate;
	}

	public override string[] MakeReplacements(VisitState visit, int index)
	{
		return new string[4]
		{
			"groupname",
			rival.FindPlayer().social.FindPlayerGroupNameColorized(),
			"nextdate",
			Loc.FormatDateLong(nextAskDate)
		};
	}
}

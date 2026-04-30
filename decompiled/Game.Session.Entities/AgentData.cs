using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Game.Core;
using Game.Session.Player.Commands;
using SomaSim.Util;

namespace Game.Session.Entities;

public sealed class AgentData : BaseData
{
	public enum NickStatus
	{
		None,
		Eligible,
		Selected
	}

	public PlayerID pid;

	public EntityID introducer;

	public NodeID nid;

	public int actionsLeft;

	public int movesLeft;

	public Fixnum health = 100;

	public List<ActionHistoryElement> actionHistory;

	public XP xp;

	public SimTime lastArrestTime = SimTime.MAX_DATE;

	public SimTime lastInjuryTime = SimTime.MAX_DATE;

	public Fixnum expectedSalary = Fixnum.ZERO;

	public Fixnum lastPaidSalary = Fixnum.ZERO;

	public NickStatus nickname;

	public Dictionary<CrewStats, int> crewHistoryStats = Enum.GetValues(typeof(CrewStats)).Cast<CrewStats>().ToDictionary((CrewStats key) => key, (CrewStats key) => 0);

	public bool IsInHumanCrew => pid.IsHumanPlayer;

	public bool IsNotInHumanCrew => !pid.IsHumanPlayer;

	[Conditional("UNITY_EDITOR")]
	public void AddActionHistory(Command cmd)
	{
		int currentTurn = Game.ctx.clock.CurrentTurn;
		actionHistory = actionHistory ?? new List<ActionHistoryElement>();
		actionHistory.Add(new ActionHistoryElement(cmd, currentTurn));
		if (actionHistory.Count > 200)
		{
			actionHistory.RemoveAt(0);
		}
	}
}

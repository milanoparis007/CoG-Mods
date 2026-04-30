using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Entities;

public sealed class ResEventData
{
	public enum Attendance
	{
		None,
		Registered,
		Done
	}

	public Label eventId;

	public int turnStarts;

	public int turnEnds;

	public bool isSkipped;

	public Attendance attendance;

	public ResEventResultData chosenResult;

	private static int CurrentTurn => Game.ctx.clock.CurrentTurn;

	public bool HasChosenResult => chosenResult != null;

	public bool IsExpired => turnEnds < CurrentTurn;

	public bool IsThisTurn
	{
		get
		{
			if (!isSkipped)
			{
				return turnEnds == CurrentTurn;
			}
			return false;
		}
	}

	public bool IsComingSoon
	{
		get
		{
			if (!isSkipped)
			{
				return turnEnds > CurrentTurn;
			}
			return false;
		}
	}

	public bool IsSkipped => isSkipped;

	public bool IsAttendanceAvailable => attendance == Attendance.None;

	public bool IsAttendanceRegistered => attendance == Attendance.Registered;

	public bool IsAttendanceDone => attendance == Attendance.Done;

	public ResEventData()
	{
	}

	public ResEventData(Label eventId)
	{
		this.eventId = eventId;
		Reset();
	}

	public void Reset()
	{
		turnStarts = -1;
		turnEnds = -1;
		isSkipped = false;
		attendance = Attendance.None;
		chosenResult = null;
	}

	public void Start(int waitTurns, bool skipped)
	{
		turnStarts = CurrentTurn;
		turnEnds = CurrentTurn + waitTurns;
		isSkipped = skipped;
		attendance = Attendance.None;
		chosenResult = null;
	}

	public void SetAttendanceRegistered()
	{
		attendance = Attendance.Registered;
	}

	public void SetAttendanceDone(ResEventResultData result)
	{
		attendance = Attendance.Done;
		chosenResult = result;
	}

	public void ConsumeResult()
	{
		chosenResult = null;
	}

	public SimTime GetFinishDate()
	{
		return Game.ctx.clock.TurnsToSimTime(turnEnds - 1);
	}

	public Price GetCost(PlayerID pid)
	{
		return GetConfig().buyincash.Evaluate(pid);
	}

	public ResidentialEventConfig GetConfig()
	{
		return Game.serv.globals.settings.people.residentialEvents.FindEvent(eventId);
	}

	internal Price FindPrice(PlayerID pid)
	{
		return new Price(GetConfig()?.buyincash.Evaluate(pid) ?? ((Fixnum)0));
	}
}

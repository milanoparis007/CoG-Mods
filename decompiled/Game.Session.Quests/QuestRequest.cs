using System.Diagnostics;
using Game.Core;
using Game.Session.Data;

namespace Game.Session.Quests;

[DebuggerDisplay("{DebugString}")]
public class QuestRequest
{
	public enum Status
	{
		NotOffered,
		Available,
		Accepted,
		Declined
	}

	public EntityID owner;

	public string questid;

	public QuestUUID uuid;

	public Status status;

	public bool ShouldResetBecauseDeclined
	{
		get
		{
			if (status != Status.Declined)
			{
				return status == Status.NotOffered;
			}
			return true;
		}
	}

	public bool ShouldResetBecauseAvailable => status == Status.Available;

	public bool ShouldShowRequest => status == Status.Available;

	private string DebugString => $"QuestRequest from {owner} for {questid} status={status}";

	public QuestRequest()
	{
	}

	public QuestRequest(EntityID owner, string questid, bool available)
	{
		this.owner = owner;
		this.questid = questid;
		uuid = QuestUUID.EMPTY;
		status = (available ? Status.Available : Status.NotOffered);
	}

	public void SetAccepted(QuestUUID uuid)
	{
		status = Status.Accepted;
		this.uuid = uuid;
	}

	public void SetDeclined()
	{
		status = Status.Declined;
	}
}

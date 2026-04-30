using Game.Core;

namespace Game.Session.Player;

public struct HistoryItem
{
	public Label chapterId;

	public bool success;

	public string chosenDec;

	public SimTime decTime;

	public HistoryItem(Label chapterId, string chosenDec, bool success, SimTime decTime)
	{
		this.chapterId = chapterId;
		this.success = success;
		this.chosenDec = chosenDec;
		this.decTime = decTime;
	}
}

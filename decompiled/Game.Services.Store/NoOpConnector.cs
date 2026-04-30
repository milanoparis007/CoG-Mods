namespace Game.Services.Store;

public class NoOpConnector : StoreConnector
{
	public override bool isActive => true;

	public override bool IsPackInstalled(PackID id)
	{
		return false;
	}

	public override object GetUserPlatformID()
	{
		return null;
	}

	public override void OpenStorePage()
	{
	}

	public override void OpenStorePageForDLC(PackID pack)
	{
	}

	public override void SetPresence(string gang, string date, string city)
	{
	}

	public override void ClearPresence()
	{
	}

	public override void DebugClearAllAchievements()
	{
	}

	public override void SetAchievement(IAchievementDef def)
	{
	}

	public override bool GetAchievement(IAchievementDef def)
	{
		return false;
	}
}

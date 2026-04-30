namespace Game.Services.Store;

public class DebugConnectorWrapper : StoreConnector
{
	private StoreConnector _next;

	public override bool isActive => _next.isActive;

	public DebugConnectorWrapper(StoreConnector next)
	{
		_next = next;
	}

	public override void Initialize()
	{
		_next.Initialize();
	}

	public override void Release()
	{
		_next.Release();
		_next = null;
	}

	public override bool IsPackInstalled(PackID id)
	{
		return _next.IsPackInstalled(id);
	}

	public override void OpenStorePage()
	{
		_next.OpenStorePage();
	}

	public override void OpenStorePageForDLC(PackID pack)
	{
		_next.OpenStorePageForDLC(pack);
	}

	public override object GetUserPlatformID()
	{
		return _next.GetUserPlatformID();
	}

	public override void SetPresence(string gang, string date, string city)
	{
		_next.SetPresence(gang, date, city);
	}

	public override void ClearPresence()
	{
		_next.ClearPresence();
	}

	public override void DebugClearAllAchievements()
	{
		_next.DebugClearAllAchievements();
	}

	public override void SetAchievement(IAchievementDef def)
	{
		_next.SetAchievement(def);
	}

	public override bool GetAchievement(IAchievementDef def)
	{
		return _next.GetAchievement(def);
	}
}

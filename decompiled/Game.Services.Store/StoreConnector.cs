namespace Game.Services.Store;

public abstract class StoreConnector
{
	public bool isInitialized { get; private set; }

	public abstract bool isActive { get; }

	public virtual void Initialize()
	{
		isInitialized = true;
	}

	public virtual void Release()
	{
		isInitialized = false;
	}

	public abstract object GetUserPlatformID();

	public abstract void OpenStorePage();

	public abstract void OpenStorePageForDLC(PackID pack);

	public abstract void SetPresence(string gang, string date, string city);

	public abstract void ClearPresence();

	public abstract bool IsPackInstalled(PackID id);

	public abstract bool GetAchievement(IAchievementDef def);

	public abstract void SetAchievement(IAchievementDef def);

	public abstract void DebugClearAllAchievements();
}

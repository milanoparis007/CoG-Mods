namespace Game.Session;

public static class ISessionManagerExtensions
{
	public static T SubInit<M, T>(this M manager, T submanager) where M : ISessionManager where T : ISubManager<M>
	{
		submanager.Initialize(manager);
		return submanager;
	}

	public static T SubRelease<M, T>(M manager, T submanager) where M : ISessionManager where T : ISubManager<M>
	{
		submanager.Release();
		return submanager;
	}
}

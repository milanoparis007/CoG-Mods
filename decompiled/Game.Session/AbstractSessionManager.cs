using System.Linq;
using SomaSim.Util;

namespace Game.Session;

public abstract class AbstractSessionManager : ISessionManager
{
	public virtual bool IsInitializingDone => true;

	public virtual void OnInitializeStarted()
	{
	}

	public virtual void OnInitializeDone()
	{
	}

	public virtual void OnPreInteractive()
	{
	}

	public virtual void OnPreInteractiveAIGen()
	{
	}

	public virtual void OnInteractive()
	{
	}

	public virtual void OnPreRelease()
	{
	}

	public virtual void OnReleased()
	{
	}

	public virtual void OnDestroyed()
	{
	}

	protected static void InitializeSubmanagers<TSubtype>(TSubtype instance) where TSubtype : ISessionManager
	{
		TypeUtils.MakeMemberInstances<ISubManager<TSubtype>>(instance);
		TypeUtils.GetMemberInstances<ISubManager<TSubtype>>(instance).ForEach(delegate(ISubManager<TSubtype> sub)
		{
			sub.Initialize(instance);
		});
	}

	protected static void ReleaseSubmanagers<TSubtype>(TSubtype instance) where TSubtype : ISessionManager
	{
		TypeUtils.GetMemberInstances<ISubManager<TSubtype>>(instance).Reverse().ForEach(delegate(ISubManager<TSubtype> sub)
		{
			sub.Release();
		});
		TypeUtils.RemoveMemberInstances<ISubManager<TSubtype>>(instance);
	}
}

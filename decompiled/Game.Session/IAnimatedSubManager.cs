using Game.Core;

namespace Game.Session;

public interface IAnimatedSubManager<TParentManager> : ISubManager<TParentManager> where TParentManager : ISessionManager
{
	void UpdateAnimations(GameAnimUpdate anim);
}

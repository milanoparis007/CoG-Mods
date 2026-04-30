using Game.Core;

namespace Game.Session;

public interface IAnimatingManager : ISessionManager
{
	void UpdateAnimations(GameAnimUpdate anim);
}

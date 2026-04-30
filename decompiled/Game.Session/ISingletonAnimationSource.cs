using Game.Core;

namespace Game.Session;

public interface ISingletonAnimationSource : ISessionManager
{
	GameAnimUpdate AdvanceGameAnim(float unityFrameDelta);
}

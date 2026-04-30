using Game.Core;

namespace Game.Session.Entities;

public interface IAnimatedComponent
{
	void UpdateOnFrame(GameAnimUpdate anim);
}

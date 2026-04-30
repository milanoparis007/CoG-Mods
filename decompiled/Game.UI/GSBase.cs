using Game.Services;
using SomaSim.Util;

namespace Game.UI;

public class GSBase : AbstractSmartStackElement
{
	public GameScreenService Service => base.Stack as GameScreenService;

	public virtual void Update()
	{
	}
}

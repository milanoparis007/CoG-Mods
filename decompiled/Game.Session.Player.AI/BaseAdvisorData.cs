using SomaSim.Util;

namespace Game.Session.Player.AI;

public abstract class BaseAdvisorData
{
	public Xorshift rng = new Xorshift();

	public T GetAndClear<T>(ref T value)
	{
		T result = value;
		value = default(T);
		return result;
	}
}

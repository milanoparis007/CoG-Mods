namespace Game.Services;

public interface IEventOfType<T>
{
	T type { get; }
}

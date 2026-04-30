namespace Game.Services;

public interface ILateUpdateService : IService
{
	void OnLateUpdate();
}

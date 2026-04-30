namespace Game.Services;

public interface IUIDialog
{
	bool IsShowing { get; }

	void RequestShow(UIService service);

	void RequestsHide(UIService service);
}

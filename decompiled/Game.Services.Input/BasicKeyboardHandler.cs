using System.Collections.Generic;

namespace Game.Services.Input;

public class BasicKeyboardHandler : KeyboardHandler
{
	private Priority _priority;

	private List<KeyInput> _handlers;

	private Fallthrough _fallthrough;

	public override Priority priority => _priority;

	public override List<KeyInput> keyhandlers => _handlers;

	public override Fallthrough fallthrough => _fallthrough;

	public BasicKeyboardHandler(Priority priority, List<KeyInput> handlers, Fallthrough fallthrough)
	{
		_priority = priority;
		_handlers = handlers;
		_fallthrough = fallthrough;
	}

	public override void Reset()
	{
		_priority = Priority.Lowest;
		_handlers = new List<KeyInput>();
		_fallthrough = Fallthrough.Always;
	}
}

using System.Collections.Generic;

namespace Game.Services.Input;

public abstract class KeyboardHandler
{
	public enum Fallthrough
	{
		Always,
		OnlyIfNotProcessed,
		Never
	}

	public enum Priority
	{
		HighestModalDialog = 1,
		HighNonModalDialog = 5,
		LowKeyboardShortcuts = 9,
		Lowest = 10
	}

	public abstract Priority priority { get; }

	public abstract Fallthrough fallthrough { get; }

	public abstract List<KeyInput> keyhandlers { get; }

	public abstract void Reset();
}

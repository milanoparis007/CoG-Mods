using System;
using System.Collections.Generic;

namespace Game.Services.Input;

public class CapturingKeyboardHandler : KeyboardHandler
{
	private static readonly List<KeyInput> EMPTY = new List<KeyInput>();

	private Func<bool> _shouldCaptureKeys;

	private Priority _priority;

	public override Priority priority => _priority;

	public override List<KeyInput> keyhandlers => EMPTY;

	public override Fallthrough fallthrough
	{
		get
		{
			if (!_shouldCaptureKeys())
			{
				return Fallthrough.Always;
			}
			return Fallthrough.Never;
		}
	}

	public CapturingKeyboardHandler(Func<bool> shouldCaptureKeys, Priority priority)
	{
		_shouldCaptureKeys = shouldCaptureKeys;
		_priority = priority;
	}

	public override void Reset()
	{
		_shouldCaptureKeys = null;
	}
}

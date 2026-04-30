using UnityEngine;

namespace Game.Services.Input;

public static class KeyUtil
{
	public static bool IsShiftDown
	{
		get
		{
			if (!UnityEngine.Input.GetKey(KeyCode.LeftShift))
			{
				return UnityEngine.Input.GetKey(KeyCode.RightShift);
			}
			return true;
		}
	}

	public static bool IsCtrlDown
	{
		get
		{
			if (!UnityEngine.Input.GetKey(KeyCode.LeftControl))
			{
				return UnityEngine.Input.GetKey(KeyCode.RightControl);
			}
			return true;
		}
	}

	public static bool IsAltDown
	{
		get
		{
			if (!UnityEngine.Input.GetKey(KeyCode.LeftAlt))
			{
				return UnityEngine.Input.GetKey(KeyCode.RightAlt);
			}
			return true;
		}
	}
}

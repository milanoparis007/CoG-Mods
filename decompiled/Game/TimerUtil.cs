using System;
using System.Collections;
using UnityEngine;

namespace Game;

public sealed class TimerUtil
{
	private static IEnumerator RunNextFrameHelper(Action action)
	{
		yield return 0;
		action();
	}

	private static IEnumerator RunAfterTimeHelper(Action action, float seconds)
	{
		yield return new WaitForSeconds(seconds);
		action();
	}

	public static void RunNextFrame(Action action)
	{
		Game.instance.StartCoroutine(RunNextFrameHelper(action));
	}

	public static void RunAfterTime(Action action, float seconds)
	{
		Game.instance.StartCoroutine(RunAfterTimeHelper(action, seconds));
	}
}

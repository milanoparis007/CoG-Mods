using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SomaSim.Util;

namespace Game.Session.Sim;

public sealed class LawCallbackManager
{
	private Dictionary<string, Action> _callbacks = new Dictionary<string, Action>();

	private static List<string> AllCallbackNames;

	static LawCallbackManager()
	{
		AllCallbackNames = new List<string>();
		AllCallbackNames.AddRange(from m in FindAllLawCallbacks()
			select m.Name);
	}

	internal void Initialize()
	{
		InitializeCallbacks();
	}

	private static IEnumerable<MethodInfo> FindAllLawCallbacks()
	{
		return typeof(LawCallbacks).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy);
	}

	private static Action MakeLawCallback(object me, MethodInfo m)
	{
		return delegate
		{
			m.Invoke(null, null);
		};
	}

	private void InitializeCallbacks()
	{
		foreach (MethodInfo item in FindAllLawCallbacks())
		{
			_callbacks.Add(item.Name, MakeLawCallback(this, item));
		}
	}

	public void ProcessCallbackFromName(string callbackName)
	{
		ProcessCallback(_callbacks, callbackName);
	}

	private void ProcessCallback(Dictionary<string, Action> dict, string key)
	{
		if (key != null)
		{
			Action action = dict.FindOrNull(key);
			if (action == null)
			{
				Logger.Error("Unknown callback name: " + key);
			}
			else
			{
				action();
			}
		}
	}
}

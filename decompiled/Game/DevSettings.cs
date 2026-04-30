using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using SomaSim.Util;

namespace Game;

public static class DevSettings
{
	public interface IDevKeyProvider
	{
		Dictionary<string, string> DevKeys { get; }
	}

	[Conditional("UNITY_EDITOR")]
	public static void Initialize()
	{
		List<IDevKeyProvider> list = (from type in TypeUtils.FindAllChildrenOf(typeof(IDevKeyProvider))
			select (IDevKeyProvider)Activator.CreateInstance(type)).ToList();
		Logger.DevKeys.Clear();
		foreach (IDevKeyProvider item in list)
		{
			foreach (KeyValuePair<string, string> devKey in item.DevKeys)
			{
				Logger.DevKeys[devKey.Key] = devKey.Value;
			}
		}
	}
}

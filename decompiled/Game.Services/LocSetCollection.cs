using System.Collections.Generic;

namespace Game.Services;

internal sealed class LocSetCollection
{
	public readonly LocDataSet builtins;

	public readonly LocDataSet moddata;

	public LocSetCollection()
	{
		builtins = new LocDataSet();
		moddata = new LocDataSet();
	}

	public void Clear()
	{
		moddata.ClearLanguageData();
		builtins.ClearLanguageData();
	}

	public bool Contains(LocKey k)
	{
		if (!builtins.Contains(k))
		{
			return moddata.Contains(k);
		}
		return true;
	}

	public List<string> Find(LocKey k)
	{
		bool flag = false;
		LocDataSet.Result result = builtins.Find(k);
		flag = flag || result.fallback;
		if (result.values == null)
		{
			result = moddata.Find(k);
			flag = flag || result.fallback;
		}
		if (flag && k.IsDefaultLang)
		{
			Logger.Error($"Missing loc key {k}");
		}
		return result.values;
	}
}

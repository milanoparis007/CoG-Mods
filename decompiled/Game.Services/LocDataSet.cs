using System.Collections.Generic;
using System.Linq;
using SomaSim.Util;

namespace Game.Services;

internal sealed class LocDataSet
{
	public struct Result
	{
		public List<string> values;

		public bool fallback;

		public Result(List<string> values, bool fallback)
		{
			this.values = values;
			this.fallback = fallback;
		}
	}

	public readonly Dictionary<string, LocLangData> langdata;

	public LocDataSet()
	{
		langdata = new Dictionary<string, LocLangData>();
	}

	public void AppendLanguageData(LocLangData data, string langid, string langname = null)
	{
		LocLangData locLangData = langdata.FindOrAddNew(langid);
		locLangData.langid = locLangData.langid ?? langid;
		locLangData.langname = locLangData.langname ?? langname;
		data.ConcatInto(locLangData);
	}

	public void ClearLanguageData()
	{
		langdata.Clear();
	}

	private bool ContainsHelper(LocKey k)
	{
		return langdata.FindOrNull(k.lang)?.ContainsKey(k.key) ?? false;
	}

	public bool Contains(LocKey k)
	{
		if (ContainsHelper(k))
		{
			return true;
		}
		if (k.lang == "en")
		{
			return false;
		}
		return ContainsHelper(k.ToDefaultLang());
	}

	private List<string> FindHelper(LocKey k)
	{
		return langdata.FindOrNull(k.lang)?.FindOrNull(k.key);
	}

	public Result Find(LocKey k)
	{
		List<string> list = FindHelper(k);
		if (list != null)
		{
			return new Result(list, fallback: false);
		}
		if (k.lang == "en")
		{
			return new Result(null, fallback: true);
		}
		return new Result(FindHelper(k.ToDefaultLang()), fallback: true);
	}

	public List<LanguageChoice> GetLanguages()
	{
		return langdata.Select((KeyValuePair<string, LocLangData> e) => new LanguageChoice
		{
			langid = e.Key,
			langname = e.Value.langname
		}).ToList();
	}

	public LocLangData GetLanguageDataUnsafe(string langid)
	{
		return langdata.FindOrNull(langid);
	}
}

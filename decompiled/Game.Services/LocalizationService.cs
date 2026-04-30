using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SomaSim.Util;

namespace Game.Services;

public sealed class LocalizationService : AbstractService
{
	private LocDirectoryLoader _loader;

	private LocSetCollection _locdata;

	private Xorshift _random;

	public Listeners OnLanguageChanged;

	private NumberFormatInfo _numFormatter;

	private const string MISSING_STRING = "?";

	public string currentLang { get; private set; }

	public Encyclopedia pedia { get; private set; }

	public override bool IsLoadingDone
	{
		get
		{
			if (_loader != null)
			{
				return _loader.IsDone;
			}
			return false;
		}
	}

	public bool IsCurrentLanguageNonLatin
	{
		get
		{
			if (!(currentLang == "ru"))
			{
				return currentLang == "zh";
			}
			return true;
		}
	}

	public override void OnCreated()
	{
		Loc.instance = this;
		currentLang = "en";
		pedia = new Encyclopedia();
		_locdata = new LocSetCollection();
		_random = new Xorshift();
	}

	public override void OnStartLoading()
	{
		OnLanguageChanged = new Listeners();
		_loader = new LocDirectoryLoader("Loc", ProcessLocFiles);
		_loader.Start();
	}

	public override void OnReleased()
	{
		OnLanguageChanged.Clear();
		pedia = null;
		_loader = null;
		_locdata.Clear();
		Loc.instance = null;
	}

	public NumberFormatInfo GetNumberFormatter()
	{
		_numFormatter = _numFormatter ?? Game.serv.saveload.prefs.game.GetCurrentNumberFormat();
		return _numFormatter;
	}

	private void ResetNumberFormatter()
	{
		_numFormatter = null;
	}

	public List<LanguageChoice> GetLanguages()
	{
		return _locdata.builtins.GetLanguages();
	}

	public void OnGameplaySettingsChanged(string langid)
	{
		SwitchLanguage(langid);
		ResetNumberFormatter();
	}

	public void SwitchLanguage(string langid)
	{
		if (_locdata.builtins.langdata.ContainsKey(langid))
		{
			currentLang = langid;
			pedia.Rebuild();
			OnLanguageChanged.Invoke();
		}
		else
		{
			Logger.Error("Missing loc data for language " + langid);
		}
	}

	private void ProcessLocFiles(List<string> contents)
	{
		foreach (string content in contents)
		{
			ParseLanguageFile(content);
		}
	}

	private void ParseLanguageFile(string contents)
	{
		LocLangData data = new LocLangData();
		bool num = FileUtil.ParseTabSeparatedFile(contents, delegate(int linenumber, string[] elements)
		{
			if (linenumber != 0 && elements.Length >= 2)
			{
				string key = elements[0];
				string value = elements[1];
				data.AddToList(key, value);
			}
		});
		string text = data.FindOrNull("__id")?.FirstOrDefaultFast() ?? null;
		string langname = data.FindOrNull("__name")?.FirstOrDefaultFast() ?? null;
		if (num && data.Count > 0 && text != null)
		{
			_locdata.builtins.AppendLanguageData(data, text, langname);
		}
		else
		{
			Logger.Error("Failed to load loc file for language " + text);
		}
		if (text == null)
		{
			Logger.Error("Missing language identifier $$id while loading loc file");
		}
		if (text != null && (data.FindOrNull("__id")?.Count ?? 0) != 1)
		{
			Logger.Error("There should be exactly one language identifier field $$id when loading loc file for " + text);
		}
	}

	internal LocLangData GetLangDataUnsafe()
	{
		return _locdata.builtins.GetLanguageDataUnsafe(currentLang);
	}

	public bool HasKey(string key)
	{
		return _locdata.Contains(new LocKey(key, currentLang));
	}

	private string GetRawValue(string key, IRandom random, string lang = null)
	{
		if (key == null)
		{
			return "?";
		}
		List<string> list = _locdata.Find(new LocKey(key, lang ?? currentLang));
		if (list == null)
		{
			return "?";
		}
		if (list.Count == 1)
		{
			return list[0];
		}
		return (random ?? _random).PickElement(list);
	}

	private List<string> GetRawValuesArray(string key, string lang = null)
	{
		if (key == null)
		{
			return null;
		}
		List<string> list = _locdata.Find(new LocKey(key, lang ?? currentLang));
		if (list == null)
		{
			Logger.Error("Loc key missing or doesn't point to array: " + key);
		}
		return list;
	}

	public List<string> GetAllPossibleValues(string key)
	{
		List<string> rawValuesArray = GetRawValuesArray(key);
		if (rawValuesArray != null)
		{
			return new List<string>(rawValuesArray);
		}
		return new List<string>();
	}

	public string GetValue(string key, IRandom rng, params object[] replacements)
	{
		return GetValue(key, new LocReplacementContext(rng, replacements));
	}

	public string GetValue(string key, LocReplacementContext ctx, int depth = 0)
	{
		if (depth >= 5)
		{
			Logger.Error("Exceeded loc recursion depth with key " + key);
			return null;
		}
		string text = GetRawValue(key, ctx.rng, ctx.forcedlang);
		string.IsNullOrWhiteSpace(text);
		while (true)
		{
			int num = text.IndexOf('{');
			if (num < 0)
			{
				break;
			}
			int num2 = text.IndexOf('}') + 1;
			int num3 = num2 - num - 2;
			if (num3 < 0)
			{
				break;
			}
			string token = text.Substring(num + 1, num3);
			string value = text[num + 1] switch
			{
				'$' => ReplacePersonal(token, ctx), 
				'#' => ReplacePattern(token, ctx, depth), 
				_ => ReplaceToken(key, token, ctx), 
			};
			StringBuilder stringBuilder = StringBuilderPool.Instance.Allocate();
			stringBuilder.Append(text, 0, num);
			stringBuilder.Append(value);
			stringBuilder.Append(text, num2, text.Length - num2);
			text = stringBuilder.ToStringAndReturnToPool();
		}
		return text;
	}

	public static string Stringify(object o)
	{
		if (!(o is string result))
		{
			if (o == null)
			{
				return "?";
			}
			return o.ToString();
		}
		return result;
	}

	private string ReplaceToken(string key, string token, LocReplacementContext ctx)
	{
		object[] replacements = ctx.replacements;
		if (replacements != null)
		{
			int i = 0;
			for (int num = replacements.Length; i < num; i += 2)
			{
				if (token.Equals(Stringify(replacements[i])))
				{
					return Stringify(replacements[i + 1]);
				}
			}
		}
		return "";
	}

	private string ReplacePattern(string token, LocReplacementContext ctx, int depth)
	{
		string key = token.Substring(1);
		return GetValue(key, ctx, depth + 1);
	}

	private string ReplacePersonal(string token, LocReplacementContext ctx)
	{
		if (ctx.person.HasValue)
		{
			switch (token)
			{
			case "$eth":
				return GetEthnicityAdjective(ctx);
			case "$firstname":
				return ctx.person.Value.first;
			case "$lastname":
				return ctx.person.Value.last;
			case "$cityname":
				return Game.ctx.session.mapconfig.GetCityName(ctx);
			case "$statename":
				return Game.ctx.session.mapconfig.GetStateName(ctx);
			}
		}
		return null;
		static string GetEthnicityAdjective(LocReplacementContext cctx)
		{
			return Game.serv.globals.settings.ethnicities.FindEthnicityDef(cctx.person.Value.ethid)?.loc?.GetEthnicity(cctx);
		}
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using Game.Code.Effects;
using Game.Core;
using Game.Services;
using Game.Services.Filesystem;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Board;

public class SeasonManager : AbstractSessionManager, IAnimatingManager, ISessionManager
{
	private class Weather
	{
		public WeatherFX fx;

		public WeatherFX.Type selected;

		public WeatherFX.Type? forced;

		public bool zoomedin;

		public bool disabled;

		public DateTime nextUpdate;
	}

	private SeasonConfig _seasonConfig;

	private int _overrideDay = -1;

	private float _overrideHour = -1f;

	private Xorshift _rng;

	private Weather _weather;

	private Light _sun;

	private List<SeasonData> _seasons;

	private List<ASR> _seasonAsrs;

	private List<ASR> _hourAsrs;

	private List<float> _seasonValues;

	private List<float> _hourValues;

	private Color _hourColorMultiplier = Color.white;

	private const float TOD_UPDATE_FPS = 1f;

	private const float TOD_HOURS_PER_RLSECOND = 0.02f;

	private int _lastUpdate;

	public SeasonData CurrentSeason { get; private set; }

	public float GetSpringFoliageHue()
	{
		FloatRange foliageHueShift = _seasonConfig.GetSpring().foliageHueShift;
		return Mathf.Lerp(foliageHueShift.from, foliageHueShift.to, _rng.GenerateFloat());
	}

	public float GetFallFoliageHue()
	{
		FloatRange foliageHueShift = _seasonConfig.GetFall().foliageHueShift;
		return Mathf.Lerp(foliageHueShift.from, foliageHueShift.to, _rng.GenerateFloat());
	}

	public float GetSnowThresholdForTrees()
	{
		return _rng.GenerateFloat();
	}

	public override void OnInitializeDone()
	{
		base.OnInitializeDone();
		_seasonConfig = Game.ctx.session.mapconfig.seasons;
		_rng = Game.ctx.scenario.MakeSeededRng<SeasonManager>();
		Game.ctx.console.Add(this, new DebugConsoleEntry("visual", "season", CheatOverrideDate));
		Game.ctx.console.Add(this, new DebugConsoleEntry("visual", "hour", CheatOverrideHour));
		WeatherFX.Type[] valuesAsArray = EnumUtil<WeatherFX.Type>.GetValuesAsArray();
		for (int i = 0; i < valuesAsArray.Length; i++)
		{
			WeatherFX.Type type = valuesAsArray[i];
			Game.ctx.console.Add(this, new DebugConsoleEntry("visual", "weather", type.ToString().ToLowerInvariant(), (string[] args) => CheatOverrideWeather(type)));
		}
		Game.ctx.console.Add(this, new DebugConsoleEntry("visual", "weather", "reset", (string[] args) => CheatOverrideWeather(null)));
		_sun = UnityEngine.Object.FindObjectOfType<Light>();
		_seasons = _seasonConfig.data;
		_seasonAsrs = _seasons.Select((SeasonData segment) => new ASR(segment.dateRange)).ToList();
		_seasonValues = ListGenerators.ListOfDefaultValues<float>(_seasonAsrs.Count);
		List<TerritorySettings.TODColorSegment> segments = Game.serv.globals.settings.general.territory.timeOfDayColors.segments;
		_hourAsrs = segments.Select((TerritorySettings.TODColorSegment segment) => new ASR(segment.hourRange)).ToList();
		_hourValues = ListGenerators.ListOfDefaultValues<float>(_hourAsrs.Count);
		_overrideHour = FindTimeOverrideFromSettings();
		_overrideDay = FindDayOverrideFromSettings();
		_weather = new Weather();
		_weather.fx = Game.serv.camera.GetWeatherFX();
		_weather.fx.StopAll();
		_weather.fx.SetVisible(visible: false);
		_weather.nextUpdate = DateTime.MinValue;
		_weather.disabled = !Game.serv.saveload.prefs.game.weatherEnabled;
		Game.serv.camera.OnCameraZoomPercentage.Add(OnCameraZoom);
	}

	public override void OnPreRelease()
	{
		base.OnPreRelease();
		Game.ctx.console.Remove(this);
		Game.serv.camera.OnCameraZoomPercentage.Remove(OnCameraZoom);
		_weather.fx.StopAll();
		_weather.fx.SetVisible(visible: false);
		_weather = null;
		_hourColorMultiplier = Color.white;
		_overrideHour = -1f;
	}

	private void OnCameraZoom(float zoomPercentage)
	{
		FloatRange showWeatherZoomRange = Game.serv.globals.settings.general.territory.showWeatherZoomRange;
		bool show = zoomPercentage >= showWeatherZoomRange.from && zoomPercentage < showWeatherZoomRange.to;
		UpdateWeatherFXOnZoom(show);
	}

	public Color GetHourSunColorMultiplier()
	{
		return _hourColorMultiplier;
	}

	public Color GetHourAmbientColorMultiplier()
	{
		return Color.Lerp(Color.white, _hourColorMultiplier, 0.4f);
	}

	private float HoursSinceMidnight()
	{
		return (float)(DateTime.Now.Hour * 60 + DateTime.Now.Minute) / 60f;
	}

	private void MaybeTODUpdateBasedOnComputerTime()
	{
		if (Game.serv.saveload.prefs.game.timeofday == GamePreferences.TimeOfDayOption.Computer)
		{
			_overrideHour = HoursSinceMidnight();
		}
	}

	private float FindTimeOverrideFromSettings()
	{
		return Game.serv.saveload.prefs.game.timeofday switch
		{
			GamePreferences.TimeOfDayOption.Daytime => 12f, 
			GamePreferences.TimeOfDayOption.Evening => 18f, 
			GamePreferences.TimeOfDayOption.Nightime => 2f, 
			GamePreferences.TimeOfDayOption.Morning => 6f, 
			GamePreferences.TimeOfDayOption.Computer => HoursSinceMidnight(), 
			_ => -1f, 
		};
	}

	private int FindDayOverrideFromSettings()
	{
		return Game.serv.saveload.prefs.game.seasons switch
		{
			GamePreferences.SeasonsOption.SummerOnly => 180, 
			GamePreferences.SeasonsOption.AutumnOnly => 270, 
			GamePreferences.SeasonsOption.WinterOnly => 0, 
			GamePreferences.SeasonsOption.Computer => DateTime.Now.DayOfYear, 
			_ => -1, 
		};
	}

	private float GetSeconds()
	{
		return Game.ctx.clock.LastGameAnimUpdate.cumulativeSeconds + 600f;
	}

	public void UpdateAnimations(GameAnimUpdate anim)
	{
		int num = (int)(GetSeconds() / 1f);
		if (_lastUpdate != num)
		{
			_lastUpdate = num;
			if (_overrideHour <= 0f)
			{
				UpdateColors();
			}
			UpdateWeatherFXOnTimer();
		}
	}

	public void Update(bool skipHourUpdate = false)
	{
		if (!skipHourUpdate)
		{
			MaybeTODUpdateBasedOnComputerTime();
		}
		UpdateColors();
	}

	public float FindDisplayedTimeOfDay()
	{
		if (!(_overrideHour >= 0f))
		{
			return GetSeconds() * 0.2f % 24f;
		}
		return _overrideHour;
	}

	public int FindDisplayedDayOfYear()
	{
		if (_overrideDay < 0)
		{
			return Game.ctx.clock.Now.DayOfYear;
		}
		return _overrideDay;
	}

	private void UpdateColors()
	{
		if (Game.ctx != null && Game.ctx.clock != null)
		{
			Shader.SetGlobalFloat("_Season", Game.serv.globals.settings.general.debug.ignoreSeasons ? 0f : 1f);
			int num = FindDisplayedDayOfYear();
			float num2 = FindDisplayedTimeOfDay();
			ASR.GetValues(num2, 0f, 24f, 2f, _hourAsrs, _hourValues, out var currentSeason);
			List<TerritorySettings.TODColorSegment> segments = Game.serv.globals.settings.general.territory.timeOfDayColors.segments;
			TerritorySettings.TODColorSegment tODColorSegment = segments[currentSeason];
			TerritorySettings.TODColorSegment tODColorSegment2 = segments[(currentSeason + 1) % segments.Count];
			float t = _hourValues[currentSeason];
			_hourColorMultiplier = Color.Lerp(tODColorSegment2.colorMultiplier, tODColorSegment.colorMultiplier, t);
			float value = Mathf.Lerp(tODColorSegment2.nightValue, tODColorSegment.nightValue, t);
			ASR.GetValues(num, 0f, 365f, 15f, _seasonAsrs, _seasonValues, out var currentSeason2);
			SeasonData seasonData = _seasons[currentSeason2];
			SeasonData seasonData2 = _seasons[(currentSeason2 + 1) % _seasons.Count];
			float num3 = _seasonValues[currentSeason2];
			CurrentSeason = ((num3 > 0.5f) ? seasonData : seasonData2);
			Color color = Color.Lerp(seasonData2.sunColor, seasonData.sunColor, num3);
			float intensity = Mathf.Lerp(seasonData2.sunIntensity, seasonData.sunIntensity, num3);
			_sun.color = color * GetHourSunColorMultiplier();
			_sun.intensity = intensity;
			Color color2 = Color.Lerp(seasonData2.ambientColor, seasonData.ambientColor, num3);
			float p = Mathf.Lerp(seasonData2.ambientIntensity, seasonData.ambientIntensity, num3);
			Color fogColor = Color.Lerp(seasonData2.fogColor, seasonData.fogColor, num3);
			float fogDensity = Mathf.Lerp(seasonData2.fogIntensity, seasonData.fogIntensity, num3);
			RenderSettings.ambientLight = color2 * GetHourAmbientColorMultiplier() * Mathf.Pow(2f, p);
			RenderSettings.fogColor = fogColor;
			RenderSettings.fogDensity = fogDensity;
			float value2 = Mathf.Lerp(seasonData2.groundHueShift, seasonData.groundHueShift, num3);
			float value3 = Mathf.Lerp(seasonData2.groundSaturationShift, seasonData.groundSaturationShift, num3);
			float value4 = Mathf.Lerp(seasonData2.foliageSaturationShift, seasonData.foliageSaturationShift, num3);
			float value5 = _seasonValues[3];
			Shader.SetGlobalColor("_EmissiveMultiplier", Game.serv.globals.settings.general.territory.timeOfDayColors.emissiveMultiplier);
			Shader.SetGlobalFloat("_SnowAmount", value5);
			Shader.SetGlobalFloat("_TimeOfDay", num2);
			Shader.SetGlobalFloat("_NightValue", value);
			Shader.SetGlobalFloat("_GroundHueShift", value2);
			Shader.SetGlobalFloat("_GroundSaturationShift", value3);
			Shader.SetGlobalFloat("_FoliageSaturationShift", value4);
			float value6 = _seasonValues[0];
			Shader.SetGlobalFloat("_SpringHues", value6);
			float value7 = _seasonValues[2];
			Shader.SetGlobalFloat("_FallHues", value7);
		}
	}

	public void OverrideDate(int dayOfYear)
	{
		_overrideDay = MathUtil.Clamp(dayOfYear, -1, 366);
		Update(skipHourUpdate: true);
	}

	public void OverrideHour(float hourOfDay)
	{
		_overrideHour = MathUtil.Clamp(hourOfDay, -1f, 24f);
		Update(skipHourUpdate: true);
	}

	private void UpdateWeatherFXOnZoom(bool show)
	{
		if (!(_weather.fx == null) && !_weather.disabled)
		{
			_weather.zoomedin = show;
			ShowOrHideSelectedWeather(show, fromzoom: true);
		}
	}

	private void UpdateWeatherFXOnTimer()
	{
		if (!(_weather.fx == null) && !_weather.disabled && !(_weather.nextUpdate > DateTime.Now))
		{
			var (type, num) = SelectNewWeatherType();
			_weather.nextUpdate = DateTime.Now.AddSeconds(num);
			_weather.selected = type;
			ShowOrHideSelectedWeather(type != WeatherFX.Type.None, fromzoom: false);
		}
	}

	private (WeatherFX.Type type, float duration) SelectNewWeatherType()
	{
		int dayOfYear = FindDisplayedDayOfYear();
		float hourOfDay = FindDisplayedTimeOfDay();
		WeatherConfig config = CurrentSeason.weather;
		WeatherFX.Type item = _weather.forced ?? (DoesPass() ? config.type : WeatherFX.Type.None);
		float item2 = config?.durationSeconds ?? 30f;
		return (type: item, duration: item2);
		bool DoesPass()
		{
			if (config == null)
			{
				return false;
			}
			if (!(hourOfDay >= config.hoursOfDay.from) || !(hourOfDay < config.hoursOfDay.to))
			{
				return false;
			}
			if (!new SplitMix64((uint)(((float)dayOfYear * 24f + hourOfDay) * 1000f)).CheckProbability(config.prob))
			{
				return false;
			}
			return true;
		}
	}

	private void ShowOrHideSelectedWeather(bool show, bool fromzoom)
	{
		if (fromzoom)
		{
			if (_weather.fx.IsVisible != show)
			{
				_weather.fx.SetVisible(show);
				SelectedHelper(show, prewarm: true);
			}
		}
		else if (_weather.zoomedin)
		{
			SelectedHelper(show, prewarm: false);
		}
		void SelectedHelper(bool flag, bool prewarm)
		{
			prewarm = false;
			if (flag)
			{
				_weather.fx.StartEffect(_weather.selected, prewarm);
			}
			else
			{
				_weather.fx.StopAll();
			}
		}
	}

	private string CheatOverrideDate(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<day-of-year>");
		}
		if (!int.TryParse(args[2], out var result))
		{
			_overrideDay = -1;
			return DebugConsoleEntry.InvalidParam("Invalid day of year", args, 2, "<day-of-year>");
		}
		OverrideDate(result);
		if (_overrideDay < 0)
		{
			return "Override seasonal day set to -1, now using game clock time";
		}
		return $"Override seasonal day to {_overrideDay}";
	}

	private string CheatOverrideHour(string[] args)
	{
		if (args.Length != 3)
		{
			return DebugConsoleEntry.InvalidParam("Invalid parameters", args, 2, "<hour-of-day>");
		}
		if (!float.TryParse(args[2], out var result))
		{
			_overrideHour = -1f;
			return DebugConsoleEntry.InvalidParam("Invalid hour of day", args, 2, "<hour-of-day>");
		}
		OverrideHour(result);
		if (_overrideHour < 0f)
		{
			return "Override hour set to -1, now using game clock time";
		}
		return $"Override hour of day to {_overrideHour}";
	}

	private string CheatOverrideWeather(WeatherFX.Type? type)
	{
		_weather.forced = type;
		_weather.nextUpdate = DateTime.MinValue;
		UpdateWeatherFXOnTimer();
		return $"Started weather fx: {type}";
	}
}

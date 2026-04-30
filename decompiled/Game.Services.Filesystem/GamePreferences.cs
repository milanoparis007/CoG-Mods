using System.Collections.Generic;
using System.Globalization;
using Game.Services.Input;
using SomaSim.Util;
using UnityEngine;

namespace Game.Services.Filesystem;

public sealed class GamePreferences
{
	public class NumberFormattingEqualityComparer : IEqualityComparer<NumberFormat>
	{
		public bool Equals(NumberFormat x, NumberFormat y)
		{
			return x == y;
		}

		public int GetHashCode(NumberFormat obj)
		{
			return (int)obj;
		}
	}

	public enum TimeOfDayOption
	{
		Daytime,
		Evening,
		Nightime,
		Morning,
		Cycle,
		Computer
	}

	public enum SeasonsOption
	{
		AllSeasons,
		SummerOnly,
		AutumnOnly,
		WinterOnly,
		Computer
	}

	public enum NumberFormat
	{
		None,
		Comma,
		Period,
		Tick,
		Space
	}

	public enum VolumeFormat
	{
		FeetCubed,
		MetersCubed
	}

	public enum DateFormat
	{
		MonthDayNth,
		MonthDay,
		DayMonth
	}

	public enum TutorialType
	{
		None,
		HintsOnly,
		TutorialAndHints
	}

	public enum AutosaveType
	{
		Never,
		Every5Days,
		Every10Days,
		Every20Days,
		Every40Days
	}

	public enum DeliveryNotifSettings
	{
		AllTickers,
		OnlyShowFailed,
		None
	}

	public static readonly List<TimeOfDayOption> ALL_TIMES_OF_DAY = EnumUtil<TimeOfDayOption>.GetValuesAsList();

	public static readonly List<SeasonsOption> ALL_SEASONS = EnumUtil<SeasonsOption>.GetValuesAsList();

	public static readonly Dictionary<NumberFormat, NumberFormatInfo> ALL_NUMBER_FORMAT_INFOS = new Dictionary<NumberFormat, NumberFormatInfo>(new NumberFormattingEqualityComparer())
	{
		{
			NumberFormat.None,
			MakeFormatter("", ".")
		},
		{
			NumberFormat.Comma,
			MakeFormatter(",", ".")
		},
		{
			NumberFormat.Period,
			MakeFormatter(".", ",")
		},
		{
			NumberFormat.Tick,
			MakeFormatter("'", ",")
		},
		{
			NumberFormat.Space,
			MakeFormatter(" ", ".")
		}
	};

	public static readonly List<NumberFormat> ALL_NUMFORMATS = EnumUtil<NumberFormat>.GetValuesAsList();

	public static readonly List<VolumeFormat> ALL_VOLFORMATS = EnumUtil<VolumeFormat>.GetValuesAsList();

	public static readonly List<DateFormat> ALL_DATEFORMATS = EnumUtil<DateFormat>.GetValuesAsList();

	public static readonly List<TutorialType> ALL_TUTORIALS = EnumUtil<TutorialType>.GetValuesAsList();

	public static readonly List<AutosaveType> ALL_AUTOSAVE = EnumUtil<AutosaveType>.GetValuesAsList();

	public static readonly List<DeliveryNotifSettings> ALL_DELIVERY_NOTIF_TYPES = EnumUtil<DeliveryNotifSettings>.GetValuesAsList();

	public const int DEFAULT_UI_SCALE = 100;

	public float volmusic = 0.8f;

	public float volambient = 0.8f;

	public float voluisfx = 0.8f;

	public string langid = "en";

	public int width;

	public int height;

	public bool postEnabled = true;

	public bool computeShaders = true;

	public bool weatherEnabled = true;

	public bool trafficEnabled = true;

	public bool fullscreen;

	public int autosaveTurns = 20;

	public int uiscale = 100;

	public int vsync = 1;

	public KeyMapper keymapper;

	public TimeOfDayOption timeofday = TimeOfDayOption.Cycle;

	public SeasonsOption seasons;

	public NumberFormat numformat;

	public VolumeFormat volformat;

	public DateFormat dateformat;

	public TutorialType tutorial = TutorialType.TutorialAndHints;

	public AutosaveType autosaveType = AutosaveType.Every20Days;

	public DeliveryNotifSettings deliveryNotifType;

	public int lastdiffindex = 1;

	public int lastboardindex;

	public bool CanShowTutorial => tutorial == TutorialType.TutorialAndHints;

	public bool CanShowHints
	{
		get
		{
			if (tutorial != TutorialType.TutorialAndHints)
			{
				return tutorial == TutorialType.HintsOnly;
			}
			return true;
		}
	}

	public bool ShowFeetCubed => volformat == VolumeFormat.FeetCubed;

	public bool ShowMetersCubed => volformat == VolumeFormat.MetersCubed;

	public bool IsAutosaveEnabled => autosaveTurns > 1;

	public static NumberFormatInfo MakeFormatter(string separator, string dec)
	{
		NumberFormatInfo obj = (NumberFormatInfo)CultureInfo.InvariantCulture.NumberFormat.Clone();
		obj.NumberGroupSeparator = separator;
		obj.NumberDecimalSeparator = dec;
		obj.NumberDecimalDigits = 2;
		return obj;
	}

	public bool ShouldShowDeliveryTicker(bool isWarning)
	{
		return deliveryNotifType switch
		{
			DeliveryNotifSettings.OnlyShowFailed => isWarning, 
			DeliveryNotifSettings.None => false, 
			_ => true, 
		};
	}

	public void SetLanguage(string langid)
	{
		this.langid = langid;
	}

	public void SetResolution(int width, int height, bool fullscreen)
	{
		if (!Game.settings.IsEditor)
		{
			this.width = width;
			this.height = height;
			this.fullscreen = fullscreen;
			Screen.SetResolution(width, height, fullscreen);
		}
	}

	public void SetFullscreen(bool fullscreen)
	{
		if (!Game.settings.IsEditor)
		{
			this.fullscreen = fullscreen;
			Screen.fullScreen = fullscreen;
		}
	}

	public void SetUIScale(int uiscale)
	{
		this.uiscale = uiscale;
		Game.serv.ui.RefreshAllCanvasScale();
	}

	public void SetPostFX(bool postEnabled)
	{
		this.postEnabled = postEnabled;
		Game.serv.camera.SetPostFX(postEnabled);
	}

	public void SetVSync(int vsync)
	{
		this.vsync = vsync;
		QualitySettings.vSyncCount = vsync;
	}

	public void SetAutosave(AutosaveType type)
	{
		autosaveType = type;
		switch (type)
		{
		case AutosaveType.Never:
			autosaveTurns = 0;
			break;
		case AutosaveType.Every5Days:
			autosaveTurns = 5;
			break;
		case AutosaveType.Every10Days:
			autosaveTurns = 10;
			break;
		case AutosaveType.Every20Days:
			autosaveTurns = 20;
			break;
		case AutosaveType.Every40Days:
			autosaveTurns = 40;
			break;
		default:
			Logger.Warning("Unknown autosave increment in PlayerPreferences.cs!");
			break;
		}
	}

	public NumberFormatInfo GetCurrentNumberFormat()
	{
		NumberFormatInfo numberFormatInfo = ALL_NUMBER_FORMAT_INFOS.FindOrNull(numformat);
		if (numberFormatInfo != null)
		{
			return numberFormatInfo;
		}
		Logger.Warning("Missing number formatter for setting number " + numformat);
		return CultureInfo.InvariantCulture.NumberFormat;
	}
}

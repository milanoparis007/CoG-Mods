using System;
using UnityEngine;

namespace Game;

public class GameSettings
{
	public enum BuildStage
	{
		Alpha,
		Beta,
		Screenshots,
		Release
	}

	public static readonly string BUILD_PREFIX = "20220801";

	public static readonly uint version = MakeVersion(1u, 4u, 4u);

	public static readonly uint lastCompatibleSaveFileVersion = MakeVersion(1u, 3u, 99u);

	public static readonly string build = BUILD_PREFIX ?? "";

	public const string COPYRIGHT_YEAR = "2021, 2022";

	public const string URL_SOMASIM = "http://somasim.com";

	public const string URL_KASEDO = "http://kasedogames.com";

	public const string URL_YOUTUBE = "https://www.youtube.com/watch?v=iJqSSHfLoWA&list=PL-AySLCs87QiGmOGJLxLocK6pz2j2Rwwv";

	public const string URL_TWITTER = "https://twitter.com/CityOfGangsters";

	public const string URL_DISCORD = "https://discord.gg/CityofGangsters";

	public const string URL_WEBSITE = "https://www.kasedogames.com/cityofgangsters";

	public const string URL_MODDING = "https://www.kasedogames.com/city-of-gangsters-modding-portal";

	public const bool IsSteamBuild = true;

	public const string PLATFORM_LOG_NAME = "STEAM";

	public const string SAVES_SUBDIR = "saves";

	public const string PREFS_SUBDIR = "prefs";

	public const uint STEAM_APPID = 1386780u;

	public const uint STEAM_COREAPPID = 1386780u;

	public const string REMOTE_SETTINGS = "https://storage.googleapis.com/cog-static/cogs.txt";

	public const string GA_ID = "UA-169203907-2";

	public const string BUILD_SUFFIX = "";

	public const bool NEXT_FEST_DEMO = false;

	public bool IsExpired => ExpirationDate < DateTime.Now;

	public BuildStage CurrentBuildStage => BuildStage.Release;

	public bool IsEditor => Application.isEditor;

	public bool IsDesktop => !Application.isMobilePlatform;

	public bool IsModdingEnabled => true;

	public bool DoEnableWatermark => false;

	public bool DoEnableFPS => false;

	public bool DoEnableEntityLogging => IsEditor;

	public bool DoEnableSettingsValidation => IsEditor;

	public bool IsShowFloor => false;

	public bool IsLoggingBuild => true;

	public bool TestLogging => false;

	public DateTime ExpirationDate => DateTime.MaxValue;

	public static uint MakeVersion(uint major, uint minor, uint point)
	{
		return major * 1000000 + minor * 1000 + point;
	}

	public static (uint major, uint minor, uint point) ParseVersion(uint version)
	{
		return (major: version / 1000000, minor: version / 1000 % 1000, point: version % 1000);
	}

	public static string GetVersionString(uint version)
	{
		var (num, num2, num3) = ParseVersion(version);
		return $"{num}.{num2}.{num3}";
	}

	public static string GetVersionString()
	{
		return GetVersionString(version);
	}

	public static string GetVersionAndBuildString()
	{
		return GetVersionString() + "/" + build;
	}

	public static string GetPlatformAndBuildForLogging()
	{
		return "STEAM/" + GetVersionAndBuildString();
	}

	public bool IsSaveFileVersionCompatible(uint version)
	{
		if (version >= lastCompatibleSaveFileVersion)
		{
			return version <= GameSettings.version;
		}
		return false;
	}
}

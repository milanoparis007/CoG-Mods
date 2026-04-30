using System;
using System.Globalization;
using Steamworks;

namespace Game.Services.Store;

public static class StoreSteamUtils
{
	public static readonly uint PREORDER_PACK = 1643230u;

	public static readonly uint BOURBON_DLC = 1643231u;

	public static readonly uint ATLANTIC_CITY_DLC = 1811230u;

	public static readonly uint CRIMINAL_RECORD_DLC = 1869410u;

	public static readonly uint SHADOW_GOVERNMENT_DLC = 1968890u;

	public static readonly uint ETHPACK_DE = 1968891u;

	public static readonly uint ETHPACK_EN = 1968895u;

	public static readonly uint ETHPACK_IR = 1968894u;

	public static readonly uint ETHPACK_IT = 1968893u;

	public static readonly uint ETHPACK_PL = 1968892u;

	public static string MakeOverlayLinkForGameWorkshopPage()
	{
		return $"steam://url/SteamWorkshopPage/{1386780u}";
	}

	public static string MakeOverlayLinkForWorkshopItem(string workshopid)
	{
		return "steam://url/CommunityFilePage/" + workshopid;
	}

	public static string MakeOverlayLinkForStore()
	{
		return $"steam://url/StoreAppPage/{1386780u}";
	}

	public static string MakeOverlayLinkForAppID(uint appid)
	{
		return "steam://url/StoreAppPage/" + appid.ToString("D", CultureInfo.InvariantCulture);
	}

	public static bool IsSteamClientStarted()
	{
		try
		{
			InteropHelp.TestIfAvailableClient();
			return true;
		}
		catch (InvalidOperationException)
		{
			Logger.Error("You're running a STEAM build - please start up the Steam client and restart");
		}
		return false;
	}
}

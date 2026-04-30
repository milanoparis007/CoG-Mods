using System;
using System.Collections.Generic;
using SomaSim.Util;
using Steamworks;
using UnityEngine;

namespace Game.Services.Store;

public class StoreSteam : StoreConnector
{
	private readonly Dictionary<PackID, uint> PACK_APPIDS = new Dictionary<PackID, uint>(new PackIDEqualityComparer())
	{
		{
			PackID.Preorder,
			StoreSteamUtils.PREORDER_PACK
		},
		{
			PackID.Deluxe,
			StoreSteamUtils.BOURBON_DLC
		},
		{
			PackID.AtlanticCity,
			StoreSteamUtils.ATLANTIC_CITY_DLC
		},
		{
			PackID.CriminalRecord,
			StoreSteamUtils.CRIMINAL_RECORD_DLC
		},
		{
			PackID.ShadowGovernment,
			StoreSteamUtils.SHADOW_GOVERNMENT_DLC
		},
		{
			PackID.EthPackDE,
			StoreSteamUtils.ETHPACK_DE
		},
		{
			PackID.EthPackEN,
			StoreSteamUtils.ETHPACK_EN
		},
		{
			PackID.EthPackIR,
			StoreSteamUtils.ETHPACK_IR
		},
		{
			PackID.EthPackIT,
			StoreSteamUtils.ETHPACK_IT
		},
		{
			PackID.EthPackPL,
			StoreSteamUtils.ETHPACK_PL
		}
	};

	private GameObject _go;

	private ulong _steamId;

	private List<object> _globals = new List<object>();

	private bool _gotUserStats;

	public override bool isActive => SteamManager.Initialized;

	public override void Initialize()
	{
		base.Initialize();
		SteamManager.APPID = new AppId_t(1386780u);
		_go = new GameObject
		{
			name = "SteamManager"
		};
		_go.AddComponent<SteamManager>();
		if (SteamManager.Initialized)
		{
			Logger.LogAlways("Steam initialized for " + SteamFriends.GetPersonaName());
			InitializeCallbacks();
		}
		else if (!StoreSteamUtils.IsSteamClientStarted())
		{
			MaybeNotifyOfSteamFailure();
		}
	}

	public override void Release()
	{
		ReleaseCallbacks();
		UnityEngine.Object.Destroy(_go);
		_go = null;
		base.Release();
	}

	public override bool IsPackInstalled(PackID id)
	{
		uint num = PACK_APPIDS.FindOrDefault(id);
		if (num == 0)
		{
			return false;
		}
		try
		{
			return SteamApps.BIsDlcInstalled(new AppId_t(num));
		}
		catch (Exception)
		{
			return false;
		}
	}

	private void MaybeNotifyOfSteamFailure()
	{
		Logger.Error("STEAM CLIENT NOT FOUND");
	}

	private void InitializeCallbacks()
	{
		_globals.Add(Callback<GameOverlayActivated_t>.Create(OnGameOverlayActivated));
		_globals.Add(Callback<UserAchievementStored_t>.Create(OnUserAchievementStored));
		_globals.Add(Callback<UserStatsReceived_t>.Create(OnUserStatsReceived));
		_globals.Add(Callback<UserStatsStored_t>.Create(OnUserStatsStored));
		_globals.Add(Callback<RemoteStoragePublishedFileSubscribed_t>.Create(delegate
		{
			OnPublishedFileChanged();
		}));
		_globals.Add(Callback<RemoteStoragePublishedFileUnsubscribed_t>.Create(delegate
		{
			OnPublishedFileChanged();
		}));
		SteamUserStats.RequestCurrentStats();
	}

	private void ReleaseCallbacks()
	{
		_globals.Clear();
	}

	private void OnGameOverlayActivated(GameOverlayActivated_t data)
	{
	}

	private void OnUserAchievementStored(UserAchievementStored_t data)
	{
	}

	private void OnUserStatsStored(UserStatsStored_t data)
	{
	}

	private void OnUserStatsReceived(UserStatsReceived_t data)
	{
		_steamId = data.m_steamIDUser.m_SteamID;
		_gotUserStats = true;
	}

	private void OnPublishedFileChanged()
	{
	}

	public override void OpenStorePage()
	{
		Application.OpenURL(StoreSteamUtils.MakeOverlayLinkForStore());
	}

	public override void OpenStorePageForDLC(PackID pack)
	{
		uint num = PACK_APPIDS.FindOrDefault(pack, 0u);
		if (num != 0)
		{
			Application.OpenURL(StoreSteamUtils.MakeOverlayLinkForAppID(num));
		}
	}

	public override void SetPresence(string gang, string date, string city)
	{
		try
		{
			SteamFriends.SetRichPresence("groupname", gang);
			SteamFriends.SetRichPresence("date", date);
			SteamFriends.SetRichPresence("city", city);
			SteamFriends.SetRichPresence("steam_display", "#Status_Default");
		}
		catch (Exception)
		{
		}
	}

	public override void ClearPresence()
	{
		try
		{
			SteamFriends.ClearRichPresence();
		}
		catch (Exception)
		{
		}
	}

	public override object GetUserPlatformID()
	{
		return _steamId;
	}

	public override bool GetAchievement(IAchievementDef def)
	{
		if (!_gotUserStats)
		{
			return false;
		}
		try
		{
			string text = def?.GetSteamId();
			if (text == null)
			{
				return false;
			}
			SteamUserStats.GetAchievement(text, out var pbAchieved);
			return pbAchieved;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public override void SetAchievement(IAchievementDef def)
	{
		try
		{
			string text = def?.GetSteamId();
			if (text != null)
			{
				SteamUserStats.SetAchievement(text);
				SteamUserStats.StoreStats();
			}
		}
		catch (Exception)
		{
		}
	}

	public override void DebugClearAllAchievements()
	{
		SteamUserStats.ResetAllStats(bAchievementsToo: true);
		SteamUserStats.StoreStats();
	}
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Game.Services.Store;
using SomaSim.Util;
using Steamworks;

namespace Game.Services.Mods;

public class SteamModsProvider : IModsProvider
{
	private List<ModMetadata> _moddefs;

	private Dictionary<string, string> _rootpaths;

	public readonly uint MAX_ITEMS = 50u;

	private List<CallResult<CreateItemResult_t>> _handlersCreate = new List<CallResult<CreateItemResult_t>>();

	private List<CallResult<SubmitItemUpdateResult_t>> _handlersSubmit = new List<CallResult<SubmitItemUpdateResult_t>>();

	public void Initialize()
	{
		_moddefs = new List<ModMetadata>();
		_rootpaths = new Dictionary<string, string>();
	}

	public void Release()
	{
		_rootpaths.Clear();
		_moddefs.Clear();
	}

	public string MakeModPreviewPath(ModID modid)
	{
		return "";
	}

	public string MakeModDataDirPath(ModID modid)
	{
		return GetModRootPath(modid);
	}

	public List<ModID> GetModIds()
	{
		return _moddefs.Select((ModMetadata def) => def.modid).ToList();
	}

	public void StartLoadingModDefinitions(Action<ModID, ModMetadata> callback)
	{
		if (StoreSteamUtils.IsSteamClientStarted() && SteamManager.Initialized)
		{
			PublishedFileId_t[] array = new PublishedFileId_t[MAX_ITEMS];
			uint subscribedItems = SteamUGC.GetSubscribedItems(array, MAX_ITEMS);
			subscribedItems = Math.Min(subscribedItems, MAX_ITEMS);
			for (int i = 0; i < subscribedItems; i++)
			{
				LoadWorkshopItem(array[i], callback);
			}
		}
	}

	public void ShowWorkshopOverlay()
	{
		SteamFriends.ActivateGameOverlayToWebPage(StoreSteamUtils.MakeOverlayLinkForGameWorkshopPage());
	}

	private string GetModRootPath(ModID modid)
	{
		return _rootpaths.FindOrNull(modid.id);
	}

	private bool LoadWorkshopItem(PublishedFileId_t id, Action<ModID, ModMetadata> callback)
	{
		if ((SteamUGC.GetItemState(id) & 4) == 0)
		{
			return false;
		}
		if (!SteamUGC.GetItemInstallInfo(id, out var punSizeOnDisk, out var pchFolder, 1024u, out var _))
		{
			return false;
		}
		Logger.LogAlways($"STEAM ITEM INSTALLED {id} AT {pchFolder} SIZE {punSizeOnDisk}");
		_rootpaths[id.m_PublishedFileId.ToString()] = pchFolder;
		StartLoadingExtraInfo(id, callback);
		return true;
	}

	private void StartLoadingExtraInfo(PublishedFileId_t id, Action<ModID, ModMetadata> callback)
	{
		CallResult<SteamUGCRequestUGCDetailsResult_t>.Create(onFinished).Set(SteamUGC.RequestUGCDetails(id, uint.MaxValue), onFinished);
		void onFinished(SteamUGCRequestUGCDetailsResult_t result, bool failed)
		{
			if (failed || result.m_details.m_eResult != EResult.k_EResultOK)
			{
				Logger.LogAlways($"STEAM FAILURE for {id.m_PublishedFileId} error code {result.m_details.m_eResult}");
			}
			else
			{
				string text = id.m_PublishedFileId.ToString();
				ModMetadata modMetadata = new ModMetadata
				{
					modid = ModID.MakeWorkshop(text),
					name = result.m_details.m_rgchTitle,
					description = result.m_details.m_rgchDescription,
					workshopid = text
				};
				_moddefs.Add(modMetadata);
				callback(modMetadata.modid, modMetadata);
			}
		}
	}

	public void CreateNewItem(ModMetadata mod, Action<bool, string> callback)
	{
		if (!StoreSteamUtils.IsSteamClientStarted())
		{
			callback(arg1: true, Loc.Get("ui.options.mods.workshopresult.login"));
			return;
		}
		SteamAPICall_t hAPICall = SteamUGC.CreateItem(new AppId_t(1386780u), EWorkshopFileType.k_EWorkshopFileTypeFirst);
		CallResult<CreateItemResult_t> callResult = CallResult<CreateItemResult_t>.Create(onFinished);
		callResult.Set(hAPICall, onFinished);
		_handlersCreate.Add(callResult);
		void onFinished(CreateItemResult_t result, bool failure)
		{
			string arg = "";
			switch (result.m_eResult)
			{
			case EResult.k_EResultTimeout:
				arg = Loc.Get("ui.options.mods.workshopresult.timeout");
				break;
			case EResult.k_EResultInsufficientPrivilege:
				arg = Loc.Get("ui.options.mods.workshopresult.privs");
				break;
			case EResult.k_EResultNotLoggedOn:
				arg = Loc.Get("ui.options.mods.workshopresult.login");
				break;
			default:
				if (failure || result.m_eResult != EResult.k_EResultOK)
				{
					arg = Loc.Get("ui.options.mods.workshopresult.error", "err", result.m_eResult);
					failure = true;
				}
				break;
			}
			if (result.m_bUserNeedsToAcceptWorkshopLegalAgreement)
			{
				arg = Loc.Get("ui.options.mods.workshipresult.legal");
			}
			if (!failure)
			{
				mod.workshopid = result.m_nPublishedFileId.m_PublishedFileId.ToString(CultureInfo.InvariantCulture);
			}
			callback(failure, arg);
		}
	}

	public void UploadItem(ModMetadata def, Action<bool, string> callback)
	{
		ModsLoader<LocalModsProvider> localLoader = Game.serv.mods.LocalLoader;
		UGCUpdateHandle_t handle = SteamUGC.StartItemUpdate(new AppId_t(1386780u), new PublishedFileId_t(def.GetWorkshopId()));
		bool flag = true && SteamUGC.SetItemTitle(handle, def.name) && SteamUGC.SetItemDescription(handle, def.description) && SteamUGC.SetItemContent(handle, localLoader.MakeModDataDirPath(def.modid)) && SteamUGC.SetItemPreview(handle, localLoader.MakeModPreviewPath(def.modid));
		if (!flag)
		{
			callback(arg1: true, Loc.Get("ui.options.mods.workshopresult.error", "err", flag));
		}
		else
		{
			SteamAPICall_t hAPICall = SteamUGC.SubmitItemUpdate(handle, "");
			CallResult<SubmitItemUpdateResult_t> callResult = CallResult<SubmitItemUpdateResult_t>.Create(onFinished);
			callResult.Set(hAPICall);
			_handlersSubmit.Add(callResult);
		}
		void onFinished(SubmitItemUpdateResult_t result, bool failure)
		{
			if (result.m_eResult != EResult.k_EResultOK)
			{
				callback(arg1: true, Loc.Get("ui.options.mods.workshopresult.error", "err", result.m_eResult));
			}
			else
			{
				SteamFriends.ActivateGameOverlayToWebPage(StoreSteamUtils.MakeOverlayLinkForWorkshopItem(def.workshopid));
				callback(arg1: false, Loc.Get("ui.options.mods.status.uploaded"));
			}
		}
	}
}

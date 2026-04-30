using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Game.Services.Maps;
using SomaSim.Util;
using UnityEngine;

namespace Game.Services.Mods;

public class ModsService : AbstractService
{
	private Dictionary<string, ModMetadata> _allmods;

	private Dictionary<string, ModData> _dataCache;

	public ModsLoader<LocalModsProvider> LocalLoader { get; private set; }

	public ModsLoader<SteamModsProvider> WorkshopLoader { get; private set; }

	public bool IsModdingEnabled => Game.settings.IsModdingEnabled;

	public bool HasEnabledMods
	{
		get
		{
			if (CountKnownMods() <= 0)
			{
				return CountEnabledMods() > 0;
			}
			return true;
		}
	}

	public override void OnInitialized()
	{
		base.OnInitialized();
		LocalLoader = new ModsLoader<LocalModsProvider>();
		LocalLoader.Initialize();
		WorkshopLoader = new ModsLoader<SteamModsProvider>();
		WorkshopLoader.Initialize();
		_allmods = new Dictionary<string, ModMetadata>();
		_dataCache = new Dictionary<string, ModData>();
		if (IsModdingEnabled)
		{
			TimerUtil.RunAfterTime(StartRefreshingModList, 1f);
		}
	}

	public override void OnReleased()
	{
		base.OnReleased();
		_allmods.Clear();
		_dataCache.Clear();
		WorkshopLoader.Release();
		WorkshopLoader = null;
		LocalLoader.Release();
		LocalLoader = null;
	}

	public IModsLoader GetLoader(ModID modid)
	{
		switch (modid.source)
		{
		case ModID.Source.Local:
			return LocalLoader;
		case ModID.Source.Workshop:
			return WorkshopLoader;
		default:
			Logger.Error("Unexpected mod source: " + modid.source);
			return LocalLoader;
		}
	}

	private void StartRefreshingModList()
	{
		_allmods.Clear();
		LocalLoader.StartLoadingModDefinitions(AddToMods);
		WorkshopLoader.StartLoadingModDefinitions(AddToMods);
	}

	private void AddToMods(ModID modid, ModMetadata def)
	{
		if (def == null)
		{
			Logger.Warning("Failed to load mod definition for id " + modid.id);
			return;
		}
		if (_allmods.ContainsKey(def.modid.id))
		{
			Logger.Warning("Duplicate mods found with id " + modid.id);
		}
		_allmods[def.modid.id] = def;
	}

	public bool RemoveFromModsList(ModID modid)
	{
		return _allmods.Remove(modid.id);
	}

	public List<ModMetadata> GetAllModDefinitions()
	{
		return _allmods.Values.ToList();
	}

	public bool IsModEnabled(ModID modid)
	{
		return Game.serv.saveload.prefs.enabledmods.Contains(modid.id);
	}

	public bool SetModEnabled(ModID modid, bool enabled)
	{
		List<string> enabledmods = Game.serv.saveload.prefs.enabledmods;
		bool flag = enabledmods.Contains(modid.id);
		if (!enabled && flag)
		{
			enabledmods.Remove(modid.id);
			Game.serv.saveload.SavePrefs();
			return true;
		}
		if (enabled && !flag)
		{
			enabledmods.Add(modid.id);
			Game.serv.saveload.SavePrefs();
			return true;
		}
		return false;
	}

	public int CountKnownMods()
	{
		return _allmods.Count;
	}

	public int CountEnabledMods()
	{
		return GetEnabledMods().Count();
	}

	public ModMetadata GetModMetadata(ModID modid)
	{
		return _allmods.FindOrNull(modid.id);
	}

	public IEnumerable<ModID> GetEnabledMods()
	{
		return _allmods.Values.Select((ModMetadata def) => def.modid).Where(IsModEnabled);
	}

	public IEnumerable<ModMetadata> GetEnabledModMetas()
	{
		return from id in GetEnabledMods()
			select GetModMetadata(id);
	}

	public ModData FindModDataOrNull(ModID modid)
	{
		ModData modData = _dataCache.FindOrNull(modid.id);
		if (modData == null)
		{
			ModMetadata modMetadata = GetModMetadata(modid);
			modData = LoadModDataFile(modMetadata);
			if (modData != null)
			{
				_dataCache[modid.id] = modData;
			}
		}
		return modData;
	}

	private ModData LoadModDataFile(ModMetadata def)
	{
		ModID modid = def.modid;
		try
		{
			return GetLoader(modid).LoadDataFile(modid);
		}
		catch (Exception ex)
		{
			Logger.Error($"Error parsing moveins for mod id {modid}\n{ex.Message}");
		}
		return null;
	}

	internal void CreateNewMapMod(MapConfig map, string name, string desc, bool showFolder, bool showDocs)
	{
		string id = DateTime.Now.ToString("yyyyMMdd-HHmmssffff");
		ModMetadata modMetadata = new ModMetadata
		{
			name = name,
			description = desc,
			modid = ModID.MakeLocal(id)
		};
		ModData modData = new ModData
		{
			id = 0uL,
			type = ModType.Map,
			map = map
		};
		byte[] preview = Game.serv.camera.TakeScreenshotForSavefile();
		LocalLoader.Handler.CreateMod(modMetadata, modData, preview, LocalLoader.DataFileName);
		_allmods[modMetadata.modid.id] = modMetadata;
		_dataCache[modMetadata.modid.id] = modData;
		if (showFolder)
		{
			ShowModFolder(modMetadata);
		}
		if (showDocs)
		{
			Application.OpenURL("https://www.kasedogames.com/city-of-gangsters-modding-portal");
		}
	}

	public void ShowModFolder(ModMetadata def)
	{
		string text = LocalLoader.MakeModDataDirPath(def.modid);
		text = Path.Combine(text.Replace('/', '\\'), "..");
		Process.Start("explorer.exe", text);
	}

	internal void DeleteModPermanently(ModMetadata def)
	{
		_allmods.Remove(def.modid.id);
		_dataCache.Remove(def.modid.id);
		LocalLoader.Handler.DeleteModPermanently(def);
	}

	internal void OnModsOrDocsClick(bool mods)
	{
		if (mods)
		{
			WorkshopLoader.Handler.ShowWorkshopOverlay();
		}
		else
		{
			Application.OpenURL("https://www.kasedogames.com/city-of-gangsters-modding-portal");
		}
	}
}

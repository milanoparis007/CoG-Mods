using System.Collections;
using Game.Services.Maps;
using SomaSim.Util;

namespace Game.Services;

public sealed class GlobalSettingsService : AbstractService
{
	private DirectoryLoader _hentities;

	private DirectoryLoader _hsettings;

	private DirectoryLoader _hmapgen;

	private DirectoryLoader _hui;

	public ArrayList entities;

	public GlobalSettings settings;

	public MapSettings mapgen;

	public UISettings ui;

	public override bool IsLoadingDone
	{
		get
		{
			if (entities != null)
			{
				return settings != null;
			}
			return false;
		}
	}

	public override void OnCreated()
	{
	}

	public override void OnStartLoading()
	{
		_hentities = new SettingsDirectoryLoader("Entities", arrayFiles: true, OnEntitiesLoaded);
		_hentities.Start();
		_hsettings = new SettingsDirectoryLoader("Settings", arrayFiles: false, OnSettingsLoaded);
		_hsettings.Start();
		_hmapgen = new SettingsDirectoryLoader("Maps", arrayFiles: true, OnMapsLoaded);
		_hmapgen.Start();
		_hui = new SettingsDirectoryLoader("UI", arrayFiles: true, OnUILoaded);
		_hui.Start();
	}

	public override void OnReleased()
	{
		_hentities.Stop();
		_hsettings.Stop();
		_hmapgen.Stop();
		_hui.Stop();
		_hentities = (_hsettings = (_hmapgen = (_hui = null)));
		entities = null;
		settings = null;
		mapgen = null;
		ui = null;
	}

	private void OnEntitiesLoaded(Hashtable data)
	{
		entities = data["entities"] as ArrayList;
	}

	private void OnMapsLoaded(Hashtable data)
	{
		mapgen = Game.serv.serializer.instance.Deserialize<MapSettings>(data);
		Logger.LogAlways("Found maps: " + mapgen.maps.SelectToString((MapConfig cfg) => cfg.id, ","));
		mapgen.maps.StableSort((MapConfig a, MapConfig b) => string.CompareOrdinal(a.id, b.id));
	}

	private void OnUILoaded(Hashtable data)
	{
		ui = Game.serv.serializer.instance.Deserialize<UISettings>(data);
		TypeUtils.GetMemberInstances<ISettingsLoadObserver>(ui).ForEach(delegate(ISettingsLoadObserver s)
		{
			s.OnAfterSettingsLoaded();
		});
		if (Game.settings.DoEnableSettingsValidation)
		{
			ui.Validate();
			TypeUtils.GetMemberInstances<IValidatingSettings>(ui).ForEach(delegate(IValidatingSettings s)
			{
				s.Validate();
			});
		}
		Logger.LogAlways($"Found ui: {ui.convos.Keys.Count} entries");
	}

	private void OnSettingsLoaded(Hashtable data)
	{
		settings = Game.serv.serializer.instance.Deserialize<GlobalSettings>(data);
		TypeUtils.GetMemberInstances<ISettingsLoadObserver>(settings).ForEach(delegate(ISettingsLoadObserver s)
		{
			s.OnAfterSettingsLoaded();
		});
		if (Game.settings.DoEnableSettingsValidation)
		{
			TypeUtils.GetMemberInstances<IValidatingSettings>(settings).ForEach(delegate(IValidatingSettings s)
			{
				s.Validate();
			});
		}
		Logger.LogAlways($"Found models: {settings.modelimport.modelconfigs.Count}");
	}
}

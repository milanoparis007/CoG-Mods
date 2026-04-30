using System;
using System.Collections.Generic;
using System.Linq;
using Game.Services.Audio;
using Game.Services.Input;
using Game.Services.Maps;
using Game.Services.Mods;
using Game.Services.Store;
using Game.UI;
using Game.UI.Mouseovers;
using SomaSim.Util;

namespace Game.Services;

public sealed class ServiceContext
{
	public ServiceEventBus events;

	public ActionSequencerService sequencer;

	public SerializerService serializer;

	public DebugVisualizationService debugvis;

	public RemoteSettingsService remotesettings;

	public LoggerService log;

	public SaveLoadService saveload;

	public AnalyticsService stats;

	public UnityDiagnosticsService diags;

	public GlobalSettingsService globals;

	public LocalizationService loc;

	public UIService ui;

	public PortraitMakerService portraits;

	public CameraService camera;

	public GameScreenService screens;

	public KeyboardService keyboard;

	public InputService input;

	public MouseoverService mouseovers;

	public AudioService audio;

	public DiscordService discord;

	public StoreService store;

	public ModsService mods;

	private List<IService> _all;

	private List<IUpdateService> _updating;

	private List<ILateUpdateService> _lateupdating;

	private ServiceState _state;

	public bool IsInitialized => _state == ServiceState.Initialized;

	private void SetState(ServiceState before, Action<IService> fn, ServiceState after)
	{
		_all.ForEach(fn);
		_state = after;
		if (after != ServiceState.Released && after != ServiceState.Destroyed)
		{
			events.EnqueueOnce(ServiceEventType.ServiceStateChange);
		}
	}

	public void Initialize()
	{
		TypeUtils.MakeMemberInstances<IService>(this);
		_all = TypeUtils.GetMemberInstances<IService>(this).ToList();
		_updating = _all.WhereTypeIs<IUpdateService>().ToList();
		_lateupdating = _all.WhereTypeIs<ILateUpdateService>().ToList();
		SetState(ServiceState.None, delegate(IService service)
		{
			service.OnCreated();
		}, ServiceState.Created);
		SetState(ServiceState.Created, delegate(IService service)
		{
			service.OnStartLoading();
		}, ServiceState.LoadingStarted);
	}

	public void Release()
	{
		_all.Reverse();
		SetState(ServiceState.Initialized, delegate(IService service)
		{
			service.OnReleased();
		}, ServiceState.Released);
		SetState(ServiceState.Released, delegate(IService service)
		{
			service.OnDestroyed();
		}, ServiceState.Destroyed);
		_all = null;
		_lateupdating = null;
		_updating = null;
		TypeUtils.RemoveMemberInstances<IService>(this);
	}

	public void Update()
	{
		switch (_state)
		{
		case ServiceState.LoadingStarted:
			TryFinishLoading();
			break;
		case ServiceState.Initialized:
		{
			foreach (IUpdateService item in _updating)
			{
				item.OnUpdate();
			}
			break;
		}
		}
	}

	public void LateUpdate()
	{
		if (_state != ServiceState.Initialized)
		{
			return;
		}
		foreach (ILateUpdateService item in _lateupdating)
		{
			item.OnLateUpdate();
		}
	}

	private void TryFinishLoading()
	{
		if (_state == ServiceState.LoadingStarted && _all.All((IService service) => service.IsLoadingDone))
		{
			SetState(ServiceState.LoadingStarted, delegate(IService service)
			{
				service.OnLoaded();
			}, ServiceState.Loaded);
			StartInitialize();
		}
	}

	private void StartInitialize()
	{
		SetState(ServiceState.Loaded, delegate(IService service)
		{
			service.OnInitialized();
		}, ServiceState.Initialized);
		string langid = Game.serv.saveload.prefs.game.langid;
		Game.serv.loc.SwitchLanguage(langid);
		screens.Add(new GSMainMenu());
	}
}

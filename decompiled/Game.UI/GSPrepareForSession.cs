using System;
using System.Collections;
using Game.Platform;
using Game.Services.Maps;
using Game.Session;
using SomaSim.Util;

namespace Game.UI;

public sealed class GSPrepareForSession : GSBase
{
	private IPlatformSaveSlotDescriptor _slotDescriptor;

	private SessionPrepResults _results;

	private Action<SessionPrepResults> _onFinished;

	private CoroutineTask _task;

	public GSPrepareForSession(ScenarioConfig scenario, MapConfig map, Action<SessionPrepResults> finishedCallback)
	{
		_onFinished = finishedCallback;
		_results = new SessionPrepResults
		{
			scenario = scenario,
			custommap = map
		};
	}

	public GSPrepareForSession(IPlatformSaveSlotDescriptor slotDesc, Action<SessionPrepResults> finishedCallback)
	{
		_slotDescriptor = slotDesc;
		_results = new SessionPrepResults();
		_onFinished = finishedCallback;
	}

	public override void OnActivated(bool pushed)
	{
		base.OnActivated(pushed);
		_task = Game.serv.sequencer.StartCoroutineTask(LoadActions());
	}

	public override void OnDeactivated(bool popped)
	{
		Game.serv.debugvis.RemoveHeatmap();
		base.OnDeactivated(popped);
	}

	public override void Update()
	{
		base.Update();
		if (_task != null && _task.IsFinished)
		{
			_onFinished(_results);
		}
	}

	private IEnumerator LoadActions()
	{
		if (_slotDescriptor != null)
		{
			_results.savefile = new SaveFileContents(new Hashtable());
			yield return null;
			yield return Game.serv.sequencer.StartCoroutine(Game.serv.saveload.LoadGame(_slotDescriptor, _results.savefile));
			_results.scenario = SessionContext.ParseScenario(_results.savefile.data);
			_results.custommap = SessionContext.ParseMapConfigOrNull(_results.savefile.data);
			_results.custommap = _results.custommap ?? Game.serv.globals.mapgen.FindBuiltInMapConfigByID(_results.scenario.mapdef);
		}
		if (_results.mapconfig == null)
		{
			Logger.Error("Failed to load map file:", _results.scenario.mapdef);
		}
	}
}

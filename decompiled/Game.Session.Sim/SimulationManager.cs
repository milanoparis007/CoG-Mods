using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Services.Store;
using SomaSim.SION;
using SomaSim.Util;

namespace Game.Session.Sim;

public class SimulationManager : AbstractSessionManager, ISystemTurnHandler, ISessionManager, ICityGenManager, ISaveLoadProvider, ISaveObserver, ILoadObserver
{
	public ResidenceTracker residences;

	public PeopleTracker peoplegen;

	public BusinessTracker businesses;

	public PoliticsManager politics;

	public RelationshipTracker rels;

	public CopTracker cops;

	public CombatManager combat;

	public DemandsTracker demands;

	public ResEventManager resevents;

	public HintTracker hints;

	public VictoryTracker victory;

	public List<ISystemTurnSubManager<SimulationManager>> _turnUpdates;

	private StringBuilder tmp_sb = new StringBuilder();

	private Stopwatch tmp_stopwatch = new Stopwatch();

	public override void OnInitializeStarted()
	{
		base.OnInitializeStarted();
	}

	public override void OnInitializeDone()
	{
		AbstractSessionManager.InitializeSubmanagers(this);
		_turnUpdates = TypeUtils.GetMemberInstances<ISystemTurnSubManager<SimulationManager>>(this).ToList();
	}

	public override void OnPreInteractive()
	{
		base.OnPreInteractive();
		if (!Game.ctx.HasSaveFile)
		{
			Game.ctx.heatmaps.ManualUpdateAll();
			peoplegen.RunNewGameFamilyPlacement();
			businesses.RunNewGameBusinessAssignments();
			if (Game.serv.store.IsPackInstalled(PackID.ShadowGovernment))
			{
				politics.RunNewGamePoliticianSetup();
			}
		}
	}

	public override void OnReleased()
	{
		_turnUpdates.Clear();
		AbstractSessionManager.ReleaseSubmanagers(this);
	}

	public void OnCityGenStarted()
	{
	}

	public void OnCityGenDone()
	{
	}

	public void OnCityGenTurn()
	{
		OnNextTurn();
	}

	public void OnSystemTurn()
	{
		OnNextTurn();
	}

	private void OnNextTurn()
	{
		bool flag = true;
		long num = 0L;
		foreach (ISystemTurnSubManager<SimulationManager> turnUpdate in _turnUpdates)
		{
			if (flag)
			{
				tmp_stopwatch.Restart();
			}
			turnUpdate.OnSystemTurn();
			if (flag)
			{
				tmp_stopwatch.Stop();
				tmp_sb.AppendLine($"{turnUpdate} => {tmp_stopwatch.Elapsed.TotalMilliseconds} ms");
				num += tmp_stopwatch.ElapsedMilliseconds;
			}
		}
	}

	public Resource FindResource(Label resId)
	{
		return Game.ctx.resManager.resourcesCache.FindOrNull(resId);
	}

	public void OnBeforeSave()
	{
		SaveLoadUtils.GetEachOfType<ISaveObserver>(this).ForEach(delegate(SaveLoadUtils.MemberProvider<ISaveObserver> entry)
		{
			entry.provider.OnBeforeSave();
		});
	}

	public void Save(Serializer s, ConcurrentSaveTable results)
	{
		Hashtable hashtable = new Hashtable();
		SaveLoadUtils.SaveMembersByNameInParallel(this, hashtable);
		results.PopulateFrom(hashtable);
	}

	public IEnumerator Load(Hashtable data)
	{
		yield return Game.serv.sequencer.StartCoroutine(SaveLoadUtils.LoadMembersByNameCoroutine(this, data));
	}

	public void OnAfterManagerLoad()
	{
		SaveLoadUtils.GetEachOfType<ILoadObserver>(this).ForEach(delegate(SaveLoadUtils.MemberProvider<ILoadObserver> entry)
		{
			entry.provider.OnAfterManagerLoad();
		});
	}

	public void OnAfterEntityLoad()
	{
		SaveLoadUtils.GetEachOfType<ILoadObserver>(this).ForEach(delegate(SaveLoadUtils.MemberProvider<ILoadObserver> entry)
		{
			entry.provider.OnAfterEntityLoad();
		});
	}
}

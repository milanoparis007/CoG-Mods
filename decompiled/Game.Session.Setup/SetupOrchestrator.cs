using System;
using System.Collections;
using Game.Core;
using Game.Platform;
using Game.Services;
using Game.Session.Input;
using Game.UI;
using Game.UI.Session.Popups;
using IndirectRendering;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Setup;

public class SetupOrchestrator : AbstractSessionManager, ISingletonBoardInitManager, ISessionManager, ICityGenManager, ISystemTurnHandler
{
	private SetupOrchestratorContext _ctx;

	private CoroutineTask _boardInitTask;

	private bool _cityGenActive;

	private DateTime _gsStart;

	public bool IsBoardInitDone => _boardInitTask.IsFinished;

	public bool IsCityGenDone => !_cityGenActive;

	public override void OnInitializeDone()
	{
		AbstractSessionManager.InitializeSubmanagers(this);
	}

	public override void OnReleased()
	{
		Game.serv.discord.Clear();
		if (_boardInitTask != null && !_boardInitTask.IsFinished)
		{
			_boardInitTask.Stop();
		}
		_boardInitTask = null;
		_cityGenActive = false;
		PresenceUtil.Clear();
		AbstractSessionManager.ReleaseSubmanagers(this);
	}

	public void OnSystemTurn()
	{
		PresenceUtil.Set(Game.ctx.players.Human.social.PlayerGroupName, Loc.FormatNumber(Game.ctx.clock.Now.YearsInt), Game.ctx.session.mapconfig.CityName);
	}

	public void OnBoardInitStarted()
	{
		IndirectRenderer.PreGameSetup();
		ToggleGSGenerating(show: true);
		IEnumerator coroutine = (Game.ctx.HasSaveFile ? StartLoadingFile(Game.ctx.session.savefile) : SkipLoadingFile());
		_boardInitTask = Game.serv.sequencer.StartCoroutineTask(coroutine);
	}

	private IEnumerator StartLoadingFile(SaveFileContents file)
	{
		_ctx = new SetupOrchestratorContext();
		yield return new CreateMountains(_ctx).Start();
		yield return new CreateWater(_ctx).Start();
		yield return new CreateHeightmap(_ctx).Start();
		Game.ctx.board.terrain.StartTerrainMeshGeneration(_ctx.MakeTerrainGenData());
		while (!Game.ctx.board.terrain.IsTerrainMeshGenerationDone())
		{
			yield return null;
		}
		yield return Game.serv.sequencer.StartCoroutine(Game.ctx.LoadFromSaveFile(file.data));
		Game.ctx.models.CombinedRoadsMesh.PushUpdates();
		Game.serv.audio.MusicStopAll();
		yield return Game.ctx.fogofwar.RefreshEntireMap();
		Game.ctx.board.terrain.UpdateColors(forceHeatmapUpdate: true);
		yield return new WaitForSeconds(2f);
	}

	private IEnumerator SkipLoadingFile()
	{
		_ctx = new SetupOrchestratorContext();
		yield return new CreateMountains(_ctx).Start();
		yield return new CreateWater(_ctx).Start();
		yield return new CreateHeightmap(_ctx).Start();
		Game.ctx.board.terrain.StartTerrainMeshGeneration(_ctx.MakeTerrainGenData());
		while (!Game.ctx.board.terrain.IsTerrainMeshGenerationDone())
		{
			yield return null;
		}
		yield return new CreateMapNodes(_ctx).Start();
		yield return new CreateMapEdges(_ctx).Start();
		yield return new CreateGridConnections(_ctx).Start();
		yield return new CreateRail().Start();
		yield return new CreateRoads(_ctx).Start();
		yield return new CreateIslandConnections(_ctx).Start();
		yield return new CreateMapEdgeBeads(_ctx).Start();
		yield return new CreateTransitTiles(_ctx).Start();
		yield return new CreateTrainStationLocations(_ctx).Start();
		yield return new CreateDistricts().Start();
		yield return new CreateCopStationLocations(_ctx).Start();
		yield return new CreateEmptyLots(_ctx).Start();
		Game.ctx.board.terrain.UpdateColors(forceHeatmapUpdate: true);
		yield return new AssignNamesToStreets().Start();
		yield return new AssignLotsToZones(_ctx).Start();
		yield return new AssignBuildingsToLots().Start();
		yield return new CreateProps(_ctx).Start();
		yield return new CreateTerrainDecos(_ctx).Start();
		yield return new CreateWaterDecos(_ctx).Start();
		yield return new AssignBusinessesToBuildings(_ctx).Start();
		yield return new CreateCopPrecincts(_ctx).Start();
		yield return new CreatePoliticalOffices(_ctx).Start();
		Game.ctx.board.terrain.UpdateColors(forceHeatmapUpdate: true);
		IndirectRenderer.WireUpDebug();
		Game.serv.audio.MusicStopAll();
	}

	private void DebugCollisionOnNodes()
	{
		float num = 10f;
		float num2 = 10f;
		int[] array = new int[3] { 1, 2, 3 };
		for (int i = 0; i < array.Length; i++)
		{
			int index = array[i];
			WorldPos pos = Game.ctx.board.nodes.GetAllNodesUnsafe()[index].pos;
			Debug.Log("ALGO NODE: " + index + ", " + pos.ToString());
			float num3 = 0f - num2;
			for (float num4 = num2; num3 < num4 + 0.1f; num3 += 0.5f)
			{
				float num5 = 0f - num;
				for (float num6 = num; num5 < num6 + 0.1f; num5 += 0.5f)
				{
					WorldPos pos2 = pos + new WorldPos(num5, num3);
					Color color = (Game.ctx.board.DoesPointIntersectAnyEntity(pos2) ? Color.green : Color.red);
					Game.serv.debugvis.AddCube(pos2, color, 0.1f);
				}
			}
		}
	}

	public void OnBoardInitDone()
	{
	}

	public void OnCityGenStarted()
	{
		_cityGenActive = true;
	}

	public void OnCityGenTurn()
	{
		if (Game.ctx.clock.GetDaysLeftForCityGenTurn() <= 0)
		{
			_cityGenActive = false;
		}
	}

	public void OnCityGenDone()
	{
	}

	public override void OnPreInteractive()
	{
		base.OnPreInteractive();
		if (Game.ctx.HasSaveFile)
		{
			SaveLoadUtils.TryFixups();
		}
	}

	public override void OnPreInteractiveAIGen()
	{
		try
		{
			if (!Game.ctx.HasSaveFile)
			{
				using (new BlockStopwatch("procgen", "Generating players"))
				{
					new CreatePlayersHuman(_ctx).RunBlocking();
					new CreatePlayersGang(_ctx).RunBlocking();
					new CreatePlayersCops(_ctx).RunBlocking();
					new CreatePlayersFeds(_ctx).RunBlocking();
					new AfterCreatePlayers().RunBlocking();
				}
				Game.ctx.events.SendImmediate(SessionEventType.OnAfterAIInitNewGame);
			}
			else
			{
				Game.ctx.events.SendImmediate(SessionEventType.OnAfterAIInitLoadedGame);
			}
			string id = Game.ctx.session.mapconfig.id;
			string text = (Game.ctx.HasSaveFile ? "saved_game" : "new_game");
			Game.serv.stats.LogEvent("game_start_" + text, id);
		}
		catch (Exception ex)
		{
			Logger.Error(ex.Message + "\n" + ex.StackTrace);
			Game.ctx.SetProcGenFailed();
		}
	}

	public override void OnInteractive()
	{
		Game.instance.ReclaimMemoryAsync(waitForGC: true);
		ToggleGSGenerating(show: false);
		Game.ctx.events.EnqueueOnce(SessionEventType.OnGameBecomeInteractive);
		if (Game.ctx.IsProcGenFailed())
		{
			OkPopup.ShowOk(Loc.Get("ui.customcity.failed"), delegate
			{
				Game.ctx.QuitGame();
			});
		}
		else
		{
			Game.ctx.tutorial.OnSessionStart();
			Game.serv.audio.MusicStartProcedural();
			DefaultInputMode.Reset();
		}
	}

	public override void OnPreRelease()
	{
		base.OnPreRelease();
		IndirectRenderer.PostGameDestroy();
		Game.serv.audio.MusicStopAll();
	}

	private void ToggleGSGenerating(bool show)
	{
		if (show)
		{
			_gsStart = DateTime.Now;
			IntSize mapSize = Game.ctx.session.mapconfig.map.mapSize;
			Game.serv.camera.ResetCamera(new WorldSize(mapSize.width, mapSize.height));
			Game.serv.screens.Add(new GSGenerateCity());
			Game.serv.ui.ShowOrHideSessionCanvas(show: false);
		}
		else
		{
			Game.serv.ui.ShowOrHideSessionCanvas(show: true);
			if (Game.serv.screens.Top is GSGenerateCity)
			{
				Game.serv.screens.Pop();
			}
		}
	}
}

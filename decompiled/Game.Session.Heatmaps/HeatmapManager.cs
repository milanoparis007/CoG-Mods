using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Game.Core;
using Game.Session.Entities;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Heatmaps;

public class HeatmapManager : AbstractSessionManager, ISystemTurnHandler, ISessionManager, ICityGenManager
{
	public struct HeatmapEntry
	{
		public HeatmapType type;

		public Label tag;

		public Heatmap map;
	}

	private const float UPDATE_DAYS = 7f;

	private List<List<HeatmapEntry>> maps;

	private List<List<HeatmapEntry>> _temp;

	private static readonly string CMD = "heatmap";

	public override void OnInitializeDone()
	{
		maps = new List<List<HeatmapEntry>>();
		maps.AddTimes(() => new List<HeatmapEntry>(), 9);
		_temp = new List<List<HeatmapEntry>>();
		_temp.AddTimes(() => new List<HeatmapEntry>(), 9);
		Game.ctx.console.Add(this, new DebugConsoleEntry(CMD, "off", DebugHeatmap));
		Add<BuildingSizeHeatmap>(Label.NULL, Color.magenta);
		Add<LotZoneHeatmap>(TagConstants.TAG_RESIDENTIAL, Color.green);
		Add<LotZoneHeatmap>(TagConstants.TAG_COMMERCIAL, Color.blue);
		Add<LotZoneHeatmap>(TagConstants.TAG_INDUSTRIAL, Color.yellow);
		Add<DensityHeatmap>(TagConstants.TAG_RESIDENTIAL, Color.green);
		Add<DensityHeatmap>(TagConstants.TAG_COMMERCIAL, Color.blue);
		Add<DensityHeatmap>(TagConstants.TAG_INDUSTRIAL, Color.yellow);
		Add<RailPlacementHeatmap>(Label.NULL, Color.red);
		Add<AppealHeatmap>(Label.NULL, new Color(1f, 0.5f, 0.1f), 2f);
		foreach (Label item in Game.ctx.session.mapconfig.GetEthnicitiesUniqueSorted())
		{
			Add<EthnicityHeatmap>(item, Color.magenta);
		}
		Add<GroundRailHeatmap>(Label.NULL, Color.black, 0.25f);
		Add<GroundBuildingHeatmap>(Label.NULL, Color.black, 0.25f);
	}

	public override void OnReleased()
	{
		Game.ctx.console.Remove(this);
		maps.ClearDeep();
		maps = null;
		_temp.ClearDeep();
		_temp = null;
	}

	public void OnCityGenStarted()
	{
	}

	public void OnCityGenDone()
	{
	}

	public void OnCityGenTurn()
	{
		UpdateHeatmaps();
	}

	public void OnSystemTurn()
	{
		UpdateHeatmaps();
	}

	private void UpdateHeatmaps()
	{
		CopyCurrentMapsToTemp();
		UpdateTempMaps();
		SwapTempAndCurrent();
	}

	private void CopyCurrentMapsToTemp()
	{
		foreach (List<HeatmapEntry> map2 in maps)
		{
			foreach (HeatmapEntry item in map2)
			{
				Heatmap map = item.map;
				Heatmap heatmap = Find(_temp, item.type, item.tag);
				Heatmap.Array2DCopy(map.data, heatmap.data, heatmap.heatmapSize);
			}
		}
	}

	private void UpdateTempMaps()
	{
		Task.WaitAll((from entry in _temp.SelectMany((List<HeatmapEntry> entry) => entry)
			select Task.Run(delegate
			{
				entry.map.UpdateInteractive();
			})).ToArray());
	}

	private void SwapTempAndCurrent()
	{
		List<List<HeatmapEntry>> temp = maps;
		maps = _temp;
		_temp = temp;
	}

	public void ManualUpdate(HeatmapType type, int times)
	{
		Find(type).UpdateCityGen(times);
	}

	public void ManualUpdate(HeatmapType type, Label tag, int times)
	{
		Find(type, tag).UpdateCityGen(times);
	}

	public void ManualUpdateAll()
	{
		UpdateHeatmaps();
	}

	internal void Add<T>(Label tag, Color fillColor, float? scale = null) where T : Heatmap, new()
	{
		Add<T>(maps, tag, scale).config.fillColor = fillColor;
		Add<T>(_temp, tag, scale).config.fillColor = fillColor;
	}

	internal Heatmap Add<T>(List<List<HeatmapEntry>> maps, Label tag, float? scale) where T : Heatmap, new()
	{
		IntSize cellSize = Game.ctx.session.mapconfig.map.heatmapSpacing;
		if (scale.HasValue)
		{
			cellSize = new IntSize((int)((float)cellSize.width * scale).Value, (int)((float)cellSize.height * scale).Value);
		}
		Heatmap heatmap = new T();
		heatmap.Allocate(tag, cellSize);
		maps[(int)heatmap.Type].Add(new HeatmapEntry
		{
			map = heatmap,
			type = heatmap.Type,
			tag = heatmap.tag
		});
		string first = heatmap.Type.ToString().ToLowerInvariant();
		string second = (tag.IsSet ? tag.String.ToLowerInvariant() : null);
		Game.ctx.console.Add(this, new DebugConsoleEntry(CMD, first, second, DebugHeatmap));
		return heatmap;
	}

	internal Heatmap Find(HeatmapType type)
	{
		return Find(maps, type, Label.NULL);
	}

	internal Heatmap Find(HeatmapType type, Label tag)
	{
		return Find(maps, type, tag);
	}

	internal Heatmap FindEthnicityMap(Label eth)
	{
		return Find(HeatmapType.Ethnicity, eth);
	}

	internal Heatmap FindDensityMap(Label tag)
	{
		return Find(HeatmapType.Density, tag);
	}

	private static Heatmap Find(List<List<HeatmapEntry>> dict, HeatmapType type, Label tag)
	{
		List<HeatmapEntry> list = dict[(int)type];
		if (list == null || list.Count == 0)
		{
			return null;
		}
		int i = 0;
		for (int count = list.Count; i < count; i++)
		{
			HeatmapEntry heatmapEntry = list[i];
			if (heatmapEntry.tag == tag)
			{
				return heatmapEntry.map;
			}
		}
		return null;
	}

	private string DebugHeatmap(string[] arg)
	{
		if (arg.Length <= 1 || arg[1].Equals("off", StringComparison.OrdinalIgnoreCase))
		{
			Game.serv.debugvis.RemoveHeatmap();
			return "Heatmaps hidden";
		}
		if (!Enum.TryParse<HeatmapType>(arg[1], ignoreCase: true, out var result))
		{
			return "Unknown type: " + arg[1];
		}
		Label label = ((arg.Length > 2) ? new Label(arg[2]) : Label.NULL);
		Heatmap heatmap = Find(result, label);
		if (heatmap == null)
		{
			return $"Unknown map: {result} {label}";
		}
		Game.serv.debugvis.ShowHeatmap(heatmap);
		return $"Showing map: {result} {label}";
	}
}

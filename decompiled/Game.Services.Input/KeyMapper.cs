using System.Collections.Generic;
using UnityEngine;

namespace Game.Services.Input;

public class KeyMapper
{
	public static readonly List<KeyMappingTuple> DEFAULT_MAPPINGS = new List<KeyMappingTuple>
	{
		new KeyMappingTuple(KeyAction.FinishTurn, KeyCode.Space),
		new KeyMappingTuple(KeyAction.AdvanceToNextCrew, KeyCode.Tab),
		new KeyMappingTuple(KeyAction.Cancel, KeyCode.Escape),
		new KeyMappingTuple(KeyAction.HideUI, KeyCode.Backslash),
		new KeyMappingTuple(KeyAction.CheatConsole, KeyCode.Slash),
		new KeyMappingTuple(KeyAction.Quicksave, KeyCode.T),
		new KeyMappingTuple(KeyAction.CameraLeft, KeyCode.A),
		new KeyMappingTuple(KeyAction.CameraRight, KeyCode.D),
		new KeyMappingTuple(KeyAction.CameraUp, KeyCode.W),
		new KeyMappingTuple(KeyAction.CameraDown, KeyCode.S),
		new KeyMappingTuple(KeyAction.CameraZoomOut, KeyCode.R),
		new KeyMappingTuple(KeyAction.CameraZoomIn, KeyCode.F),
		new KeyMappingTuple(KeyAction.CameraTurnLeft, KeyCode.E),
		new KeyMappingTuple(KeyAction.CameraTurnRight, KeyCode.Q),
		new KeyMappingTuple(KeyAction.Select1, KeyCode.Alpha1),
		new KeyMappingTuple(KeyAction.Select2, KeyCode.Alpha2),
		new KeyMappingTuple(KeyAction.Select3, KeyCode.Alpha3),
		new KeyMappingTuple(KeyAction.Select4, KeyCode.Alpha4),
		new KeyMappingTuple(KeyAction.Select5, KeyCode.Alpha5),
		new KeyMappingTuple(KeyAction.Select6, KeyCode.Alpha6),
		new KeyMappingTuple(KeyAction.Select7, KeyCode.Alpha7),
		new KeyMappingTuple(KeyAction.Select8, KeyCode.Alpha8),
		new KeyMappingTuple(KeyAction.Select9, KeyCode.Alpha9),
		new KeyMappingTuple(KeyAction.Select0, KeyCode.Alpha0),
		new KeyMappingTuple(KeyAction.BuildingSelect1, KeyCode.F1),
		new KeyMappingTuple(KeyAction.BuildingSelect2, KeyCode.F2),
		new KeyMappingTuple(KeyAction.BuildingSelect3, KeyCode.F3),
		new KeyMappingTuple(KeyAction.BuildingSelect4, KeyCode.F4),
		new KeyMappingTuple(KeyAction.BuildingSelect5, KeyCode.F5),
		new KeyMappingTuple(KeyAction.BuildingSelect6, KeyCode.F6),
		new KeyMappingTuple(KeyAction.BuildingSelect7, KeyCode.F7),
		new KeyMappingTuple(KeyAction.BuildingSelect8, KeyCode.F8),
		new KeyMappingTuple(KeyAction.BuildingSelect9, KeyCode.F9),
		new KeyMappingTuple(KeyAction.BuildingSelect10, KeyCode.F10),
		new KeyMappingTuple(KeyAction.OverlayRespect, KeyCode.C),
		new KeyMappingTuple(KeyAction.OverlayHeat, KeyCode.X),
		new KeyMappingTuple(KeyAction.OverlayResources, KeyCode.Z),
		new KeyMappingTuple(KeyAction.OpenOrgChart, KeyCode.B)
	};

	public List<KeyMappingTuple> mappings = new List<KeyMappingTuple>(DEFAULT_MAPPINGS);

	public KeyMapper Clone()
	{
		return new KeyMapper
		{
			mappings = new List<KeyMappingTuple>(mappings)
		};
	}

	public void LoadFrom(KeyMapper other)
	{
		if (other.mappings != null)
		{
			other.mappings.ForEach(UpdateMapping);
		}
	}

	public int FindMappingIndex(KeyAction action)
	{
		int i = 0;
		for (int count = mappings.Count; i < count; i++)
		{
			if (mappings[i].action == action)
			{
				return i;
			}
		}
		return -1;
	}

	public KeyMappingTuple? FindMapping(KeyAction action)
	{
		int num = FindMappingIndex(action);
		if (num >= 0 && num < mappings.Count)
		{
			return mappings[num];
		}
		return null;
	}

	public void UpdateMapping(KeyMappingTuple entry)
	{
		int num = FindMappingIndex(entry.action);
		if (num >= 0 && num < mappings.Count)
		{
			mappings[num] = entry;
		}
	}

	public bool IsSameAsDefaults()
	{
		if (DEFAULT_MAPPINGS.Count != mappings.Count)
		{
			return false;
		}
		int i = 0;
		for (int count = mappings.Count; i < count; i++)
		{
			if (!mappings[i].Equals(DEFAULT_MAPPINGS[i]))
			{
				return false;
			}
		}
		return true;
	}
}

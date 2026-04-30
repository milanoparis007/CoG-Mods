using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Assets;
using Game.UI.Session;
using SomaSim.SION;
using SomaSim.Util;

namespace Game.Session.Sim;

public class HintTracker : ISubManager<SimulationManager>, ISaveLoadProvider
{
	public sealed class HintTrackerPersistedData
	{
		public Xorshift rng = Game.ctx.scenario.MakeSeededRng<HintTracker>();

		public List<Label> shownThisSession = new List<Label>();

		public bool WasShown(Label id)
		{
			return shownThisSession.Contains(id);
		}

		public void MarkAsShown(Label id)
		{
			shownThisSession.Add(id);
		}
	}

	public static readonly Label EXPLAIN_CORNER_NEED = (Label)"explainCornerNeed";

	public static readonly Label EXPLAIN_CORNER_ONE = (Label)"explainCornerOne";

	public static readonly Label EXPLAIN_CORNER_TWO = (Label)"explainCornerTwo";

	public static readonly Label EXPLAIN_SKILL_ONE = (Label)"explainSkillOne";

	public static readonly Label EXPLAIN_SKILL_TWO = (Label)"explainSkillTwo";

	public static readonly Label EXPLAIN_CONSTRUCTION_ONE = (Label)"explainConstructionOne";

	public static readonly Label EXPLAIN_CONSTRUCTION_TWO = (Label)"explainConstructionTwo";

	public static readonly Label EXPLAIN_BUILDING_ONE = (Label)"explainBuildingOne";

	public static readonly Label EXPLAIN_BUILDING_TWO = (Label)"explainBuildingTwo";

	public static readonly Label EXPLAIN_BUILDING_LOST = (Label)"explainBuildingLost";

	public static readonly Label EXPLAIN_QUEST_ONE = (Label)"explainQuestOne";

	public static readonly Label EXPLAIN_QUEST_TWO = (Label)"explainQuestTwo";

	public static readonly Label EXPLAIN_CREW_ONE = (Label)"explainCrewOne";

	public static readonly Label EXPLAIN_CREW_TWO = (Label)"explainCrewTwo";

	public static readonly Label EXPLAIN_VEHICLE_ONE = (Label)"explainVehicleOne";

	public static readonly Label EXPLAIN_BUYOUT_ONE = (Label)"explainBuyoutOne";

	public HintTrackerPersistedData data;

	public void Initialize(SimulationManager manager)
	{
		data = new HintTrackerPersistedData();
	}

	public void Release()
	{
		data = null;
	}

	public void ShowOneTimeHintPhoto(Label id)
	{
		if (!data.WasShown(id))
		{
			ShowHintPhoto(SFXType.None, id);
			data.MarkAsShown(id);
		}
	}

	public void ShowHintPhoto(SFXType sfx, Label id)
	{
		List<PhotoConfig> list = Game.serv.globals.ui.photos.hints.FindOrNull(id);
		if (list != null)
		{
			Game.serv.ui.AddPopup(new PhotoPopup(list, sfx, null));
		}
	}

	public void ShowHintPhotoRandom(SFXType sfx, params Label[] ids)
	{
		ShowHintPhoto(sfx, data.rng.PickElement(ids));
	}

	public void ShowCornerHint()
	{
		ShowHintPhotoRandom(SFXType.None, EXPLAIN_CORNER_ONE, EXPLAIN_CORNER_TWO);
	}

	public void ShowConstructionHint()
	{
		ShowHintPhotoRandom(SFXType.EventConstructionComplete, EXPLAIN_CONSTRUCTION_ONE, EXPLAIN_CONSTRUCTION_TWO);
	}

	public void ShowSkillHint()
	{
		ShowHintPhotoRandom(SFXType.EventSkillLearned, EXPLAIN_SKILL_ONE, EXPLAIN_SKILL_TWO);
	}

	public void ShowBuildingGainedHint()
	{
		ShowHintPhotoRandom(SFXType.EventNewBuilding, EXPLAIN_BUILDING_ONE, EXPLAIN_BUILDING_TWO);
	}

	public void ShowBuildingLostHint()
	{
		ShowHintPhotoRandom(SFXType.EventTerritoryContracted, EXPLAIN_BUILDING_LOST);
	}

	public void ShowQuestHint()
	{
		ShowHintPhotoRandom(SFXType.EventQuestComplete, EXPLAIN_QUEST_ONE, EXPLAIN_QUEST_TWO);
	}

	public void ShowCrewHint()
	{
		ShowHintPhotoRandom(SFXType.EventNewCrew, EXPLAIN_CREW_ONE, EXPLAIN_CREW_TWO);
	}

	public void ShowVehicleHint()
	{
		ShowHintPhotoRandom(SFXType.EventNewCar, EXPLAIN_VEHICLE_ONE);
	}

	public void ShowBuyoutHint()
	{
		ShowHintPhotoRandom(SFXType.EventNewCrew, EXPLAIN_BUYOUT_ONE);
	}

	public void Save(Serializer s, ConcurrentSaveTable results)
	{
		results.Set("data", s.Serialize(data));
	}

	public IEnumerator Load(Hashtable data)
	{
		SaveLoadUtils.DeserializeSingleKey(data, "data", delegate(HintTrackerPersistedData result)
		{
			this.data = result;
		});
		yield break;
	}
}

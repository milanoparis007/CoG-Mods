using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using Game.UI.Session;
using Game.UI.Session.Picks;
using Game.UI.Session.Tutorial;
using SomaSim.Util;

namespace Game.Session.Tutorial;

public abstract class BaseLesson
{
	public abstract int LessonNumber { get; }

	public abstract int StepsCount { get; }

	public abstract bool InhibitsQuestRequests { get; }

	public PlayerInfo HumanPlayer => Game.ctx.players.Human;

	public string LocRoot => GetLocRoot(LessonNumber);

	protected TutorialManager Tutorial => Game.ctx.tutorial;

	public virtual bool CanRun()
	{
		return true;
	}

	public abstract IEnumerator Run();

	public static string GetLocRoot(int lesson)
	{
		return "tut.lesson-" + lesson.ToString(CultureInfo.InvariantCulture);
	}

	public static string GetLessonTitle(int lesson)
	{
		return Loc.Get(GetLocRoot(lesson) + ".title");
	}

	public string GetLessonTitle()
	{
		return Loc.Get(LocRoot + ".title");
	}

	public string GetStepKey(int step)
	{
		return LocRoot + "." + step.ToString(CultureInfo.InvariantCulture);
	}

	public string GetStepText(int step, string[] replacements = null)
	{
		return Loc.GetPluralized(LocRoot, step, replacements);
	}

	protected void ShowStepBlurb(int step, FrancineDialog.Anchor anchor, FrancineDialog.ButtonType type = FrancineDialog.ButtonType.Continue, string[] replacements = null)
	{
		string stepText = GetStepText(step, replacements);
		Tutorial.ShowLesson(this, step, stepText, null, anchor, type);
	}

	protected void ShowStepBlurbGendered(int step, Entity peep, FrancineDialog.Anchor anchor = FrancineDialog.Anchor.BCenter, FrancineDialog.ButtonType type = FrancineDialog.ButtonType.Continue, string[] replacements = null)
	{
		Gender g = peep.data.person.g;
		string gendered = Loc.GetGendered(GetStepKey(step), g, replacements);
		Tutorial.ShowLesson(this, step, gendered, null, anchor, type);
	}

	protected void HighlightBuildingPickFor(Entity building)
	{
		BasePick orNull = Game.ctx.hud.picks.GetContainer(PickType.BuildingPick).GetOrNull(building);
		if (orNull != null)
		{
			orNull.PushToFront();
			Tutorial.SetHighlight(orNull.go.GetChild("Button/BG Image"));
		}
	}

	protected bool GotNext()
	{
		return !Game.ctx.hud.francine.IsShowing;
	}

	protected Entity GetPlayerPeep()
	{
		return HumanPlayer.social.GetPlayerPeep();
	}

	protected CrewAssignment GetPlayerPeepCrew()
	{
		return HumanPlayer.crew.GetCrewForPlayerPeep();
	}

	protected Entity GetPlayerPeepVehicle()
	{
		return GetPlayerPeepCrew().GetVehicle();
	}

	protected Entity GetPlayerSafehouse()
	{
		return HumanPlayer.territory.Safehouse.FindEntity();
	}

	protected Node GetPlayerPeepCorner()
	{
		return GetPlayerPeepCrew().GetPeep().components.agent.GetNode();
	}

	protected Node GetPlayerSafehouseCorner()
	{
		return GetPlayerSafehouse().components.board.GetNode();
	}

	protected Fixnum GetBeerQtyInCar()
	{
		return GetBeerQtyIn(GetPlayerPeepVehicle());
	}

	protected Fixnum GetBeerQtyIn(Entity container)
	{
		return ModulesUtil.GetInventory(container).data.Get(Tutorial.TutorialResource).qty;
	}

	protected void EnsurePlayerPeepHas(Money amt)
	{
		Fixnum fixnum = amt.cash - HumanPlayer.finances.GetMoneyTotal().cash;
		if (fixnum > 0)
		{
			HumanPlayer.finances.DoChangeMoneyOnPlayerPeep(new Price(fixnum), MoneyReason.Other);
		}
	}

	protected void EnsureInventoryHas(Entity location, List<ResourceAndQty> resources)
	{
		InventoryModuleData inventoryModuleData = ModulesUtil.GetInventory(location)?.data;
		foreach (ResourceAndQty resource in resources)
		{
			Fixnum fixnum = resource.qty - inventoryModuleData.Get(resource.id).qty;
			if (fixnum > 0)
			{
				inventoryModuleData.Increment(new ResourceAndQty(resource.id, fixnum));
			}
		}
	}

	protected void AddToInventory(Entity location, ResourceAndQty resources)
	{
		(ModulesUtil.GetInventory(location)?.data).Increment(resources);
	}

	protected void ForceStartQuestWithReward(string id)
	{
		EntityID id2 = Tutorial.Context.Francine.Id;
		QuestDefinition questDefinition = Game.ctx.quests.FindQuestDefinition(id);
		Game.ctx.quests.Requests.OnGrantStartingARequest(id2, questDefinition);
		Game.ctx.quests.StartQuest(questDefinition.id, id2, fromRequest: true);
	}

	protected bool IsPlayerVehSelected()
	{
		return Game.ctx.selection.CurrentActive == GetPlayerPeepVehicle();
	}

	protected bool IsPlayerSafehouseSelected()
	{
		return Game.ctx.selection.CurrentActive == GetPlayerSafehouse();
	}

	protected int GetConvoDialogHistoryCount()
	{
		return (Game.ctx.hud.convoDialog?.Model?.shared?.blurbs?.Count).GetValueOrDefault();
	}

	protected void TweenCameraTo(Entity entity, HUDUtil.ZoomInLevel zoomIn = HUDUtil.ZoomInLevel.Full, bool resetPitch = false, float? angle = null)
	{
		TweenCameraTo(BoardUtil.FindBoardPositionFor(entity).Value, zoomIn, resetPitch, angle);
	}

	protected void TweenCameraTo(Node node, HUDUtil.ZoomInLevel zoomIn = HUDUtil.ZoomInLevel.Full, bool resetPitch = false)
	{
		TweenCameraTo(node.pos, zoomIn, resetPitch, null);
	}

	private static void TweenCameraTo(WorldPos pos, HUDUtil.ZoomInLevel zoomIn, bool resetPitch, float? angle)
	{
		HUDUtil.GoTo(pos, zoomIn, showFx: true);
		if (resetPitch)
		{
			Game.serv.camera.SetPitch(Game.serv.camera.Settings.startXPitch + 5f, CameraTween.SLOW_FOCUS_TWEEN_NOINT);
		}
		if (angle.HasValue)
		{
			Game.serv.camera.SetRotation(angle.Value, CameraTween.SLOW_FOCUS_TWEEN_NOINT);
		}
	}

	public virtual void OnSkip()
	{
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim.Modules;
using Game.UI.Session.Crew;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.OwnedBiz;

public static class ModuleToggleUtils
{
	private const string CORNER_SPRITE = "Corner Pick Image";

	private const string ADD_CREW_SPRITE = "Crew Human";

	private const string CIVIC_SPIRTE = "Politics Image";

	public const string TOGGLE_GROUP_BUTTONS = "Buttons";

	public const string TOGGLE_BUTTON_CARD = "Toggle/Card";

	public const string TOGGLE_BUTTON_ICON = "Toggle/Image";

	public const string HEAL_BUTTON_OVERLAY = "HealthOverlay";

	public static void SetModuleToggleSprites(GameObject toggle, Sprite cardOrNull, Sprite iconOrNull)
	{
		toggle.SetImageOrHide("Toggle/Card", cardOrNull);
		toggle.SetImageOrHide("Toggle/Image", iconOrNull);
	}

	public static GameObject MakeManagerToggle(Transform modules, GameObject tmplButton, VisitState visit, bool selected)
	{
		Entity entity = BuildingUtil.FindOwnerOrManagerForAnyBuilding(visit.building);
		if (entity == null)
		{
			Sprite iconSprite = Game.ctx.hud.uisprites.uiatlas.Find("Crew Human");
			GameObject gameObject = InitializeModuleToggle(modules, tmplButton, selected: true, null, iconSprite, delegate
			{
				Game.ctx.selection.ClearActive();
				Game.serv.ui.AddPopup<CrewManagementPopup>();
			});
			if (selected)
			{
				Toggle componentInChildren = gameObject.GetComponentInChildren<Toggle>();
				componentInChildren.SetIsOnWithoutNotify(value: true);
				componentInChildren.onValueChanged.RemoveAllListeners();
			}
			gameObject.GetOrAddComponent<ModuleToggleContext>().SetLocKey("ui.crewinfo.nomanager.desc");
			return gameObject;
		}
		Sprite crewSprite = HUDUtil.GetCrewSprite(entity);
		GameObject gameObject2 = InitializeModuleToggle(modules, tmplButton, selected: true, crewSprite, null, delegate
		{
			ResidenceComponent.StartCasinoConversation(visit, fromGamblingDialog: true);
		});
		if (selected)
		{
			Toggle componentInChildren2 = gameObject2.GetComponentInChildren<Toggle>();
			componentInChildren2.SetIsOnWithoutNotify(value: true);
			componentInChildren2.onValueChanged.RemoveAllListeners();
		}
		VisitState owner = new VisitState(visit.crew, entity, Game.ctx.clock.Now, PlayerID.HumanPlayer);
		gameObject2.GetOrAddComponent<ModuleToggleContext>().SetOwner(owner);
		return gameObject2;
	}

	public static GameObject MakeOwnerToggle(Transform modules, GameObject tmplButton, VisitState visit, bool selected)
	{
		Sprite crewSprite = HUDUtil.GetCrewSprite(visit.npc ?? visit.peep);
		GameObject gameObject = InitializeModuleToggle(modules, tmplButton, selected: true, crewSprite, null, delegate
		{
			BizComponent.StartConversation(visit, fromBizDialog: true);
		});
		if (selected)
		{
			Toggle componentInChildren = gameObject.GetComponentInChildren<Toggle>();
			componentInChildren.SetIsOnWithoutNotify(value: true);
			componentInChildren.onValueChanged.RemoveAllListeners();
		}
		GameObject child = gameObject.GetChild("HealthOverlay");
		if (visit.building != null && visit.building.data.building.controlled.pid.IsHumanPlayer && Game.ctx.players.Human.kb.HealShouldPrompt(visit))
		{
			child.SetActive(value: true);
		}
		gameObject.GetOrAddComponent<ModuleToggleContext>().SetOwner(visit);
		return gameObject;
	}

	public static GameObject MakeCornerToggle(Transform container, GameObject tmplButton, VisitState visit)
	{
		Sprite cardSprite = Game.ctx.hud.uisprites.uiatlas.Find("Corner Pick Image");
		GameObject gameObject = InitializeModuleToggle(container, tmplButton, selected: false, cardSprite, null, delegate
		{
			Game.ctx.hud.cornerInfo.Show(visit.crew, visit.GetBldgNode());
		});
		gameObject.GetOrAddComponent<ModuleToggleContext>().SetCorner(visit);
		gameObject.GetChild("HealthOverlay").SetActive(value: false);
		return gameObject;
	}

	public static GameObject MakePoliticianToggle(Transform container, GameObject tmplButton, VisitState visit, bool selected)
	{
		Sprite crewSprite = HUDUtil.GetCrewSprite(visit.building.data.civic.npc.FindEntity());
		GameObject gameObject = InitializeModuleToggle(container, tmplButton, selected, crewSprite, null, delegate
		{
			CivicComponent.StartConversation(visit.building, visit.crew, fromPolDialog: true);
		});
		if (selected)
		{
			Toggle componentInChildren = gameObject.GetComponentInChildren<Toggle>();
			componentInChildren.SetIsOnWithoutNotify(value: true);
			componentInChildren.onValueChanged.RemoveAllListeners();
		}
		gameObject.GetOrAddComponent<ModuleToggleContext>().SetOwner(visit);
		return gameObject;
	}

	public static GameObject MakePoliticsToggle(Transform container, GameObject tmplButton, VisitState visit, bool selected)
	{
		Sprite cardSprite = Game.ctx.hud.uisprites.uiatlas.Find("Politics Image");
		GameObject gameObject = InitializeModuleToggle(container, tmplButton, selected, cardSprite, null, delegate
		{
			Game.ctx.hud.politicsDialog.Show(visit);
		});
		if (selected)
		{
			Toggle componentInChildren = gameObject.GetComponentInChildren<Toggle>();
			componentInChildren.SetIsOnWithoutNotify(value: true);
			componentInChildren.onValueChanged.RemoveAllListeners();
		}
		gameObject.GetOrAddComponent<ModuleToggleContext>().mouseover = Loc.GetWithPolUnit("ui.politics.module-button-text");
		return gameObject;
	}

	public static GameObject InitializeModuleToggle(Transform modules, GameObject tmplButton, bool selected, Sprite cardSprite, Sprite iconSprite, Action<GameObject> clickfn)
	{
		GameObject card = UnityEngine.Object.Instantiate(tmplButton, modules);
		card.GetOrAddComponent<ModuleToggleContext>().Reset();
		Toggle componentInChildren = card.GetComponentInChildren<Toggle>();
		if (selected)
		{
			componentInChildren.isOn = true;
		}
		ToggleGroup component = card.transform.parent.gameObject.GetComponent<ToggleGroup>();
		componentInChildren.interactable = true;
		componentInChildren.group = component;
		componentInChildren.onValueChanged.SetListener(delegate(bool isOn)
		{
			if (isOn)
			{
				clickfn(card);
			}
		});
		SetModuleToggleSprites(card, cardSprite, iconSprite);
		return card;
	}

	public static void InitializeModuleToggleCard(int i, GameObject card, VisitState visit, Action<GameObject> clickfn, bool includeInventory = true)
	{
		List<IModule> list = visit.building.components.modules.GetAllSlotsUnsafe();
		if (!includeInventory)
		{
			list = list.Where((IModule x) => !(x is InventoryModule)).ToList();
		}
		ModuleToggleContext orAddComponent = card.GetOrAddComponent<ModuleToggleContext>();
		orAddComponent.Reset();
		Toggle componentInChildren = card.GetComponentInChildren<Toggle>();
		ToggleGroup component = card.transform.parent.gameObject.GetComponent<ToggleGroup>();
		componentInChildren.interactable = true;
		componentInChildren.group = component;
		componentInChildren.onValueChanged.SetListener(delegate(bool isOn)
		{
			if (isOn)
			{
				clickfn(card);
			}
		});
		List<ModuleSlot> slots = visit.building.config.modules.slots;
		ModuleSlot moduleSlot = slots[i];
		IModule module = list[i];
		orAddComponent.SetModule(i, slots, list);
		bool flag = module == null && moduleSlot.hidden;
		card.SetActive(!flag);
		card.GetChild("HealthOverlay").SetActive(value: false);
		if (!flag)
		{
			SetModuleToggleSprites(card, null, ModulesUIUtil.FindModuleIcon(orAddComponent.common));
		}
	}
}

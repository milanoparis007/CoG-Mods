using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim.Modules;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.Crew;

public class EntitySelectionPopup : BasePopup
{
	public class EntityDescription
	{
		public string message;

		public string description;

		public Sprite sprite;

		public string icon;

		public string buttonTextOverride;

		public bool interactable = true;
	}

	private const string CARD_TEMPLATE = "Templates/Entity Selection Card";

	private const string CONTAINER = "Panel/Scroll View List/Viewport/Content";

	private const string CLOSE = "Panel/Close Button";

	private const string TASK = "Panel/Task";

	private List<EntityID> _eids;

	private EntityID _selected;

	private Action<EntityID> _onSelect;

	private Action<EntityID> _onCancel;

	private Func<Entity, EntityDescription> _descriptor;

	private string _message;

	private const string CARD_IMAGE = "Image";

	private const string CARD_DESCRIPTION = "Description";

	private const string CARD_NAME = "Name";

	private const string CARD_ICON = "Icon";

	private const string CARD_BUTTON = "Button";

	private const string CARD_BUTTON_TEXT = "Button/Text";

	public override UIReference UIReference => UIElements.EntitySelectionPopup;

	public EntitySelectionPopup(List<EntityID> eids, Func<Entity, EntityDescription> descriptor, string message, Action<EntityID> onSelect, Action<EntityID> onCancel)
	{
		_eids = eids;
		_descriptor = descriptor;
		_message = message;
		_onSelect = onSelect;
		_onCancel = onCancel;
		_selected = EntityID.INVALID;
	}

	protected override void InitializeOnPush()
	{
		_go.GetButton("Panel/Close Button").onClick.SetListener(Close);
		_go.GetText("Panel/Task").text = _message;
		_selected = EntityID.INVALID;
		GameObject child = _go.GetChild("Templates/Entity Selection Card");
		GameObject child2 = _go.GetChild("Panel/Scroll View List/Viewport/Content");
		child2.EnsureChildCount(_eids, child);
		child2.InitializeChildren(_eids, MakeCrewCard);
	}

	protected override void ReleaseOnPop()
	{
		ProcessCallbacksAfterClose(_selected, _onSelect, _onCancel);
		_eids = null;
		_descriptor = null;
		_message = null;
		_onSelect = (_onCancel = null);
		_selected = EntityID.INVALID;
		_go.GetChild("Panel/Scroll View List/Viewport/Content").DestroyAllChildren();
		_go.ClearButtonListeners("Panel/Close Button");
	}

	private static void ProcessCallbacksAfterClose(EntityID selected, Action<EntityID> onSelect, Action<EntityID> onCancel)
	{
		TimerUtil.RunNextFrame(delegate
		{
			if (selected.IsValid)
			{
				onSelect?.Invoke(selected);
			}
			else
			{
				onCancel?.Invoke(selected);
			}
		});
	}

	private void MakeCrewCard(int i, GameObject card, EntityID eid)
	{
		Entity arg = eid.FindEntity();
		EntityDescription entityDescription = _descriptor(arg);
		card.SetText("Name", entityDescription.message);
		card.SetActive("Description", entityDescription.description != null);
		if (entityDescription.description != null)
		{
			card.SetText("Description", entityDescription.description);
		}
		card.SetImageOrHide("Image", entityDescription.sprite);
		card.SetTextOrHide("Icon", entityDescription.icon);
		card.SetText("Button/Text", entityDescription.buttonTextOverride ?? Loc.Get("ui.entityselection.card.select"));
		Button button = card.GetButton("Button");
		button.interactable = entityDescription.interactable;
		button.onClick.SetListener(delegate
		{
			_selected = eid;
			Close();
		});
	}

	private static EntityDescription PeepDescriptor(Entity peep)
	{
		return new EntityDescription
		{
			message = peep.data.person.FullName,
			sprite = HUDUtil.GetCrewSprite(peep)
		};
	}

	private static EntityDescription VehicleDescriptor(Entity vehicle)
	{
		return new EntityDescription
		{
			message = Loc.Get(vehicle.config.mobile.locname),
			sprite = HUDUtil.GetVehicleSprite(vehicle)
		};
	}

	private static EntityDescription BuildingDescriptor(Entity building)
	{
		BuildingUtil.FindBizForBuilding(building);
		return new EntityDescription
		{
			message = BuildingUtil.FindBuildingName(building),
			icon = (building.components.modules.FindBackroomModule()?.ModuleConfig.Common.display.GetTextIcon() ?? BuildingUtil.FindBuildingIcon(building))
		};
	}

	private static EntityDescription BuildingDescriptorWithCapacity(Entity building)
	{
		BuildingUtil.FindBizForBuilding(building);
		string text = Loc.Volume(building.components.modules.inventory.config.capacity);
		string text2 = Loc.Percentage(building.components.modules.inventory.GetUsedCapacityAsPercent());
		EntityDescription entityDescription = new EntityDescription();
		entityDescription.message = BuildingUtil.FindBuildingName(building);
		entityDescription.description = Loc.Get("ui.viewinventory.capacity-simple", "capacity", text, "percent", text2);
		entityDescription.icon = building.components.modules.FindBackroomModule()?.ModuleConfig.Common.display.GetTextIcon() ?? BuildingUtil.FindBuildingIcon(building);
		return entityDescription;
	}

	private static string GetBuildingIcon(Entity building)
	{
		IModule module = building.components.modules.FindBackroomModule();
		if (module != null && module.ModuleConfig.Common.display.locicon != null)
		{
			return module.ModuleConfig.Common.display.GetTextIcon();
		}
		return BuildingUtil.FindBuildingIcon(building);
	}

	public static void ShowCrewSelector(NodeID nid, string message, Action<EntityID> onSelect, Action<EntityID> onCancel = null)
	{
		if (onSelect != null && !nid.IsNotValid)
		{
			ShowCrewSelector(Game.ctx.players.Human.crew.FindAllDriversAtNode(nid).SelectIntoNewList((CrewAssignment crew) => crew.peepId), message, onSelect, onCancel);
		}
	}

	public static void ShowCrewSelector(List<EntityID> peeps, string message, Action<EntityID> onSelect, Action<EntityID> onCancel = null, bool shortcut = true)
	{
		ShowEIDSelector(peeps, PeepDescriptor, message, onSelect, onCancel, shortcut);
	}

	public static void ShowVehicleSelector(List<EntityID> vehicles, string message, Action<EntityID> callback, Action<EntityID> onCancel = null)
	{
		ShowEIDSelector(vehicles, VehicleDescriptor, message, callback, onCancel);
	}

	public static void ShowBuildingSelector(List<EntityID> vehicles, string message, Action<EntityID> callback, Action<EntityID> onCancel = null)
	{
		ShowEIDSelector(vehicles, BuildingDescriptor, message, callback, onCancel);
	}

	public static void ShowBuildingSelectorWithCapacity(List<EntityID> vehicles, string message, Action<EntityID> callback, Action<EntityID> onCancel = null)
	{
		ShowEIDSelector(vehicles, BuildingDescriptorWithCapacity, message, callback, onCancel);
	}

	public static void ShowSelectorWithPassInDescriptor(List<EntityID> items, string message, Func<Entity, EntityDescription> descriptor, Action<EntityID> callback, Action<EntityID> onCancel = null)
	{
		ShowEIDSelector(items, descriptor, message, callback, onCancel);
	}

	private static void ShowEIDSelector(List<EntityID> list, Func<Entity, EntityDescription> descriptor, string message, Action<EntityID> onSelect, Action<EntityID> onCancel = null, bool shortcut = true)
	{
		if (list.Count < 2 && shortcut)
		{
			onSelect(list.FirstOrDefaultFast());
		}
		else
		{
			Game.serv.ui.AddPopup(new EntitySelectionPopup(list, descriptor, message, onSelect, onCancel));
		}
	}
}

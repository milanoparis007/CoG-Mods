using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.UI.Mouseovers;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.Crew;

public sealed class CrewManagementPopup : BasePopup
{
	private class CrewCardContext : MonoBehaviour
	{
		public int index;

		public Entry entry;

		public CrewCardContext Set(int index, Entry entry)
		{
			this.index = index;
			this.entry = entry;
			return this;
		}
	}

	private class Entry
	{
		public CrewAssignment crew = CrewAssignment.EMPTY;

		public Entity vehicle;

		public Entity building;

		public ButtonStatus status = new ButtonStatus();

		public bool IsAlive
		{
			get
			{
				if (crew.IsValid)
				{
					return crew.IsNotDead;
				}
				return false;
			}
		}

		public bool IsPeepUnassigned
		{
			get
			{
				if (IsAlive && vehicle == null)
				{
					return building == null;
				}
				return false;
			}
		}

		public bool IsPeepInVehicle
		{
			get
			{
				if (IsAlive)
				{
					return vehicle != null;
				}
				return false;
			}
		}

		public bool IsPeepInBuilding
		{
			get
			{
				if (IsAlive)
				{
					return building != null;
				}
				return false;
			}
		}

		public Entry(CrewAssignment c)
		{
			crew = c;
			if (c.IsValid && c.IsInVehicle)
			{
				vehicle = c.GetVehicle();
			}
			if (c.IsValid && c.IsInBuilding)
			{
				building = c.GetBuilding();
			}
			status.Update(this);
		}

		public bool ParticipatedInCombatThisTurn()
		{
			if (IsPeepInVehicle)
			{
				return crew.GetPeep().components.agent.LastInjuryHappenedThisTurn();
			}
			return false;
		}

		public (string icon, string name) GetBizIconAndName()
		{
			IModule module = building.components.modules.FindBackroomModule();
			string text = "";
			text = ((module == null || module.ModuleConfig.Common.display.locicon == null) ? BuildingUtil.FindBuildingIcon(building) : module.ModuleConfig.Common.display.GetTextIcon());
			string item = BuildingUtil.FindBuildingName(building);
			return (icon: text, name: item);
		}

		public string GetVehicleName()
		{
			return vehicle.config.mobile.GetName();
		}
	}

	private class ButtonStatus
	{
		public bool isInBuilding;

		public bool isInVehicle;

		public bool canShowButtonsBuilding;

		public bool canAddToBuilding;

		public bool canRemoveFromBuilding;

		public bool buildingFailIsBoss;

		public bool buildingFailNoneLeft;

		public bool canShowButtonsVehicle;

		public bool canAddToVehicle;

		public bool canShowRemoveFromVehicle;

		public bool canPressRemoveFromVehicle;

		public bool vehicleFailNoneLeft;

		private static PlayerInfo Human => Game.ctx.players.Human;

		public void Update(Entry e)
		{
			isInBuilding = e.IsPeepInBuilding;
			isInVehicle = e.IsPeepInVehicle;
			canShowButtonsBuilding = CanShowBuildingButtons(e);
			canShowButtonsVehicle = CanShowVehicleButtons(e);
			(bool, bool, bool) tuple = CanBeAddedToBuilding(e);
			canAddToBuilding = tuple.Item1;
			buildingFailIsBoss = tuple.Item2;
			buildingFailNoneLeft = tuple.Item3;
			(bool, bool) tuple2 = CanBeAddedToVehicle(e);
			canAddToVehicle = tuple2.Item1;
			vehicleFailNoneLeft = tuple2.Item2;
			canRemoveFromBuilding = CanBeRemovedFromBuilding(e);
			canShowRemoveFromVehicle = CanBeRemovedFromVehicle(e);
			canPressRemoveFromVehicle = canShowRemoveFromVehicle && !e.ParticipatedInCombatThisTurn();
		}

		private static bool CanShowBuildingButtons(Entry e)
		{
			if (e.IsAlive)
			{
				if (!e.IsPeepUnassigned)
				{
					return e.IsPeepInBuilding;
				}
				return true;
			}
			return false;
		}

		private static bool CanShowVehicleButtons(Entry e)
		{
			if (e.IsAlive && (e.IsPeepUnassigned || e.IsPeepInVehicle))
			{
				return !Game.ctx.players.Human.schemes.IsInScheme(e.crew.GetPeep());
			}
			return false;
		}

		private static bool CanBeRemovedFromBuilding(Entry e)
		{
			if (CanShowBuildingButtons(e))
			{
				return e.IsPeepInBuilding;
			}
			return false;
		}

		private static bool CanBeRemovedFromVehicle(Entry e)
		{
			if (CanShowVehicleButtons(e))
			{
				return e.IsPeepInVehicle;
			}
			return false;
		}

		private static (bool canAdd, bool isBoss, bool noBuildings) CanBeAddedToBuilding(Entry e)
		{
			bool num = CanShowBuildingButtons(e);
			bool flag = !Human.crew.IsPlayerPeep(e.crew.peepId);
			bool flag2 = ModulesUtil.GetControlledBuildingsWithoutManagers(PlayerID.HumanPlayer).Any();
			return (canAdd: num && e.IsPeepUnassigned && flag && flag2 && !IsUnavailable(e), isBoss: !flag, noBuildings: !flag2);
		}

		private static (bool canAdd, bool noVehicles) CanBeAddedToVehicle(Entry e)
		{
			bool num = CanShowVehicleButtons(e);
			bool flag = Human.crew.AllUnassignedVehicles.Any();
			return (canAdd: num && e.IsPeepUnassigned && flag && !IsUnavailable(e), noVehicles: !flag);
		}
	}

	public class CrewMgmtCardMouseover : BaseCustomTextMouseover
	{
		private readonly bool _isBuilding;

		public CrewMgmtCardMouseover(bool isBldg)
		{
			_isBuilding = isBldg;
		}

		protected override string ProduceText()
		{
			CrewCardContext componentInObjectOrParents = context.GetComponentInObjectOrParents<CrewCardContext>();
			if (componentInObjectOrParents == null)
			{
				return null;
			}
			ButtonStatus buttonStatus = componentInObjectOrParents.entry?.status;
			if (buttonStatus == null)
			{
				return null;
			}
			string key = "";
			string text = "";
			if (_isBuilding)
			{
				key = (buttonStatus.isInBuilding ? "ui.crewmgmt.button-added-to.mo" : (buttonStatus.isInVehicle ? "ui.crewmgmt.button-already-added.mo" : (buttonStatus.canAddToBuilding ? "ui.crewmgmt.button-building-add.mo" : (buttonStatus.buildingFailIsBoss ? "ui.crewmgmt.button-buildings-boss.mo" : (buttonStatus.buildingFailNoneLeft ? "ui.crewmgmt.button-buildings-none.mo" : "ui.crewmgmt.button-add-disabled.mo")))));
				if (buttonStatus.isInBuilding)
				{
					text = componentInObjectOrParents.entry.GetBizIconAndName().name;
				}
			}
			if (!_isBuilding)
			{
				key = (buttonStatus.isInVehicle ? "ui.crewmgmt.button-added-to.mo" : (buttonStatus.isInBuilding ? "ui.crewmgmt.button-already-added.mo" : (buttonStatus.canAddToVehicle ? "ui.crewmgmt.button-vehicle-add.mo" : (buttonStatus.vehicleFailNoneLeft ? "ui.crewmgmt.button-vehicles-none.mo" : "ui.crewmgmt.button-add-disabled.mo"))));
				if (buttonStatus.isInVehicle)
				{
					text = componentInObjectOrParents.entry.GetVehicleName();
				}
			}
			return Loc.Get(key, "name", text);
		}
	}

	public const string TEMPLATE_CREW_CARD = "Templates/Crew Mgmt Card";

	public const string HEADER_TEXT = "Header/Text";

	public const string HEADER_CLOSE_BTN = "Header/Close";

	public const string INFO_TEXT = "Info/Viewport/Content/Text";

	public const string CREW_LIST_CONTAINER = "Crew List/Viewport/Content";

	private GameObject _tmplCard;

	private ToggleGroup _toggleGroup;

	private List<Entry> _entries = new List<Entry>();

	private Entry _selected;

	public const string CARD_PEEP_NAME = "Name";

	public const string CARD_DESCRIPTION = "Description";

	public const string CARD_PEEP_PORTRAIT = "Peep Image";

	public const string CARD_PEEP_PORTRAIT_SPRITE = "Peep Image/Portrait";

	public const string CARD_PEEP_PORTRAIT_BARS = "Peep Image/Bars";

	public const string CARD_PEEP_MASK = "Peep Image Mask";

	public const string CARD_PEEP_AUTOMATED = "Automation";

	public const string CARD_BTN_VHCL = "Vehicle Button";

	public const string CARD_BTN_BLDG = "Building Button";

	public const string CARD_BTN_X_IMG = "Image";

	public const string CARD_BTN_X_TXT = "Text";

	public const string CARD_BTN_REMOVE_VHCL = "Vehicle Remove";

	public const string CARD_BTN_REMOVE_BLDG = "Building Remove";

	public override UIReference UIReference => UIElements.CrewManagementPopup;

	protected override void InitializeOnPush()
	{
		_panel.SetText("Header/Text", Loc.Get("ui.crewinfo.popup.mgmt"));
		_panel.SetButtonListener("Header/Close", Close);
		_tmplCard = _go.GetChild("Templates/Crew Mgmt Card");
		_toggleGroup = _panel.GetChild("Crew List/Viewport/Content").GetComponent<ToggleGroup>();
		_selected = null;
		Game.serv.mouseovers.Register(MouseoverType.CrewMgmtBuildingButton, new CrewMgmtCardMouseover(isBldg: true));
		Game.serv.mouseovers.Register(MouseoverType.CrewMgmtVehicleButton, new CrewMgmtCardMouseover(isBldg: false));
	}

	protected override void ReleaseOnPop()
	{
		Game.serv.mouseovers.Unregister(MouseoverType.CrewMgmtBuildingButton);
		Game.serv.mouseovers.Unregister(MouseoverType.CrewMgmtVehicleButton);
		_toggleGroup = null;
		_tmplCard = null;
	}

	public override void OnActivated(bool pushed)
	{
		_toggleGroup.SetAllTogglesOff();
		base.OnActivated(pushed);
		RefreshCards(shutdown: false);
		RefreshInfoPanel();
	}

	public override void OnDeactivated(bool popped)
	{
		base.OnDeactivated(popped);
		RefreshCards(shutdown: true);
	}

	public override void OnPushed(object stack)
	{
		base.OnPushed(stack);
		Game.ctx.hud.crew.AddFreezeRequest(this);
	}

	public override void OnPopped()
	{
		base.OnPopped();
		Game.ctx.hud.crew.RemoveFreezeRequest(this);
	}

	private void RefreshCards(bool shutdown)
	{
		PlayerCrew crew = Game.ctx.players.Human.crew;
		_entries.Clear();
		if (!shutdown)
		{
			_entries.AddRange(crew.AllCrew.Select((CrewAssignment c) => new Entry(c)));
		}
		_toggleGroup.SetAllTogglesOff(sendCallback: false);
		_toggleGroup.enabled = false;
		GameObject child = _go.GetChild("Crew List/Viewport/Content");
		child.EnsureChildCount(_entries, _tmplCard);
		child.InitializeChildren(_entries, InitializeCrewCard);
		_toggleGroup.enabled = true;
		LayoutRebuilder.ForceRebuildLayoutImmediate(child.GetComponent<RectTransform>());
	}

	private void InitializeCrewCard(int i, GameObject card, Entry entry)
	{
		SetPeepInfo(card, entry);
		SetCardButtons(card, entry);
		SetCardDesc(card, entry);
		card.name = "CARD for " + entry.crew.ToString();
		card.SetActive("Peep Image Mask", entry.crew.IsDead);
		card.SetButtonListener("Building Button", delegate
		{
			AssignPeepToSomeBuilding(entry.crew.peepId);
		});
		card.SetButtonListener("Vehicle Button", delegate
		{
			AssignPeepToSomeVehicle(entry.crew.peepId);
		});
		card.SetButtonListener("Building Remove", delegate
		{
			RemovePeepFromBuilding(entry.crew.peepId);
		});
		card.SetButtonListener("Vehicle Remove", delegate
		{
			RemovePeepFromVehicle(entry.crew.peepId);
		});
		CrewCardContext ctx = card.GetOrAddComponent<CrewCardContext>().Set(i, entry);
		Toggle childToggle = card.GetChildToggle();
		childToggle.group = _toggleGroup;
		childToggle.isOn = false;
		childToggle.onValueChanged.SetListener(delegate(bool val)
		{
			OnToggleValueChanged(val, ctx);
		});
	}

	private static void SetCardButtons(GameObject card, Entry entry)
	{
		Color color = new Color(0.3f, 0.3f, 0.3f);
		string text = TextUtil.ColorWrap(Loc.Get("ui.crewmgmt.icon-building"), color);
		string text2 = TextUtil.ColorWrap(Loc.Get("ui.crewmgmt.icon-vehicle"), color);
		card.SetActive("Vehicle Remove", value: false);
		card.SetActive("Building Remove", value: false);
		card.SetActive("Vehicle Button", value: false);
		card.SetActive("Building Button", value: false);
		ButtonStatus status = entry.status;
		if (status.canAddToBuilding || status.canAddToVehicle)
		{
			ShowCardButton(card, "Building Button", status.canAddToBuilding, CrewInfoGen.GetPlusSignSprite(), text);
			ShowCardButton(card, "Vehicle Button", status.canAddToVehicle, CrewInfoGen.GetPlusSignSprite(), text2);
		}
		else if (entry.IsPeepInVehicle)
		{
			Sprite vehicleSprite = HUDUtil.GetVehicleSprite(entry.vehicle);
			ShowCardButton(card, "Vehicle Button", interactable: false, vehicleSprite);
			card.SetActive("Vehicle Remove", status.canShowRemoveFromVehicle);
			card.GetButton("Vehicle Remove").interactable = status.canPressRemoveFromVehicle;
			ShowCardButton(card, "Building Button", interactable: false, null, text);
		}
		else if (entry.IsPeepInBuilding)
		{
			bool canRemoveFromBuilding = status.canRemoveFromBuilding;
			string item = entry.GetBizIconAndName().icon;
			ShowCardButton(card, "Building Button", interactable: false, null, item);
			card.SetActive("Building Remove", canRemoveFromBuilding);
			ShowCardButton(card, "Vehicle Button", interactable: false, null, text2);
		}
		else if (entry.IsPeepUnassigned)
		{
			ShowCardButton(card, "Building Button", status.canAddToBuilding, null, text);
			ShowCardButton(card, "Vehicle Button", status.canAddToVehicle, null, text2);
		}
	}

	private static void SetCardDesc(GameObject card, Entry entry)
	{
		string text = "";
		if (entry.status.isInBuilding)
		{
			text = entry.GetBizIconAndName().name;
		}
		if (entry.status.isInVehicle)
		{
			text = entry.GetVehicleName();
		}
		card.SetText("Description", text);
	}

	private static void ShowCardButton(GameObject card, string path, bool interactable, Sprite sprite = null, string text = null)
	{
		GameObject child = card.GetChild(path);
		child.SetActive(value: true);
		child.SetImageOrHide("Image", sprite);
		child.SetTextOrHide("Text", text);
		child.GetChildButton().interactable = interactable;
	}

	private static void SetPeepInfo(GameObject card, Entry entry)
	{
		Entity peep = entry.crew.GetPeep();
		Sprite sprite = ((peep != null) ? HUDUtil.GetCrewSprite(peep) : null);
		card.SetImage("Peep Image/Portrait", sprite);
		card.SetActive("Peep Image", sprite != null);
		card.SetActive("Peep Image/Bars", IsArrested(entry));
		string text = ((peep != null) ? Game.ctx.players.Human.crew.GetCrewPeepName(entry.crew) : Loc.Get("ui.crewmgmt.panel-driver"));
		card.SetText("Name", text);
		bool value = Game.ctx.players.Human.automation.GetAutoOrNull(entry.crew)?.IsAutoActive ?? false;
		card.SetActive("Automation", value);
	}

	private void OnToggleValueChanged(bool isOn, CrewCardContext ctx)
	{
		_selected = (isOn ? ctx.entry : null);
		RefreshInfoPanel();
	}

	private void RefreshButton(string buttonName, bool show, string text, bool interactable)
	{
		Button button = _panel.GetButton(buttonName);
		button.gameObject.SetActive(show);
		if (show)
		{
			button.gameObject.SetChildText(TextUtil.ColorEnabledIf(interactable, text));
			button.interactable = interactable;
		}
	}

	private void RefreshInfoPanel()
	{
		string text = "";
		if (_selected == null)
		{
			text = GenerateAssignmentsDescription();
		}
		else if (_selected.IsPeepUnassigned && IsUnavailable(_selected))
		{
			text = (IsArrested(_selected) ? GetArrestedDesc(_selected.crew.peepId) : ((!Game.ctx.players.Human.schemes.IsInScheme(_selected.crew.peepId)) ? Loc.Get("ui.crewmgmt.offboard", "crewname", _selected.crew.peepId.FindEntity().data.person.FullName) : Loc.Get("ui.crewmgmt.scheming", "crewname", _selected.crew.peepId.FindEntity().data.person.FullName)));
		}
		else
		{
			CrewAssignment crew = _selected.crew;
			string shortName = crew.GetPeep().data.person.ShortName;
			switch (crew.type)
			{
			case CrewType.NotAssigned:
				text = Loc.Get("ui.crewmgmt.unassigned", "crewname", shortName);
				break;
			case CrewType.InVehicle:
				text = Loc.Get("ui.crewmgmt.invehicle", "crewname", shortName);
				break;
			case CrewType.InBuilding:
			{
				(string icon, string name) bizIconAndName = _selected.GetBizIconAndName();
				string item3 = bizIconAndName.icon;
				string item4 = bizIconAndName.name;
				text = Loc.Get("ui.crewmgmt.inbuilding", "crewname", shortName, "bizicon", item3, "bizname", item4);
				break;
			}
			case CrewType.Dead:
			{
				(bool paying, Price amount) supportPayments = Game.ctx.players.Human.crew.GetSupportPayments(crew.peepId);
				bool item = supportPayments.paying;
				Price item2 = supportPayments.amount;
				text = (item ? Loc.Get("ui.crewmgmt.deadpaid", "crewname", shortName, "amt", Loc.Price(item2.Abs)) : Loc.Get("ui.crewmgmt.deadother", "crewname", shortName));
				break;
			}
			default:
			{
				EntityID targetId = crew.targetId;
				Logger.Warning("Unknown type:" + targetId.ToString());
				break;
			}
			}
		}
		if (_selected?.vehicle != null)
		{
			text += Loc.Get("ui.crewinfo.vehicle.describe", "inventory", ModulesUtil.DescribeInventory(_selected.vehicle));
		}
		if (_selected != null)
		{
			text = Game.ctx.players.Human.crew.GetCrewPeepAndGroupName(_selected.crew) + "\n\n" + text;
		}
		_panel.SetText("Info/Viewport/Content/Text", text);
	}

	private string GenerateAssignmentsDescription()
	{
		StringBuilder stringBuilder = StringBuilderPool.AllocateInstance();
		PlayerInfo human = Game.ctx.players.Human;
		stringBuilder.AppendLine(Loc.Get("ui.crewmgmt.assignments"), 2);
		foreach (EntityID allVehicle in human.crew.AllVehicles)
		{
			EntityID id = human.crew.FindPeepAssignedToVehicle(allVehicle);
			bool isValid = id.IsValid;
			string name = allVehicle.FindEntity().config.mobile.GetName();
			stringBuilder.AppendLine(Loc.IconLine(isValid, name));
			string text = (isValid ? Loc.Get("ui.crewmgmt.driver", "name", id.FindEntity().data.person.ShortName) : Loc.Get("ui.crewmgmt.no-driver"));
			stringBuilder.AppendLine(Loc.IconLine("", "   " + text), 2);
		}
		foreach (EntityID item2 in human.territory.GetAllControlledBuildingsUnsafe())
		{
			Entity item = ModulesUtil.GetManagerOrNull(item2.FindEntity()).manager;
			bool flag = item != null;
			string message = BuildingUtil.FindBuildingName(item2);
			stringBuilder.AppendLine(Loc.IconLine(flag, message));
			string text2 = (flag ? Loc.Get("ui.crewmgmt.manager", "name", item.data.person.ShortName) : Loc.Get("ui.crewmgmt.no-manager"));
			stringBuilder.AppendLine(Loc.IconLine("", "   " + text2), 2);
		}
		return stringBuilder.ToStringAndReturnToPool();
	}

	private static bool IsUnavailable(Entry e)
	{
		if (e != null && e.crew.type == CrewType.NotAssigned)
		{
			if (!Game.ctx.players.Human.schemes.IsInScheme(e.crew.GetPeep()))
			{
				return !Game.ctx.players.Human.crew.IsOnBoard(e.crew.peepId);
			}
			return true;
		}
		return false;
	}

	private static bool IsArrested(Entry e)
	{
		if (e != null && e.crew.type == CrewType.NotAssigned)
		{
			return Game.ctx.simman.cops.IsArrestedOrImprisoned(e.crew.peepId);
		}
		return false;
	}

	private static string GetArrestedDesc(EntityID peepId)
	{
		CopTracker cops = Game.ctx.simman.cops;
		string shortName = peepId.FindEntity().data.person.ShortName;
		ArrestEntry arrestEntry = cops.FindArrestOrNull(peepId);
		if (arrestEntry != null)
		{
			return Loc.Get("ui.crewmgmt.arrested", "crewname", shortName, "date", Loc.FormatDateLong(arrestEntry.trialDate));
		}
		ImprisonedEntry imprisonedEntry = cops.FindImprisonedOrNull(peepId);
		if (imprisonedEntry != null)
		{
			return Loc.Get("ui.crewmgmt.imprisoned", "crewname", shortName, "date", Loc.FormatDateLong(imprisonedEntry.endDate));
		}
		return "";
	}

	private void AssignPeepToSomeVehicle(EntityID peepId)
	{
		List<EntityID> vehicles = Game.ctx.players.Human.crew.AllUnassignedVehicles.ToList();
		string message = Loc.Get("ui.crewmgmt.select-vehicle");
		EntitySelectionPopup.ShowVehicleSelector(vehicles, message, delegate(EntityID eid)
		{
			Game.ctx.players.Human.crew.AssignCrewToVehicle(peepId, eid);
			DeselectAndRefresh(peepId);
		});
	}

	private void RemovePeepFromVehicle(EntityID peepId)
	{
		Game.ctx.players.Human.crew.UnassignCrewFromVehicle(peepId);
		DeselectAndRefresh(peepId);
	}

	private void AssignPeepToSomeBuilding(EntityID peepId)
	{
		List<EntityID> vehicles = ModulesUtil.GetControlledBuildingsWithoutManagers(PlayerID.HumanPlayer).ToList();
		string message = Loc.Get("ui.crewmgmt.select-building");
		EntitySelectionPopup.ShowBuildingSelector(vehicles, message, delegate(EntityID buildingId)
		{
			Game.ctx.players.Human.crew.AssignCrewToBuilding(peepId, buildingId);
			DeselectAndRefresh(peepId);
		});
	}

	private void RemovePeepFromBuilding(EntityID peepId)
	{
		Game.ctx.players.Human.crew.UnassignCrewFromBuilding(peepId);
		DeselectAndRefresh(peepId);
	}

	private void DeselectAndRefresh(EntityID peepId)
	{
		RefreshCards(shutdown: false);
		SelectCardFor(peepId);
	}

	private void SelectCardFor(EntityID peep)
	{
		if (!peep.IsValid)
		{
			return;
		}
		foreach (Transform item in _go.GetChild("Crew List/Viewport/Content").transform)
		{
			CrewCardContext component = item.GetComponent<CrewCardContext>();
			if (component != null && component.entry != null && component.entry.crew.peepId == peep)
			{
				_selected = component.entry;
				item.gameObject.GetComponentInChildren<Toggle>().isOn = true;
				break;
			}
		}
		RefreshInfoPanel();
	}
}

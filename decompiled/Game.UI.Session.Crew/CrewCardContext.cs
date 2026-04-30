using System.Text;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.Commands;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.Crew;

public sealed class CrewCardContext : MonoBehaviour
{
	public sealed class Config
	{
		public ToggleGroup toggleGroup;

		public Config(ToggleGroup toggleGroup)
		{
			this.toggleGroup = toggleGroup;
		}
	}

	private const string CARD_TOGGLE = "Toggle";

	private const string CARD_INFO = "Info";

	private const string CARD_EXTRA = "Extras";

	private const string CARD_MASK = "Mask";

	private const string CARD_CHROME = "Selector";

	private const string CARD_EXTRA_PERSON_INFO = "Extras/Person";

	private const string CARD_EXTRA_CONTENTS = "Extras/Contents";

	private const string CARD_EXTRA_AUTOMATE = "Extras/Automate";

	private const string CARD_EXTRA_AUTOMATE_TEXT = "Extras/Automate/Text";

	private const string PANEL_TR_PEEP = "Info/Panel/Rows/Top/Buttons/Peep";

	private const string PANEL_TR_CORNER = "Info/Panel/Rows/Top/Buttons/Corner";

	private const string PANEL_TR_CORNER_BG = "Info/Panel/Rows/Top/Buttons/Corner/BG Sprite";

	private const string PANEL_TR_GOTO = "Info/Panel/Rows/Top/Buttons/Goto";

	private const string PANEL_TR_INSPECT = "Info/Panel/Rows/Top/Buttons/Inspect";

	private const string PANEL_TR_SCHEME = "Info/Panel/Rows/Top/Buttons/Stop Scheme";

	private const string PANEL_NAME = "Info/Panel/Rows/Top/Name";

	private const string PANEL_DESC = "Info/Panel/Rows/Bottom/Description";

	private const string PANEL_COMMANDS = "Info/Panel/Rows/Bottom/Commands";

	private const string PANEL_AUTOMATED = "Info/Panel/Automated";

	private const string PANEL_FIRST_BTN = "Info/Panel/First";

	private const string PANEL_SECOND_BTN = "Info/Panel/Second";

	private const string PANEL_FIRST_BAR = "Info/Panel/First Bar";

	private const string PANEL_FIRST_BAR_BAR = "Info/Panel/First Bar/Bar";

	private const string PANEL_SECOND_BAR = "Info/Panel/Second Bar";

	private const string PANEL_SECOND_BAR_BAR = "Info/Panel/Second Bar/Bar";

	private const float HEALTH_BAR_WIDTH = 32f;

	public CrewCardInfo data;

	public GameObject card;

	public Toggle toggle;

	public Config config;

	private const string EDIT_OVERLAY = "Edit Overlay";

	private const string EDIT_NAME = "Edit Overlay/Panel/Name";

	private const string EDIT_FIRST_PICTURE = "Edit Overlay/Panel/First";

	private const string EDIT_SECOND_PICTURE = "Edit Overlay/Panel/Second";

	private const string EDIT_PLACE_TOP = "Edit Overlay/Panel/Add To Top";

	private const string EDIT_PLACE_BOTTOM = "Edit Overlay/Panel/Add To Bottom";

	private const string EDIT_ITEM_ICON = "Edit Overlay/Panel/Sandwich Icon";

	private const string CHROME_PARENT = "Selector";

	private const string CHROME = "Selector/Chrome";

	public static CrewCardContext MakeCard(Config config, GameObject card)
	{
		CrewCardContext ctx = card.GetOrAddComponent<CrewCardContext>();
		ctx.card = card;
		ctx.toggle = card.GetChild<Toggle>("Toggle");
		ctx.config = config;
		ctx.toggle.group = config.toggleGroup;
		ctx.toggle.SetIsOnWithoutNotify(value: false);
		ctx.toggle.onValueChanged.SetListener(delegate(bool isOn)
		{
			Game.ctx.hud.crew.OnToggleValueChanged(isOn, ctx);
		});
		ctx.data = new CrewCardInfo(CrewCardType.Invalid);
		return ctx;
	}

	public void Reinitialize(CrewCardInfo data)
	{
		base.name = "CARD for " + data.type;
		this.data = data;
		card.SetButtonListener("Extras/Automate", OnAutomateClick);
		card.SetText("Extras/Automate/Text", Loc.Get("ui.crewinfo.delivery-manage"));
		bool flag = !data.crew.peepId.IsValid || data.crew.IsNotDead;
		card.GetChild("Mask").SetActive(!flag);
		card.GetChild("Toggle").SetActive(flag);
		RefreshCard();
	}

	private void OnAutomateClick()
	{
		Game.ctx.hud.deliveries.ShowFor(data.automation);
	}

	private void OnManagementClick()
	{
		Game.ctx.selection.ClearActive();
		if (data.type == CrewCardType.CrewJobs)
		{
			OnAutomateClick();
		}
		else
		{
			Game.serv.ui.AddPopup<CrewManagementPopup>();
		}
	}

	public void RefreshCard()
	{
		if (data.type != CrewCardType.Invalid)
		{
			bool flag = Game.ctx.hud.crew.sectionEditMode[data.type];
			ToggleExtra(toggle.isOn && !flag);
			RefreshPanel();
			RefreshTRButtons();
			RefreshCommands(forceRebuild: true);
			RefreshExtras();
			RefreshEditMode();
			EnableEthReplacements();
		}
	}

	internal void ToggleExtra(bool show)
	{
		card.SetActive("Extras", show);
		card.SetActive("Selector", show);
	}

	private void RefreshPanel()
	{
		card.SetText("Info/Panel/Rows/Top/Name", data.gen.GetTextLineTop());
		card.SetText("Info/Panel/Rows/Bottom/Description", data.gen.GetTextLineBottom());
		UpdatePortrait("Info/Panel/First", data.gen.GetFirstImage());
		UpdatePortrait("Info/Panel/Second", data.gen.GetSecondImage());
		bool flag = data.gen.ShowTLAutomated();
		card.GetChild("Info/Panel/Automated").SetActive(flag);
		if (flag)
		{
			if (((CrewInfoGenJob)data.gen).IsPaused())
			{
				card.SetTextOrHide("Info/Panel/Automated", Loc.Get("ui.command.cancel.icon"));
			}
			else
			{
				card.SetTextOrHide("Info/Panel/Automated", Loc.Get("levelup.management.icon"));
			}
		}
		UpdateHealthBar(data.gen.GetFirstImageHealth(), "Info/Panel/First Bar", "Info/Panel/First Bar/Bar");
		UpdateHealthBar(data.gen.GetSecondImageHealth(), "Info/Panel/Second Bar", "Info/Panel/Second Bar/Bar");
		void UpdateHealthBar(CrewInfoGen.HealthBarConfig config, string bar, string barbar)
		{
			card.SetActive(bar, config.show);
			if (config.show)
			{
				card.GetChild(barbar).SetUIElementWidth(config.p * 32f);
				card.GetImage(barbar).color = config.color;
			}
		}
	}

	private void UpdatePortrait(string child, CrewInfoGen.ButtonConfig config)
	{
		GameObject child2 = card.GetChild(child);
		CrewInfoGen.UpdateCrewButton(child2, config);
		child2.GetChildButton().onClick.SetListener(OnManagementClick);
	}

	private void RefreshTRButtons()
	{
		card.SetActive("Info/Panel/Rows/Top/Buttons/Peep", data.gen.ShowTRPeep());
		card.SetActive("Info/Panel/Rows/Top/Buttons/Corner", data.gen.ShowTRCorner());
		card.SetActive("Info/Panel/Rows/Top/Buttons/Goto", data.gen.ShowTRGoto());
		card.SetActive("Info/Panel/Rows/Top/Buttons/Inspect", data.gen.ShowTRInspect());
		card.SetActive("Info/Panel/Rows/Top/Buttons/Stop Scheme", data.gen.ShowTRStopScheme());
		card.SetActive("Extras/Automate", data.gen.ShowExtraAutomate());
		card.SetButtonListener("Info/Panel/Rows/Top/Buttons/Peep", delegate
		{
			data.gen.OnTRPeep();
		});
		card.SetButtonListener("Info/Panel/Rows/Top/Buttons/Corner", delegate
		{
			data.gen.OnTRCorner();
		});
		card.SetButtonListener("Info/Panel/Rows/Top/Buttons/Goto", delegate
		{
			data.gen.OnTRGoto();
		});
		card.SetButtonListener("Info/Panel/Rows/Top/Buttons/Inspect", delegate
		{
			data.gen.OnTRInspect();
		});
		card.SetButtonListener("Info/Panel/Rows/Top/Buttons/Stop Scheme", delegate
		{
			data.gen.OnTRStopScheme();
		});
		Image image = card.GetImage("Info/Panel/Rows/Top/Buttons/Corner/BG Sprite");
		if (data.gen.ShowTRCorner())
		{
			image.gameObject.SetActive(value: true);
			image.color = data.gen.GetTRColor();
		}
		else
		{
			image.gameObject.SetActive(value: false);
		}
	}

	private void RefreshExtras()
	{
		card.SetActive("Extras/Person", data.gen.ShowExtraPersonInfo());
		card.SetText("Extras/Person", data.gen.GetExtraPersonInfo());
		card.SetActive("Extras/Contents", data.gen.ShowExtraInventory());
		card.SetText("Extras/Contents", data.gen.GetExtraInventory());
	}

	private void RefreshEditMode()
	{
		CrewDialog crewdialog = Game.ctx.hud.crew;
		bool flag = crewdialog.IsCardEditSelected(this);
		bool active = Game.ctx.hud.crew.sectionEditMode[data.type] && !IsCardNewDelivery();
		card.GetButton("Edit Overlay").onClick.SetListener(delegate
		{
			crewdialog.ToggleEditSelected(this);
			crewdialog.RefreshCards();
		});
		card.GetButton("Edit Overlay").interactable = flag || (!flag && !crewdialog.IsMovingCard());
		card.GetImage("Edit Overlay").color = (flag ? new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue) : new Color32(170, 226, byte.MaxValue, byte.MaxValue));
		card.GetImage("Edit Overlay/Panel/Sandwich Icon").color = (flag ? new Color32(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue) : new Color32(170, 170, 170, byte.MaxValue));
		card.GetChild("Edit Overlay").SetActive(active);
		card.SetText("Edit Overlay/Panel/Name", data.gen.GetTextLineTop());
		card.GetButton("Edit Overlay/Panel/Add To Top").onClick.SetListener(delegate
		{
			crewdialog.Place(this, 0);
		});
		card.GetButton("Edit Overlay/Panel/Add To Bottom").onClick.SetListener(delegate
		{
			crewdialog.Place(this, 1);
		});
		card.GetChild("Edit Overlay/Panel/Add To Top").SetActive(!flag && crewdialog.IsMovingCard());
		card.GetChild("Edit Overlay/Panel/Add To Bottom").SetActive(!flag && crewdialog.IsMovingCard());
		CrewInfoGen.UpdateCrewButton(card.GetChild("Edit Overlay/Panel/First"), data.gen.GetFirstImage());
		CrewInfoGen.UpdateCrewButton(card.GetChild("Edit Overlay/Panel/Second"), data.gen.GetSecondImage());
		bool IsCardNewDelivery()
		{
			if (data.type == CrewCardType.CrewJobs)
			{
				return data.automation.id == AutomationID.INVALID.id;
			}
			return false;
		}
	}

	public void EnableEthReplacements()
	{
		GameObject ethChild = card.GetEthChild("Selector/Chrome", GetEthnicityString());
		card.GetChild("Selector").SetActiveOnlyOneChild(ethChild);
		static string GetEthnicityString()
		{
			if (!PlayerCrew.HasEthPackForCurrEth())
			{
				return "";
			}
			return Game.ctx.session.scenario.newgamepars.playerdetails.player.ethnicity.ToString().ToUpper();
		}
	}

	private void RefreshCommands(bool forceRebuild)
	{
		GameObject child = card.GetChild("Info/Panel/Rows/Bottom/Commands");
		bool isOn = toggle.isOn;
		bool activeSelf = child.activeSelf;
		if (isOn && (!activeSelf || forceRebuild))
		{
			child.SetActive(value: true);
			CommandButtonUtil.PopulateCommandButtons(data.crew, child, Game.ctx.hud.crew.CommandButtonTemplate);
		}
		if (!isOn && activeSelf)
		{
			child.SetActive(value: false);
			child.transform.DestroyAllChildren();
		}
	}

	internal string GeneratePointsMouseover(bool details)
	{
		StringBuilder sb = StringBuilderPool.AllocateInstance();
		Entity peep = data.crew.GetPeep();
		if (data.crew.IsInVehicle)
		{
			ExplainActionAndMovementPoints();
		}
		else
		{
			sb.AppendLine(data.gen.GetMouseoverHeader());
		}
		return sb.ToStringAndReturnToPool();
		void Explain(string label, int pts, Fixnum basevalue, string exp)
		{
			sb.AppendLine(Loc.Get("ui.crewinfo.mo-line", "points", pts, "label", label));
			if (basevalue != pts)
			{
				sb.AppendLine(exp);
			}
		}
		void ExplainActionAndMovementPoints()
		{
			if (peep?.components.agent != null)
			{
				int actionsRemaining = peep.components.agent.ActionsRemaining;
				int movesRemaining = peep.components.agent.MovesRemaining;
				string text = Loc.Get("ui.crewinfo.mo-action-pts");
				string text2 = Loc.Get("ui.crewinfo.mo-movement-pts");
				sb.Append(Loc.Get("ui.crewinfo.mo-remaining", "actionLeft", actionsRemaining, "actionPtsLabel", text, "movementLeft", movesRemaining, "movementPtsLabel", text2));
				if (details)
				{
					var (pts, pts2) = peep.components.agent.GetMovesAndActionsPerTurn();
					var (exp, exp2) = peep.components.agent.ExplainMovesAndActionsPerTurn(addHeader: false, includeZeros: false);
					sb.AppendLine(Loc.Get("ui.crewinfo.mo-generated"));
					Explain(text, pts2, peep.config.agent.actionsPerTurn.value, exp2);
					Explain(text2, pts, peep.config.agent.movesPerTurn.value, exp);
				}
			}
		}
	}
}

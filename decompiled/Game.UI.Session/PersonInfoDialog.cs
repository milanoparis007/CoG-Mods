using System.Collections.Generic;
using Game.Services;
using Game.Session;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.AI;
using Game.UI.Mouseovers;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session;

public class PersonInfoDialog : HUDView<PersonInfoModel, PersonInfoDialog, PersonInfoController>
{
	private class ToggleContext : MonoBehaviour
	{
		public PersonInfoSubview view;
	}

	private sealed class PersonInfoIntroMouseover : BaseCustomTextMouseover
	{
		protected override string ProduceText()
		{
			Logger.Error("The PersonInfoIntroMouseOver class is deprecated, use PersonInfoMouseover");
			return string.Empty;
		}
	}

	private sealed class PersonInfoMouseover : BaseCustomTextMouseover
	{
		protected override string ProduceText()
		{
			CardContext component = context.GetComponent<CardContext>();
			if (component == null || component.data == null)
			{
				return null;
			}
			PersonInfoUtil.Overview overview = component.data.overview;
			return Loc.Get("ui.personinfo.mouseover", "name", overview.name, "ethAndAge", overview.GetRelAgeAndEth(), "workplace", overview.GetWorkplaceOrUnemployed());
		}
	}

	private sealed class PersonInfoSkillsMouseover : BaseCustomTextMouseover
	{
		protected override string ProduceText()
		{
			string text = Game.ctx.players.Human.skills.GetCurrentSkills().SelectToString((SkillDef def) => Loc.Get("ui.personinfo.skill.oneline", "item", def.GetName()), "\n");
			return Loc.Get("ui.personinfo.crew.skills.mo") + "\n" + text;
		}
	}

	private sealed class PersonInfoExperienceMouseover : BaseCustomTextMouseover
	{
		protected override string ProduceText()
		{
			Entity entity = Game.ctx.hud.personInfo.Model.entity;
			if (entity == null)
			{
				return null;
			}
			string text = PersonInfoUtil.GenerateLevelupDescription(entity, detailed: true);
			return Loc.Get("ui.personinfo.crew.levelups.mo") + "\n" + text;
		}
	}

	public class PersonInfoGangMouseover : BaseCustomTextMouseover
	{
		protected override string ProduceText()
		{
			return GangInfoPanelUtil.GenerateMouseoverExplanation(Game.ctx.hud.personInfo.Model.entity);
		}
	}

	public const string TOGGLE_BUTTONS = "Side/Buttons";

	public const string CLOSE = "Owner/Close Button";

	public const string GOTO = "Owner/Goto Button";

	public const string BACK = "Owner/Back Button";

	public const string PAID_OFF = "Owner/Paid";

	public const string TEMPLATES = "Templates";

	public const string TMPL_TOGGLE = "Templates/Person Info Toggle";

	private List<PersonInfoSubview> _subviews;

	private GameObject _tmplButton;

	private GameObject _toggles;

	private const string HEADER_IMAGE = "Owner/Portrait/Portrait";

	private const string HEADER_NAME = "Owner/Name";

	private const string HEADER_REL = "Owner/Relationship";

	private const string HEADER_BACK = "Owner/Back Button";

	private const string GANGINFO_PANEL = "Gang Info";

	private const string CREWINFO_PANEL = "Crew Info";

	private const string CREWINFO_PANEL_BG = "Crew Info/BG/BG Highlight";

	private const string CREWINFO_DESC = "Crew Info/Description";

	private const string CREWINFO_SKILLS_TXT = "Crew Info/Columns/Skills Label";

	private const string CREWINFO_XP_TXT = "Crew Info/Columns/XP Label";

	private const string CREWINFO_XP_BTN = "Crew Info/Columns/XP Button";

	public override bool ShowAtStartup => false;

	public override TweenType Tween => TweenType.Right;

	public override GroupType Group => GroupType.ConvoGroup;

	public override UIReference UIReference => UIElements.PersonInfoDialog;

	internal override void Initialize()
	{
		base.Initialize();
		_go.GetOrAddComponent<EntityHolderContext>().Set(() => base.Model.EntityID);
		_go.SetButtonListener("Owner/Close Button", base.Controller.Close);
		_go.SetButtonListener("Owner/Back Button", base.Controller.ReturnToPrevious);
		_subviews = new List<PersonInfoSubview>
		{
			new ConnectionsTabSubview(_go, base.Controller, PanelType.Connections, "ui.conn"),
			new HistoryTabSubview(_go, base.Controller, PanelType.History, "ui.tickers.icon.default")
		};
		_tmplButton = _go.GetChild("Templates/Person Info Toggle");
		_toggles = _go.GetChild("Side/Buttons");
		_toggles.GetComponent<ToggleGroup>().allowSwitchOff = true;
		_go.SetActive("Templates", value: false);
		_go.SetButtonListener("Owner/Goto Button", base.Controller.OnGoToClick);
		Game.serv.mouseovers.Register(MouseoverType.RelationshipBar, new RelationshipBarMouseover());
		Game.serv.mouseovers.Register(MouseoverType.PersonInfoHeader, new PersonInfoHeaderMouseover());
		Game.serv.mouseovers.Register(MouseoverType.PersonInfoExperience, new PersonInfoExperienceMouseover());
		Game.serv.mouseovers.Register(MouseoverType.PersonInfoSkills, new PersonInfoSkillsMouseover());
		Game.serv.mouseovers.Register(MouseoverType.PersonInfoGang, new PersonInfoGangMouseover());
	}

	internal override void Release()
	{
		_subviews.Clear();
		_toggles = (_tmplButton = null);
		Game.serv.mouseovers.Unregister(MouseoverType.PersonInfoGang);
		Game.serv.mouseovers.Unregister(MouseoverType.PersonInfoExperience);
		Game.serv.mouseovers.Unregister(MouseoverType.PersonInfoSkills);
		Game.serv.mouseovers.Unregister(MouseoverType.PersonInfoHeader);
		Game.serv.mouseovers.Unregister(MouseoverType.RelationshipBar);
		base.Release();
	}

	public void Show(Entity peep, Entity selectOnClose = null)
	{
		base.Controller.SetModel(peep, selectOnClose);
		base.Show();
	}

	protected override void OnBeforeShow()
	{
		base.OnBeforeShow();
		Game.ctx.selection.ClearActive();
		Game.serv.mouseovers.Register(MouseoverType.UIMouseoverPersonCard, new PersonInfoMouseover());
		Game.serv.mouseovers.Register(MouseoverType.UIMouseoverPersonCardIntro, new PersonInfoIntroMouseover());
		Game.ctx.events.AddListener(SessionEventType.CrewLevelUpsChanged, OnLevelUp);
		Game.ctx.sfx.PlayPersonInfoShow();
		_toggles.GetComponent<ToggleGroup>().allowSwitchOff = false;
	}

	protected override void OnBeforeHide()
	{
		if (_toggles != null)
		{
			_toggles.GetComponent<ToggleGroup>().allowSwitchOff = true;
		}
		Game.ctx.events.RemoveListener(SessionEventType.CrewLevelUpsChanged, OnLevelUp);
		Game.serv.mouseovers.Unregister(MouseoverType.UIMouseoverPersonCard);
		Game.serv.mouseovers.Unregister(MouseoverType.UIMouseoverPersonCardIntro);
		DeactivateAll();
		base.Controller.Model.Reset();
		base.OnBeforeHide();
	}

	protected override void RefreshContents()
	{
		base.RefreshContents();
		RefreshTabButtons();
		RefreshHeader();
		RefreshPanel();
	}

	public void RefreshPanel()
	{
		GetCurrentPanel().RefreshSubview();
	}

	private PersonInfoSubview GetPanelByType(PanelType type)
	{
		foreach (PersonInfoSubview subview in _subviews)
		{
			if (subview.panelType == type)
			{
				return subview;
			}
		}
		return null;
	}

	public PersonInfoSubview GetCurrentPanel()
	{
		foreach (PersonInfoSubview subview in _subviews)
		{
			if (subview.IsActive)
			{
				return subview;
			}
		}
		return null;
	}

	public void OnSetCurrentPanel(PanelType type)
	{
		DeactivateAll();
		PersonInfoSubview panelByType = GetPanelByType(type);
		if (panelByType != null)
		{
			panelByType.Activate();
			panelByType.RefreshSubview();
			SetTabButtonForView(type);
		}
	}

	private void DeactivateAll()
	{
		_subviews.ForEach(delegate(PersonInfoSubview panel)
		{
			panel.Deactivate();
		});
	}

	private void RefreshTabButtons()
	{
		_toggles.EnsureChildCount(_subviews.Count, _tmplButton);
		_toggles.InitializeChildren(_subviews, InitializeTabButton);
		SetTabButtonForView(PanelType.Connections);
	}

	internal void SetTabButtonForView(PanelType type)
	{
		_toggles.GetComponent<ToggleGroup>().SetAllTogglesOff(sendCallback: false);
		Transform transform = _toggles.transform;
		int i = 0;
		for (int childCount = transform.childCount; i < childCount; i++)
		{
			Transform child = transform.GetChild(i);
			if (!(child == null))
			{
				ToggleContext component = child.gameObject.GetComponent<ToggleContext>();
				if (!(component == null) && component.view.panelType == type)
				{
					child.GetComponentInChildren<Toggle>().SetIsOnWithoutNotify(value: true);
				}
			}
		}
	}

	private void InitializeTabButton(int i, GameObject card, PersonInfoSubview view)
	{
		ToggleContext ctx = card.GetOrAddComponent<ToggleContext>();
		ctx.view = view;
		Toggle componentInChildren = card.GetComponentInChildren<Toggle>();
		ToggleGroup component = card.transform.parent.gameObject.GetComponent<ToggleGroup>();
		componentInChildren.interactable = true;
		componentInChildren.group = component;
		componentInChildren.onValueChanged.SetListener(delegate(bool isOn)
		{
			if (isOn)
			{
				base.Controller.SetCurrentPanel(ctx.view.panelType);
			}
		});
		card.SetChildText(view.GetIcon());
	}

	public void RefreshHeader()
	{
		RefreshPersonInfo();
		bool value = base.Model.entity.data.person.business.IsValid || base.Model.entity.data.agent.nid.IsValid;
		_go.SetActive("Owner/Goto Button", value);
		bool show = base.Model.data.bossPlayer?.PID.IsHumanPlayer ?? false;
		RefreshCrewDetails(show);
		var (show2, player) = GangInfoPanelUtil.IsConvoWithGangOrGoon(base.Model.entity);
		GangInfoPanelUtil.ShowGangPanel(_go.GetChild("Gang Info"), show2, player, base.Model.entity, base.Controller.SwitchToPerson);
		RefreshCopDonoState();
	}

	public void RefreshCopDonoState()
	{
		PrecinctAdvisor precinct = base.Model.entity.data.agent.pid.FindPlayer().ai.precinct;
		DonationState? donationState = precinct?.HasDonationFrom(Game.ctx.players.Human.PID);
		if (!donationState.HasValue || donationState == DonationState.NotPaidOff)
		{
			_go.SetTextOrHide("Owner/Paid", null);
		}
		else if (donationState == DonationState.PaidOff)
		{
			CopDonation copDonation = precinct.FindDonationFrom(Game.ctx.players.Human.PID);
			_go.SetTextOrHide("Owner/Paid", Loc.Get("convodialog.cop-paidoff", "date", Loc.FormatDateShort(copDonation.expiration)));
		}
		else
		{
			_go.SetTextOrHide("Owner/Paid", Loc.Get("convodialog.cop-waiting"));
		}
	}

	private void OnLevelUp(SessionEvent sev)
	{
		if (sev.pid.IsHumanPlayer && sev.eid == base.Model.EntityID)
		{
			RefreshHeader();
		}
	}

	private void RefreshPersonInfo()
	{
		Entity entity = base.Model.entity;
		PersonInfoUtil.Overview data = base.Model.data;
		string workplaceOrUnemployed = data.GetWorkplaceOrUnemployed();
		string relAgeAndEth = data.GetRelAgeAndEth();
		_go.SetText("Owner/Name", Loc.Get("ui.personinfo.dialog.header", "name", data.name, "workplace", workplaceOrUnemployed, "details", relAgeAndEth));
		_go.SetImage("Owner/Portrait/Portrait", HUDUtil.GetCrewSprite(entity));
		_go.GetChild("Owner/Back Button").SetActive(base.Model.HasHistory);
		PersonInfoUtil.UpdateRelationshipBar(_go.GetChild("Owner/Relationship"), base.Model.entity);
		bool flag = base.Model.EntityID == Game.ctx.players.Human.social.PlayerPeepId;
		_go.SetActive("Owner/Relationship", !flag);
		NameUtils.MaybePrintDebugInfoAboutPeep(entity);
	}

	public void SetBackButton(bool state)
	{
		_go.GetChild("Owner/Back Button").SetActive(state);
	}

	private void RefreshCrewDetails(bool show)
	{
		_go.SetActive("Crew Info", show);
		if (show)
		{
			PlayerInfo human = Game.ctx.players.Human;
			Color color = human.territory.colorInfo.GetPlayerColor().SetAlpha(0.25f);
			_go.GetImage("Crew Info/BG/BG Highlight").color = color;
			string text = Loc.Get((base.Model.EntityID == human.social.PlayerPeepId) ? "ui.personinfo.crew.you" : "ui.personinfo.crew.peep", "name", base.Model.entity.data.person.FirstName, "groupname", human.social.PlayerGroupName);
			CrewAssignment crewForPeep = human.crew.GetCrewForPeep(base.Model.EntityID);
			(bool active, string result) tuple = PersonInfoUtil.FindActionString(crewForPeep.GetPeep(), crewForPeep.GetVehicle());
			bool item = tuple.active;
			string item2 = tuple.result;
			string text2 = (item ? Loc.Get("ui.personinfo.crew.action", "action", item2) : "");
			string text3 = (text + " " + text2).Trim();
			_go.SetText("Crew Info/Description", text3);
			int currentSkillCount = human.skills.CurrentSkillCount;
			_go.SetText("Crew Info/Columns/Skills Label", Loc.GetPluralized("ui.personinfo.crew.skills", currentSkillCount, "num", currentSkillCount));
			bool flag = base.Model.entity.components.agent.CanShowLevelupPopup();
			string text4 = (flag ? Loc.Get("ui.personinfo.crew.levelups") : PersonInfoUtil.GenerateLevelupIcons(base.Model.entity));
			_go.SetText("Crew Info/Columns/XP Label", text4);
			_go.SetActive("Crew Info/Columns/XP Button", flag);
			_go.SetButtonListener("Crew Info/Columns/XP Button", base.Controller.OnLevelupClick);
			_go.GetChild("Crew Info").ForceRebuildLayoutImmediate();
		}
	}
}

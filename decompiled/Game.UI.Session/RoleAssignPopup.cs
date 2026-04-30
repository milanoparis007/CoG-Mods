using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session;
using Game.Session.Data;
using Game.Session.Player;
using Game.UI.Session.Popups;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session;

public class RoleAssignPopup : BasePopup
{
	private const string TEMPLATES = "Templates";

	private const string TMPL_RCARD = "Templates/Role Selection Card";

	private const string TITLE = "Panel/Title";

	private const string NAME = "Panel/Header";

	private const string B_CLOSE = "Panel/Close Button";

	private const string B_INSTALL = "Panel/Footer/OK";

	private const string B_CANCEL = "Panel/Footer/Cancel";

	private const string BANNER = "Panel/Description List/Banner";

	private const string MCARD_LIST = "Panel/Module List/Viewport/Content";

	private const string DESC_LIST = "Panel/Description List/Viewport/Content";

	private const string DESC_TEXT = "Panel/Description List/Viewport/Content/Text";

	private GameObject _tmplRoleCard;

	private GameObject _rcardContainer;

	private RoleDef _role;

	private RoleAddContext _selected;

	private const string CARD_IMAGE = "Image";

	private const string CARD_NAME = "Name";

	public override UIReference UIReference => UIElements.RoleAssignPopup;

	public RoleAssignPopup(RoleDef role)
	{
		_role = role;
	}

	protected override void InitializeOnPush()
	{
		_go.SetActive("Templates", value: false);
		_tmplRoleCard = _go.GetChild("Templates/Role Selection Card");
		_go.SetText("Panel/Title", Loc.Get("ui.orgchart.add-popup", "roleName", Loc.Get(_role.loctitle)));
		_go.SetText("Panel/Header", Loc.Get(_role.locshort));
		_go.SetButtonListener("Panel/Footer/OK", OnConfirm);
		_go.SetButtonListener("Panel/Footer/Cancel", OnCancel);
		_go.SetButtonListener("Panel/Close Button", OnCancel);
		_rcardContainer = _go.GetChild("Panel/Module List/Viewport/Content");
		_rcardContainer.GetComponent<ToggleGroup>().allowSwitchOff = false;
		RefreshCards();
		RefreshDetails(null, shutdown: false);
		RefreshButtons();
	}

	protected override void ReleaseOnPop()
	{
		RefreshDetails(null, shutdown: true);
		_rcardContainer = (_tmplRoleCard = null);
	}

	private void OnCancel()
	{
		Close();
	}

	private void OnConfirm()
	{
		OkCancelPopup popup = MakeConfirmPopup(_selected, _role);
		Close();
		Game.serv.ui.AddPopup(popup);
	}

	private static OkCancelPopup MakeConfirmPopup(RoleAddContext selected, RoleDef def)
	{
		return new OkCancelPopup(Loc.Get("ui.orgchart.role.add-confirm", "name", selected.peep.data.person.FullName, "role", Loc.Get(def.loctitle)), delegate
		{
			selected.peep.data.agent.xp.SetCrewRole(def.id);
			Game.ctx.events.EnqueueOnce(SessionEventType.CrewRoleGiven);
		}, delegate
		{
		});
	}

	public void RefreshCards()
	{
		List<CrewAssignment> data = SortCaptains(Game.ctx.players.Human.crew.AllCaptains);
		_rcardContainer.EnsureChildCount(data, _tmplRoleCard);
		_rcardContainer.InitializeChildren(data, InitializeCard);
	}

	public void RefreshButtons()
	{
		bool interactable = _selected != null && _selected.canPromote;
		_go.GetButton("Panel/Footer/OK").interactable = interactable;
	}

	private void InitializeCard(int index, GameObject card, CrewAssignment crew)
	{
		bool canPromote = Game.ctx.players.Human.crew.IsQualifiedForRole(crew, _role);
		card.SetImage("Image", HUDUtil.GetCrewSprite(crew.GetPeep()));
		card.SetText("Name", crew.GetPeep().data.person.FullName);
		card.GetOrAddComponent<RoleAddContext>().Set(crew.GetPeep(), canPromote);
		Toggle componentInChildren = card.GetComponentInChildren<Toggle>();
		componentInChildren.group = _rcardContainer.GetComponent<ToggleGroup>();
		componentInChildren.SetIsOnWithoutNotify(value: false);
		componentInChildren.onValueChanged.SetListener(delegate(bool isOn)
		{
			if (isOn)
			{
				OnCardClick(card);
			}
		});
	}

	private void OnCardClick(GameObject card)
	{
		RoleAddContext component = card.GetComponent<RoleAddContext>();
		RefreshDetails(component, shutdown: false);
		RefreshButtons();
	}

	private void RefreshDetails(RoleAddContext ctx, bool shutdown)
	{
		_selected = ctx;
		ClearDescription();
		if (!shutdown)
		{
			if (ctx != null)
			{
				SetDescription(ctx);
			}
			GameObject child = _go.GetChild("Panel/Description List/Viewport/Content");
			child.ForceRebuildLayoutImmediate();
			child.ForceRebuildLayoutImmediate();
			child.ForceRebuildLayoutImmediate();
		}
	}

	private void ClearDescription()
	{
		_go.SetText("Panel/Description List/Viewport/Content/Text", "");
		_go.GetChild("Panel/Description List/Banner").SetActive(value: false);
	}

	private void SetDescription(RoleAddContext ctx)
	{
		VisitRequirementList reqs = _role.reqs;
		_ = ctx.peep;
		VisitState visit = new VisitState(ctx.peep.components.agent.FindCrewAssignment(), Game.ctx.clock.Now, PlayerID.HumanPlayer);
		string text = reqs.Explain(visit);
		string text2 = Loc.Get("ui.orgchart.role-assign", "promoteInfo", text);
		_go.SetText("Panel/Description List/Viewport/Content/Text", text2);
	}

	private List<CrewAssignment> SortCaptains(IEnumerable<CrewAssignment> captains)
	{
		IOrderedEnumerable<IGrouping<int, CrewAssignment>> orderedEnumerable = from x in captains
			where Game.ctx.players.Human.crew.IsValidPromotionChoice(x)
			group x by _role.reqs.CountPass(new VisitState(x, Game.ctx.clock.Now, PlayerID.HumanPlayer)) into x
			orderby x.Key descending
			select x;
		List<CrewAssignment> list = new List<CrewAssignment>();
		foreach (IGrouping<int, CrewAssignment> item in orderedEnumerable)
		{
			IOrderedEnumerable<CrewAssignment> collection = item.OrderByDescending((CrewAssignment x) => CountRelevantStats(x, _role.reqs));
			list.AddRange(collection);
		}
		return list;
	}

	private int CountRelevantStats(CrewAssignment crew, VisitRequirementList reqs)
	{
		int num = 0;
		foreach (IVisitRequirement req in reqs)
		{
			if (req is CheckCrewStat checkCrewStat)
			{
				num += MathUtil.ClampMax(crew.GetPeep().data.agent?.crewHistoryStats[checkCrewStat.id] ?? 0, checkCrewStat.value);
			}
		}
		return num;
	}
}

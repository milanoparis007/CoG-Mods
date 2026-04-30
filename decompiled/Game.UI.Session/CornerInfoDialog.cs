using System;
using Game.Core;
using Game.Services;
using Game.Session;
using Game.Session.Board;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.AI;
using Game.Session.Sim;
using Game.UI.Mouseovers;
using Game.UI.Session.Picks;
using SomaSim.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session;

public sealed class CornerInfoDialog : BaseHUDDialog
{
	private class InfoCtx : MonoBehaviour
	{
		public NodeID nid;

		public PlayerID pid;

		internal void Set(NodeID node, PlayerID player)
		{
			nid = node;
			pid = player;
		}
	}

	private class RespectRowMouseover : BaseCustomTextMouseover
	{
		public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

		protected override string ProduceText()
		{
			InfoCtx component = context.GetComponent<InfoCtx>();
			if (component.pid.IsHumanPlayer)
			{
				Node node = component.nid.FindNode();
				Respect orNull = node.respect.GetOrNull(component.pid);
				ModQuery query = new ModQuery(component.pid, node);
				return ProportionalValue.Explain(orNull, query, Loc.Get("ui.corner.respect.human.mouseover"));
			}
			PlayerInfo playerInfo = component.pid.FindPlayer();
			string text = playerInfo.social.FindPlayerGroupNameColorized();
			string playerFullName = playerInfo.social.PlayerFullName;
			return Loc.Get("ui.corner.respect.row.mouseover", "groupname", text, "peepname", playerFullName);
		}
	}

	private sealed class HeatInfoMouseover : BaseCustomTextMouseover
	{
		protected override string ProduceText()
		{
			return Game.ctx.hud.cornerInfo.MakeHeatDescription();
		}
	}

	private sealed class CopsInfoMouseover : BaseCustomTextMouseover
	{
		protected override string ProduceText()
		{
			return Game.ctx.hud.cornerInfo.MakeCopsDescription();
		}
	}

	private const string TEMPLATES = "Templates/";

	private const string RESPECT_TEMP = "Templates/Corner Respect Row/";

	private const string BIZ_TEMP = "Templates/Business Row/";

	private const string CLOSE_BUTTON = "Close";

	private const string TITLE = "Header/Title";

	private const string COP_BUTTON = "Cops/Button";

	private const string COP_OFFICER = "Cops/Officer";

	private const string COP_HEADER = "Cops/Header";

	private GameObject _tmplRespectRow;

	private GameObject _tmplBizRow;

	private Node _node;

	private Entity _selectOnClose;

	private CrewAssignment _crew;

	private const string RESPECT_HEADER = "Respect/Header";

	private const string RESPECT_DEFAULT = "Respect/Default";

	private const string RESPECT_SCROLL = "Respect/Scroll View";

	private const string RESPECT_CONTAINER = "Respect/Scroll View/Viewport/Content";

	private const string CARD_TEXT = "Text";

	private const string CARD_BUTTON = "Button";

	private const string HEAT_TEXT = "Heat/Header";

	private const string BIZ_HEADER = "Businesses/Header";

	private const string BIZ_DEFAULT = "Businesses/Default";

	private const string BIZ_SCROLL = "Businesses/Scroll View";

	private const string BIZ_CONTENT = "Businesses/Scroll View/Viewport/Content";

	private const string BIZCARD_OVERLAY = "Button/Overlay";

	private const string BIZCARD_BUTTONTEXT = "Button/Text";

	private const string BIZCARD_BUTTON = "Button";

	private const string BIZCARD_TEXT = "Text";

	public override bool ShowAtStartup => false;

	public override TweenType Tween => TweenType.Right;

	public override GroupType Group => GroupType.ConvoGroup;

	public override UIReference UIReference => UIElements.HUDCornerInfo;

	internal override void Initialize()
	{
		base.Initialize();
		Game.serv.mouseovers.Register(MouseoverType.CornerRespectRow, new RespectRowMouseover());
		Game.serv.mouseovers.Register(MouseoverType.CornerHeatInfo, new HeatInfoMouseover());
		Game.serv.mouseovers.Register(MouseoverType.CornerCopsInfo, new CopsInfoMouseover());
		Game.ctx.events.AddListener(SessionEventType.SelectionActivationChange, OnCurrentActiveChanged);
		Game.ctx.events.AddListener(SessionEventType.HumanPlayerTurnStarted, OnHideEvent);
		Game.ctx.events.AddListener(SessionEventType.PlayerCommandExecutedOneTurn, OnHideEvent);
		_go.SetButtonListener("Close", delegate
		{
			Close(_selectOnClose);
		});
		_tmplRespectRow = _go.GetChild("Templates/Corner Respect Row/");
		_tmplBizRow = _go.GetChild("Templates/Business Row/");
	}

	internal override void Release()
	{
		_tmplBizRow = (_tmplRespectRow = null);
		Game.ctx.events.RemoveListener(SessionEventType.HumanPlayerTurnStarted, OnHideEvent);
		Game.ctx.events.RemoveListener(SessionEventType.PlayerCommandExecutedOneTurn, OnHideEvent);
		Game.ctx.events.RemoveListener(SessionEventType.SelectionActivationChange, OnCurrentActiveChanged);
		Game.serv.mouseovers.Unregister(MouseoverType.CornerCopsInfo);
		Game.serv.mouseovers.Unregister(MouseoverType.CornerHeatInfo);
		Game.serv.mouseovers.Unregister(MouseoverType.CornerRespectRow);
		base.Release();
	}

	public override void Show()
	{
		throw new NotSupportedException("Use Show(Node) with " + typeof(CornerInfoDialog));
	}

	public void Show(CrewAssignment crew, Node node, Entity selectOnClose = null)
	{
		bool num = base.IsShowing && node != _node;
		_crew = crew;
		_node = node;
		_selectOnClose = selectOnClose;
		if (Game.ctx.selection.HasActive && Game.ctx.selection.CurrentActive.config.selection.onaction != SelectionConfig.OnAction.Corner)
		{
			Game.ctx.selection.ClearActive();
		}
		Game.ctx.selection.ShowNodeHighlightAt(node);
		Game.ctx.events.EnqueueOnce(SessionEventType.UICornerDialogOpened, PlayerID.HumanPlayer);
		if (num)
		{
			RefreshContents();
		}
		else
		{
			base.Show();
		}
	}

	protected override void OnBeforeHide()
	{
		base.OnBeforeHide();
		Game.ctx.selection.HideNodeHighlight();
		_node = null;
		_crew = CrewAssignment.EMPTY;
	}

	protected override void RefreshContents()
	{
		base.RefreshContents();
		HUDUtil.GoTo(_node.pos, zoomIn: false, showFx: true);
		RefreshHeader();
		RefreshRespect();
		RefreshHeat();
		RefreshRaids();
		RefreshBusinesses();
	}

	private void Close(Entity selectOnClose)
	{
		if (base.IsShowing)
		{
			Hide();
			if (selectOnClose != null)
			{
				Game.ctx.selection.SetActive(selectOnClose);
			}
		}
	}

	private void OnCurrentActiveChanged(SessionEvent sev)
	{
		CornerData cornerData = sev.eid.FindEntity()?.data.corner;
		if (cornerData == null)
		{
			Close(null);
			return;
		}
		Node node = cornerData.FindNode();
		if (node != _node && node != null)
		{
			Show(_crew, node);
		}
	}

	private void OnHideEvent(SessionEvent ev)
	{
		Close(null);
	}

	private void RefreshHeader()
	{
		string text = Loc.Get(_node.FindMainEthnicity().loc.adjEthnicity);
		string text2 = Loc.Get("ui.corner.ethnicity", "ethadj", text);
		string cornerNameForDialog = _node.GetCornerNameForDialog();
		PlayerID pid = _node.owner.Get();
		string text3 = (pid.IsValid ? Loc.Get("ui.corner.controller", "name", pid.FindPlayer().social.FindPlayerGroupNameColorized()) : string.Empty);
		string text4 = Loc.Get("ui.corner.header", "loc", cornerNameForDialog, "eth", text2, "controller", text3).Trim();
		_go.SetText("Header/Title", text4);
	}

	private void RefreshRespect()
	{
		_go.GetText("Respect/Header");
		TextMeshProUGUI text = _go.GetText("Respect/Default");
		GameObject child = _go.GetChild("Respect/Scroll View");
		GameObject child2 = _go.GetChild("Respect/Scroll View/Viewport/Content");
		text.SetText(Loc.Get("ui.corner.respect.default"));
		bool flag = _node.respect.IsAnyPositive();
		text.gameObject.SetActive(!flag);
		child.SetActive(flag);
		if (!flag)
		{
			return;
		}
		using ListPool<ProportionalValue>.PooledBlockList pooledBlockList = ListPool<ProportionalValue>.Allocate();
		pooledBlockList.AddRange(_node.respect.data);
		pooledBlockList.Sort((ProportionalValue a, ProportionalValue b) => b.goal.scaled - a.goal.scaled);
		child2.EnsureChildCount(pooledBlockList.Count, _tmplRespectRow);
		child2.InitializeChildren(pooledBlockList, InitRespectRow);
	}

	private void InitRespectRow(int index, GameObject card, ProportionalValue respect)
	{
		int value = (int)respect.current;
		PlayerInfo playerInfo = Game.ctx.players.WithID(respect.pid);
		string text = playerInfo.social.FindPlayerGroupNameColorized();
		string text2 = Loc.Get("ui.corner.respect.row.desc", "current", Loc.FormatNumber(value), "name", text);
		card.SetText("Text", text2);
		Node playerNode = playerInfo.territory.GetHeadquartersNode();
		Button button = card.GetButton("Button");
		button.interactable = true;
		button.onClick.SetListener(delegate
		{
			GoToPlayerLocation(playerNode);
		});
		card.GetOrAddComponent<InfoCtx>().Set(_node.id, respect.pid);
	}

	private static void GoToPlayerLocation(Node cornernode)
	{
		HUDUtil.GoTo(cornernode.pos, zoomIn: false, showFx: true);
	}

	private void RefreshHeat()
	{
		(Fixnum heatvalue, int index, string icon) heatValueAndIcon = GetHeatValueAndIcon();
		Fixnum item = heatValueAndIcon.heatvalue;
		string item2 = heatValueAndIcon.icon;
		string text = Loc.Get("ui.corner.heat.header", "value", Loc.FormatNumber(item), "icon", item2);
		_go.SetText("Heat/Header", text);
	}

	private (Fixnum heatvalue, int index, string icon) GetHeatValueAndIcon()
	{
		Fixnum fixnum = Game.serv.globals.settings.people.social.police.raidThreshold.Evaluate(new ModQuery(Game.ctx.players.Human.PID)) / 4;
		Fixnum obj = _node.heat.GetOrNull(PlayerID.HumanPlayer)?.current ?? Fixnum.ZERO;
		int num = MathUtil.Clamp((obj / fixnum).IntFloor(), 0, 4);
		string pluralized = Loc.GetPluralized("ui.heatlevel", num);
		return (heatvalue: obj, index: num, icon: pluralized);
	}

	internal string MakeHeatDescription()
	{
		Heat orNull = _node.heat.GetOrNull(PlayerID.HumanPlayer);
		(Fixnum heatvalue, int index, string icon) heatValueAndIcon = GetHeatValueAndIcon();
		Fixnum item = heatValueAndIcon.heatvalue;
		int item2 = heatValueAndIcon.index;
		string item3 = heatValueAndIcon.icon;
		string pluralized = Loc.GetPluralized("ui.heatleveltext", item2);
		string pluralized2 = Loc.GetPluralized("ui.heatlevelexpn", item2);
		ModQuery query = new ModQuery(PlayerID.HumanPlayer, _node);
		string text = ((item <= 0) ? "" : ProportionalValue.Explain(orNull, query, Loc.Get("ui.corner.heat.name")));
		return (item3 + " " + pluralized + "\n\n" + pluralized2 + "\n\n" + text).Trim();
	}

	internal bool IsPaidOffByHuman()
	{
		return _node.precinctId.FindPrecinct().ai.precinct.FindDonationFrom(PlayerID.HumanPlayer)?.payer.IsHumanPlayer ?? false;
	}

	private void RefreshRaids()
	{
		PlayerInfo playerInfo = _node.precinctId.FindPrecinct();
		if (playerInfo == null)
		{
			_go.SetText("Cops/Header", Loc.Get("ui.corner.cops.no-cops", "copicon", Loc.Get("ui.copicon")));
			return;
		}
		Button button = _go.GetButton("Cops/Button");
		Node playerNode = playerInfo.territory.GetHeadquartersNode();
		button.onClick.SetListener(delegate
		{
			GoToPlayerLocation(playerNode);
		});
		string text = (IsPaidOffByHuman() ? Loc.Get("ui.copicon.paidyes") : Loc.Get("ui.copicon.paidno"));
		string text2 = Loc.Get("ui.corner.cops.header", "paidicon", text, "copicon", Loc.Get("ui.copicon"), "precinct", playerInfo.ai.precinct.GetPrecinctName(), "name", playerInfo.social.PlayerFullName);
		_go.SetText("Cops/Header", text2);
	}

	internal string MakeCopsDescription()
	{
		return MakePrecinctDetails() + "\n\n" + MakeRaidHistoryDesc();
	}

	private string MakePrecinctDetails()
	{
		if (_node == null)
		{
			return "";
		}
		PlayerInfo playerInfo = _node.precinctId.FindPrecinct();
		bool flag = IsPaidOffByHuman();
		CopDonation copDonation = playerInfo.ai.precinct.FindDonationFrom(PlayerID.HumanPlayer);
		string[] array = new string[8]
		{
			"name",
			playerInfo.social.PlayerFullName,
			"police",
			playerInfo.social.FindPlayerGroupNameColorized(),
			"precinct",
			playerInfo.ai.precinct.GetPrecinctName(),
			"date",
			(copDonation == null) ? "" : Loc.FormatDateShort(copDonation.expiration)
		};
		object[] replacements;
		if (!flag)
		{
			replacements = array;
			return Loc.Get("ui.corner.cops.neutral", replacements);
		}
		replacements = array;
		return Loc.Get("ui.corner.cops.paidoff", replacements);
	}

	private string MakeRaidHistoryDesc()
	{
		if (_node == null)
		{
			return "";
		}
		NodeRaidInfo raidOrNull = _node.GetRaidOrNull();
		bool flag = raidOrNull?.HasActiveRaid ?? false;
		bool flag2 = raidOrNull?.WasRecentlyRaided() ?? false;
		bool flag3 = raidOrNull?.HasEverBeenRaided ?? false;
		if (flag)
		{
			return Loc.Get("ui.corner.raid.active");
		}
		if (flag2)
		{
			return Loc.Get("ui.corner.raid.recent");
		}
		if (flag3)
		{
			return Loc.Get("ui.corner.raid.past");
		}
		PlayerInfo human = Game.ctx.players.Human;
		Heat orNull = _node.heat.GetOrNull(human.PID);
		if (orNull == null || !orNull.AnyValue)
		{
			return Loc.Get("ui.corner.raid.inactive");
		}
		return Loc.Get("ui.corner.raid.low");
	}

	private void RefreshBusinesses()
	{
		_go.GetText("Businesses/Header");
		TextMeshProUGUI text = _go.GetText("Businesses/Default");
		GameObject child = _go.GetChild("Businesses/Scroll View");
		GameObject child2 = _go.GetChild("Businesses/Scroll View/Viewport/Content");
		text.SetText(Loc.Get("ui.corner.business.default"));
		bool flag = _node.interesting.Count != 0;
		text.gameObject.SetActive(!flag);
		child.SetActive(flag);
		if (flag)
		{
			child2.EnsureChildCount(_node.interesting.Count, _tmplBizRow);
			child2.InitializeChildren(_node.interesting, InitBizRow);
		}
	}

	private static void InitBizRow(int index, GameObject card, EntityID buildingId)
	{
		PlayerTerritory territory = Game.ctx.players.Human.territory;
		BuildingAndBusinessData data = BuildingUtil.FindDataForBuilding(buildingId);
		bool flag = territory.IsScoped(data.building);
		territory.IsControlled(data.building);
		Color playerBuildingButtonColor = BuildingPickUtil.GetPlayerBuildingButtonColor(data.building, flag, crewhere: true);
		card.GetChild("Button/Overlay").GetImage().color = playerBuildingButtonColor;
		string text = (flag ? data.biz.config.biz.GetIcon() : Loc.Get("ui.pipicon.unscoped"));
		card.SetText("Button/Text", text);
		Button button = card.GetButton("Button");
		button.interactable = flag;
		button.onClick.SetListener(delegate
		{
			Game.ctx.selection.SetActive(data.building);
		});
		string text2 = BuildingUtil.FindBuildingName(data.building);
		string text3 = (flag ? data.owner.data.person.FullName : string.Empty);
		string text4 = Loc.Get("ui.corner.business.row.desc", "name", text2, "owner", text3);
		card.SetText("Text", text4);
	}
}

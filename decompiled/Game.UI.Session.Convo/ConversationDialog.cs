using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.AI;
using Game.Session.Quests;
using Game.Session.Sim.Modules;
using Game.UI.Mouseovers;
using Game.UI.Session.OwnedBiz;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.Convo;

public sealed class ConversationDialog : HUDView<ConversationModel, ConversationDialog, ConversationController>
{
	public class ConversationBuySellMouseover : BaseCustomTextMouseover
	{
		public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

		protected override string ProduceText()
		{
			DonationState? donationState = Game.ctx.hud.convoDialog.IsCopPaidOff();
			if (donationState.HasValue && donationState == DonationState.PaidOff)
			{
				PlayerInfo playerInfo = Game.ctx.hud.convoDialog.ConvoTarget.data.agent.pid.FindPlayer();
				CopDonation copDonation = playerInfo.ai.precinct.FindDonationFrom(Game.ctx.players.Human.PID);
				string[] array = new string[8]
				{
					"name",
					playerInfo.social.PlayerFullName,
					"police",
					playerInfo.social.FindPlayerGroupNameColorized(),
					"precinct",
					playerInfo.ai.precinct.GetPrecinctName(),
					"date",
					Loc.FormatDateShort(copDonation.expiration)
				};
				object[] replacements = array;
				return Loc.Get("ui.corner.cops.paidoff", replacements);
			}
			if (donationState.HasValue && donationState == DonationState.WaitingForRefresh)
			{
				return Loc.Get("convodialog.cop-waiting");
			}
			return BuildingUtil.GetBuySellText(Game.ctx.hud.convoDialog.Model.visit, mouseover: true, explain: true);
		}
	}

	public class ConversationGangInfoMouseover : BaseCustomTextMouseover
	{
		protected override string ProduceText()
		{
			return GangInfoPanelUtil.GenerateMouseoverExplanation(Game.ctx.hud.convoDialog.Model.visit.npc);
		}
	}

	private GameObject _panelContainer;

	private GameObject _tmplBlurbPlayer;

	private GameObject _tmplBlurbNpc;

	private ViewContents _panel;

	private const string TEMPLATES = "Templates";

	private const string TMPL_MODULE_BUTTON = "Templates/Module Toggle";

	private const string TMPL_BLURB_PLAYER = "Templates/Blurb Player";

	private const string TMPL_BLURB_NPC = "Templates/Blurb NPC";

	private const string SCROLL_VIEW = "Scroll View List";

	private const string SCROLL_VIEW_CONTENT = "Scroll View List/Viewport/Content";

	private const string HISTORY = "Scroll View List/Viewport/Content/History";

	private const string VIEW_CONTENTS = "Scroll View List/Viewport/Content/View Container";

	private const string MODULE_BG = "Module Background";

	private const string MODULE_BUTTON_GROUP = "Modules";

	private const string MODULE_BUTTON_CONTAINER = "Modules/Buttons";

	private const string CLOSE_BTN = "Owner/Close Button";

	private const string OWNER_NAME = "Owner/Info/Name";

	private const string OWNER_REL = "Owner/Info/Relationship";

	private const string OWNER_INFO = "Owner/Info/Person Info Button";

	private const string OWNER_IMAGE = "Owner/Portrait/Portrait";

	private const string GANG_PANEL = "Gang";

	private const string BUYSELL = "Buy Sell";

	private int MAX_BLURBS = 30;

	private const string BLURB_HEADER = "Header";

	private const string BLURB_BODY = "Body";

	public override bool ShowAtStartup => false;

	public override TweenType Tween => TweenType.Right;

	public override GroupType Group => GroupType.ConvoGroup;

	public override UIReference UIReference => UIElements.HUDConvo;

	internal GameObject Go => _go;

	internal GameObject ViewContainer => _panelContainer;

	public Entity ConvoTarget => base.Model.visit.npc ?? base.Model.visit.peep;

	public bool IsConvoWithOwner()
	{
		return base.Model.visit.AtValidBiz;
	}

	public bool IsConvoWithPeep()
	{
		return base.Model.visit.npc.components.agent != null;
	}

	public bool IsConvoWithCrewHuman()
	{
		return base.Model.visit.npc.data.agent.pid.IsHumanPlayer;
	}

	public bool IsConvoWithCrewNonHuman()
	{
		return base.Model.visit.npc.data.agent.pid.IsAIPlayer;
	}

	public bool IsConvoWithCrewOf(PlayerID pid)
	{
		return base.Model.visit.npc.data.agent.pid == pid;
	}

	private (bool isAI, PlayerInfo player) IsConvoWithGangOrGoon()
	{
		return GangInfoPanelUtil.IsConvoWithGangOrGoon(base.Model.visit.npc);
	}

	internal override void Initialize()
	{
		base.Initialize();
		_go.GetOrAddComponent<EntityHolderContext>().Set(() => base.Model.visit?.npc?.Id ?? EntityID.INVALID);
		_go.GetChild("Templates").SetActive(value: false);
		_tmplBlurbPlayer = _go.GetChild("Templates/Blurb Player");
		_tmplBlurbNpc = _go.GetChild("Templates/Blurb NPC");
		_panelContainer = _go.GetChild("Scroll View List/Viewport/Content/View Container");
		_panelContainer.SetActive(value: true);
		_go.GetButton("Owner/Info/Person Info Button").onClick.SetListener(OnInfoClick);
		_go.GetButton("Owner/Close Button").onClick.SetListener(OnCloseClick);
		foreach (BuffConfig definition in BuffStack.GetSettings().definitions)
		{
			Game.ctx.console.Add(this, new DebugConsoleEntry("convo", "add-buff", definition.id.String, DebugAddRelationship));
			Game.ctx.console.Add(this, new DebugConsoleEntry("convo", "remove-buff", definition.id.String, DebugRemoveRelationship));
		}
		foreach (QuestDefinition allQuest in Game.serv.globals.settings.quests.GetAllQuests())
		{
			Game.ctx.console.Add(this, new DebugConsoleEntry("convo", "force-quest", allQuest.id, DebugForceQuests));
		}
		Game.ctx.console.Add(this, new DebugConsoleEntry("convo", "add-tickets", DebugAddTickets));
		Game.serv.mouseovers.Register(MouseoverType.ConvoButton, new ConversationButtonMouseover());
		Game.serv.mouseovers.Register(MouseoverType.ConvoBuySell, new ConversationBuySellMouseover());
		Game.serv.mouseovers.Register(MouseoverType.ConvoGangInfo, new ConversationGangInfoMouseover());
	}

	internal override void Release()
	{
		Game.serv.mouseovers.Unregister(MouseoverType.ConvoGangInfo);
		Game.serv.mouseovers.Unregister(MouseoverType.ConvoBuySell);
		Game.serv.mouseovers.Unregister(MouseoverType.ConvoButton);
		Game.ctx.console.Remove(this);
		_panelContainer = (_tmplBlurbNpc = (_tmplBlurbPlayer = null));
		base.Release();
	}

	public override void Show()
	{
		throw new NotSupportedException("Use Show() on the controller!");
	}

	public override void Hide()
	{
		base.Controller?.OnDialogReset();
		base.Hide();
	}

	internal void OnControllerRefreshOrShow(ConversationController _)
	{
		if (base.IsShowing)
		{
			RefreshContents();
		}
		else
		{
			base.Show();
		}
	}

	protected override void RefreshContents()
	{
		base.RefreshContents();
		ShowPersonInfo();
		ShowBuildingBanner();
		UpdateRelationshipBars();
		UpdateModuleButtons();
	}

	protected override void OnBeforeShow()
	{
		base.OnBeforeShow();
		ClearNpcBlurb();
	}

	protected override void OnAfterShow()
	{
		base.OnAfterShow();
		Game.ctx.events.AddListener(SessionEventType.SomeEntityRelationshipChanged, OnSomeRelationshipChanged);
		Game.ctx.events.AddListener(SessionEventType.PlayerUsedSocialTicket, OnSomeRelationshipChanged);
	}

	protected override void OnBeforeHide()
	{
		Game.ctx.events.RemoveListener(SessionEventType.PlayerUsedSocialTicket, OnSomeRelationshipChanged);
		Game.ctx.events.RemoveListener(SessionEventType.SomeEntityRelationshipChanged, OnSomeRelationshipChanged);
		base.Controller?.OnDialogBeforeHide();
		HideSubview();
		ClearNpcBlurb();
		base.OnBeforeHide();
	}

	private void OnSomeRelationshipChanged(SessionEvent se)
	{
		UpdateRelationshipBars();
	}

	private void ShowPersonInfo()
	{
		Entity convoTarget = ConvoTarget;
		Sprite crewSprite = HUDUtil.GetCrewSprite(convoTarget);
		_go.GetImage("Owner/Portrait/Portrait").sprite = crewSprite;
		string text = convoTarget?.data.person?.FullName ?? "";
		string personDesc = GetPersonDesc();
		string text2 = Loc.Get("convodialog.personinfo", "owner", text, "desc", personDesc);
		_go.SetText("Owner/Info/Name", text2);
		(bool isAI, PlayerInfo player) tuple = IsConvoWithGangOrGoon();
		bool item = tuple.isAI;
		PlayerInfo item2 = tuple.player;
		bool flag = base.Model.visit.building?.data.civic != null;
		GangInfoPanelUtil.ShowGangPanel(_go.GetChild("Gang"), item, item2, base.Model.visit.npc, OnTalkToClick);
		ShowBuySell(!item && base.Model.visit.IsDisplayBuySell() && !flag);
		ShowCopPaidOff(IsCopPaidOff());
		NameUtils.MaybePrintDebugInfoAboutPeep(convoTarget);
	}

	private string GetPersonDesc()
	{
		if (IsConvoWithOwner())
		{
			return Loc.Get("convodialog.ownerof", "bizname", BuildingUtil.FindBuildingName(base.Model.visit.building));
		}
		return PersonInfoUtil.GenerateEmploymentString(base.Model.visit.npc);
	}

	private DonationState? IsCopPaidOff()
	{
		try
		{
			return ConvoTarget.data.agent.pid.FindPlayer().ai.precinct?.HasDonationFrom(Game.ctx.players.Human.PID);
		}
		catch (Exception ex)
		{
			Game.serv.stats.LogException(ex);
			return null;
		}
	}

	private void ShowBuySell(bool show)
	{
		string text = (show ? BuildingUtil.GetBuySellText(base.Model.visit, mouseover: false) : null);
		if (string.IsNullOrWhiteSpace(text))
		{
			text = null;
		}
		_go.SetTextOrHide("Buy Sell", text);
	}

	private void ShowCopPaidOff(DonationState? show)
	{
		if (show.HasValue && show != DonationState.NotPaidOff)
		{
			if (show == DonationState.PaidOff)
			{
				CopDonation copDonation = ConvoTarget.data.agent.pid.FindPlayer().ai.precinct.FindDonationFrom(Game.ctx.players.Human.PID);
				string text = Loc.Get("convodialog.cop-paidoff", "date", Loc.FormatDateShort(copDonation.expiration));
				_go.SetTextOrHide("Buy Sell", text);
			}
			else
			{
				string text2 = Loc.Get("convodialog.cop-waiting");
				_go.SetTextOrHide("Buy Sell", text2);
			}
		}
	}

	private void UpdateModuleButtons()
	{
		GameObject child = _go.GetChild("Templates/Module Toggle");
		GameObject child2 = _go.GetChild("Modules/Buttons");
		bool num = base.Model.visit.building != null && Game.ctx.players.Human.territory.IsControlled(base.Model.visit.building);
		bool flag = base.Model.visit.building != null && (base.Model.visit.building.data.civic?.npc.IsValid ?? false);
		if (num)
		{
			bool valueOrDefault = base.Model.visit.building?.components.residence?.IsGamblingHouse == true;
			List<IModule> allSlotsUnsafe = base.Model.visit.building.components.modules.GetAllSlotsUnsafe();
			if (valueOrDefault)
			{
				List<IModule> list = allSlotsUnsafe.Where((IModule x) => !(x is InventoryModule)).ToList();
				child2.EnsureChildCount(list.Count, child);
				child2.InitializeChildren(list, InitializeModuleButtonForGambling);
			}
			else
			{
				child2.EnsureChildCount(allSlotsUnsafe.Count, child);
				child2.InitializeChildren(allSlotsUnsafe, InitializeModuleButton);
			}
		}
		else if (flag)
		{
			child2.DestroyAllChildren();
			ModuleToggleUtils.MakePoliticianToggle(child2.transform, child, base.Model.visit, selected: true).transform.SetAsFirstSibling();
			if (base.Model.visit.building.components.civic.GetWard().ElectionOngoing || base.Model.visit.building.components.civic.GetWard().mostRecentElectionResult.winner.IsValid)
			{
				ModuleToggleUtils.MakePoliticsToggle(child2.transform, child, base.Model.visit, selected: false);
			}
		}
		else
		{
			child2.DestroyAllChildren();
		}
		if (!flag)
		{
			Transform transform = ModuleToggleUtils.MakeOwnerToggle(child2.transform, child, base.Model.visit, selected: true).transform;
			transform.SetAsFirstSibling();
			transform.GetComponentInChildren<Toggle>().SetIsOnWithoutNotify(value: true);
		}
		if (base.Model.visit.GetBldgNode() != null)
		{
			ModuleToggleUtils.MakeCornerToggle(child2.transform, child, base.Model.visit);
		}
		_go.SetActive("Modules", child2.transform.childCount > 1);
	}

	private void InitializeModuleButton(int i, GameObject card, IModule _)
	{
		ModuleToggleUtils.InitializeModuleToggleCard(i, card, base.Model.visit, delegate(GameObject c)
		{
			Game.ctx.hud.ownedBiz.Show(base.Model.visit);
			Game.ctx.hud.ownedBiz.Controller.OnModuleButtonClick(c);
		});
	}

	private void InitializeModuleButtonForGambling(int i, GameObject card, IModule _)
	{
		ModuleToggleUtils.InitializeModuleToggleCard(i, card, base.Model.visit, delegate(GameObject c)
		{
			Game.ctx.hud.ownedGambling.Show(base.Model.visit);
			Game.ctx.hud.ownedGambling.Controller.OnModuleButtonClick(c);
		}, includeInventory: false);
	}

	private void ShowBuildingBanner()
	{
		if (base.Model.visit.building?.components.residence != null)
		{
			ShowResidentialBanner();
		}
		else
		{
			ShowBusinessBanner();
		}
	}

	private void ShowResidentialBanner()
	{
		_go.SetActive("Module Background", value: false);
	}

	private void ShowBusinessBanner()
	{
		_go.SetActive("Module Background", value: false);
		List<IModule> list = base.Model.visit.building?.components.modules?.GetAllSlotsUnsafe();
		if (list != null)
		{
			IModuleConfig moduleConfig = list?[0]?.ModuleConfig;
			if (moduleConfig == null)
			{
				Logger.Warning($"Missing first module in {base.Model.visit.biz} / {base.Model.visit.building}");
				return;
			}
			_go.SetActive("Module Background", value: true);
			ModulesUIUtil.RefreshModuleBackground(_go.GetChild("Module Background"), base.Model.visit.building, moduleConfig.Common);
		}
	}

	private void UpdateRelationshipBars()
	{
		PersonInfoUtil.UpdateRelationshipBar(_go.GetChild("Owner/Info/Relationship"), ConvoTarget);
	}

	internal void AddPlayerBlurb(string text)
	{
		AddBlurb(text, npc: false, clear: false);
	}

	private void ClearNpcBlurb()
	{
		AddBlurb("", npc: true, clear: true);
	}

	private void AddNpcBlurb()
	{
		AddBlurb(GetNpcBlurb(), npc: true, clear: false);
	}

	private void AddBlurb(string text, bool npc, bool clear)
	{
		List<ConversationModel.InterState.Blurb> blurbs = base.Model.shared.blurbs;
		if (clear)
		{
			blurbs.Clear();
		}
		if (!string.IsNullOrWhiteSpace(text))
		{
			blurbs.Add(new ConversationModel.InterState.Blurb(!npc, text));
		}
		RefreshHistory();
	}

	internal void TrimHistoryForTutorial()
	{
		List<ConversationModel.InterState.Blurb> blurbs = base.Model.shared.blurbs;
		while (blurbs.Count > 1)
		{
			blurbs.RemoveAt(0);
		}
		RefreshHistory();
	}

	private void RefreshHistory()
	{
		List<ConversationModel.InterState.Blurb> blurbs = base.Model.shared.blurbs;
		while (blurbs.Count > MAX_BLURBS)
		{
			blurbs.RemoveAt(0);
		}
		GameObject child = _go.GetChild("Scroll View List/Viewport/Content/History");
		child.DestroyAllChildren();
		for (int i = 0; i < blurbs.Count; i++)
		{
			bool _ = i == blurbs.Count - 1;
			AddBlurb(blurbs[i], _, child.transform);
		}
		GameObject child2 = _go.GetChild("Scroll View List");
		LayoutRebuilder.ForceRebuildLayoutImmediate(_go.GetChild("Scroll View List/Viewport/Content").GetComponent<RectTransform>());
		LayoutRebuilder.ForceRebuildLayoutImmediate(child2.GetComponent<RectTransform>());
		child2.ResetScrollView(toStart: false);
	}

	private void AddBlurb(ConversationModel.InterState.Blurb blurb, bool _, Transform transform)
	{
		GameObject dialog = UnityEngine.Object.Instantiate(blurb.player ? _tmplBlurbPlayer : _tmplBlurbNpc, transform);
		dialog.SetText("Body", blurb.text);
		string text = GetName();
		dialog.SetText("Header", text);
		string GetName()
		{
			if (base.Model.visit == null)
			{
				return null;
			}
			string text2 = (blurb.player ? base.Model.visit.peep : base.Model.visit.npc)?.data.person.FullName ?? "?";
			PlayerInfo playerInfo = (blurb.player ? Game.ctx.players.Human : base.Model.visit.npc?.data.agent?.pid.FindPlayer());
			if (playerInfo == null || !playerInfo.PID.IsAnyPlayer)
			{
				return text2;
			}
			return playerInfo?.social.WrapInPlayerColor(text2);
		}
	}

	private string GetNpcBlurb()
	{
		if (base.Model.IsBusinessVisitTooFar)
		{
			return Loc.Get("convodialog.biz-too-far");
		}
		if (base.Model.HasForcedReaction)
		{
			return base.Model.shared.forcedReaction;
		}
		return base.Model.state?.ProduceNPCBlurb(base.Model) ?? "";
	}

	private void OnCloseClick()
	{
		if (base.Model.visit.building != null)
		{
			Game.ctx.selection.ClearActive();
		}
		else
		{
			Game.ctx.hud.HideGroup(GroupType.ConvoGroup);
		}
	}

	private void OnInfoClick()
	{
		Entity building = base.Model.visit.building;
		Game.ctx.selection.ClearActive();
		Game.ctx.hud.personInfo.Show(base.Model.GetConvoTarget(), building);
	}

	private void OnTalkToClick(Entity peep)
	{
		Game.ctx.selection.ClearActive();
		base.Controller.StartCrewVisit(peep, base.Model.visit.crew);
	}

	private void SetSubview(ViewContents subview)
	{
		if (_panel != null)
		{
			_panel.Release();
			_panel = null;
		}
		if (subview != null)
		{
			_panel = subview;
			_panel.Initialize(this);
		}
		bool flag = _panel != null;
		if (_panelContainer != null)
		{
			_panelContainer.SetActive(flag);
			bool flag2 = !flag && base.Model.IsBusinessVisitTooFar;
			if (flag || flag2)
			{
				AddNpcBlurb();
			}
		}
	}

	public void HideSubview()
	{
		SetSubview(null);
	}

	public void ShowConvoState(ConvoState state)
	{
		SetSubview(new ViewConvoState(state));
	}

	public void ShowItemPicker(ConvoState state, Action<QtyAndDir> okFn, Action cancelFn)
	{
		SetSubview(new ViewItemPicker(state, okFn, cancelFn));
	}

	public void ShowMoneyPicker(ConvoDataGamblingCollect data, Action<QtyAndDir> okFn, Action cancelFn)
	{
		SetSubview(new ViewMoneyPicker(data, okFn, cancelFn));
	}

	private string DebugRemoveRelationship(string[] args)
	{
		if (args.Length != 3)
		{
			return "Invalid parameters: " + string.Join(",", args) + ", expected buff id";
		}
		if (!base.IsShowing)
		{
			return "Cannot modify relationship state - make sure you're inspecting somebody's info first";
		}
		Label buffId = new Label(args[2]);
		(Relationship to, Relationship from) tuple = Game.ctx.players.Human.social.FindOrMakeRelationshipsWith(ConvoTarget.Id);
		Relationship item = tuple.to;
		Relationship item2 = tuple.from;
		bool flag = item.RemoveBuff(buffId);
		bool flag2 = item2.RemoveBuff(buffId);
		return $"Removing rel buff {buffId.String}, was found = {flag}/{flag2}";
	}

	private string DebugForceQuests(string[] args)
	{
		if (args.Length != 3)
		{
			return "Invalid parameters: " + string.Join(",", args) + ".";
		}
		if (!base.IsShowing)
		{
			return "Cannot modify quest state - make sure you're in a conversations first";
		}
		string text = args[2];
		QuestManager quests = Game.ctx.quests;
		if (!quests.HasActiveQuestForTarget(ConvoTarget.Id) && !quests.HasCompletedQuestForTarget(ConvoTarget.Id) && !quests.HasWaitingQuestForTarget(ConvoTarget.Id))
		{
			Game.ctx.quests.Requests.RememberRequest(ConvoTarget.Id, Game.ctx.quests.FindQuestDefinition(text));
			return "Started quests " + text + " with " + ConvoTarget.Name + ".";
		}
		return "Target already has a quest! Cannot have two quests with the same target at one time.";
	}

	private string DebugAddRelationship(string[] args)
	{
		if (args.Length != 3)
		{
			return "Invalid parameters: " + string.Join(",", args) + ", expected buff id";
		}
		if (!base.IsShowing)
		{
			return "Cannot modify relationship state - make sure you're inspecting somebody's info first";
		}
		Label buffId = new Label(args[2]);
		(Relationship to, Relationship from) tuple = Game.ctx.players.Human.social.FindOrMakeRelationshipsWith(ConvoTarget.Id);
		Relationship item = tuple.to;
		Relationship item2 = tuple.from;
		bool flag = item.AddBuffNoCrew(buffId);
		bool flag2 = item2.AddBuffNoCrew(buffId);
		return $"Added rel buff {buffId.String}, replacing previous = {flag}/{flag2}";
	}

	private string DebugAddTickets(string[] args)
	{
		if (args.Length != 3)
		{
			return "Invalid parameters: " + string.Join(",", args) + ", expected # of tickets";
		}
		if (!base.IsShowing)
		{
			return "Cannot modify relationship state - make sure you're inspecting somebody's info first";
		}
		if (!int.TryParse(args[2], out var result) || result == 0)
		{
			return "Invalid # of buffs, expected some positive or negative value";
		}
		EntityID playerPeepId = Game.ctx.players.Human.social.PlayerPeepId;
		Relationship orCreate = Game.ctx.simman.rels.GetOrCreate(ConvoTarget.Id, playerPeepId, RelationshipType.Acquaintance, warnOnExisting: false);
		orCreate.GrantFreebieTickets(result);
		return $"Added {result} tickets, now: {orCreate.milestone}";
	}
}

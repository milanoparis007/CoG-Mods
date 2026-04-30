using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session;
using Game.UI.Session.Convo;
using Game.UI.Session.Crew;
using Game.UI.Session.Deliveries;
using Game.UI.Session.HUD;
using Game.UI.Session.Ledger;
using Game.UI.Session.OwnedBiz;
using Game.UI.Session.OwnedGambling;
using Game.UI.Session.Picks;
using Game.UI.Session.Politics;
using Game.UI.Session.Quests;
using Game.UI.Session.Tickers;
using Game.UI.Session.Tutorial;
using Game.UI.Session.Victory;
using SomaSim.Util;

namespace Game.UI.Session;

public sealed class HUDManager : AbstractSessionManager, IAnimatingManager, ISessionManager
{
	public FlyoutManager flyouts;

	public SpriteAtlasManager uisprites;

	public PickManager picks;

	public PortraitCache portraits;

	public DebugConsoleDialog console;

	public HUDBar bar;

	public TickerBar tickers;

	public QuestBar quests;

	public OverlaysBar overlaysBar;

	public ResourcesBar resourcesBar;

	public ReportsBar reportsBar;

	public LedgerDialog ledger;

	public VictoryDialog victory;

	public UnknownBuildingInfoDialog bldginfo;

	public CrewDialog crew;

	public CrewInfoListDialog crewinfolist;

	public DeliveriesDialog deliveries;

	public ConversationDialog convoDialog;

	public OwnedBizDialog ownedBiz;

	public OwnedGamblingDialog ownedGambling;

	public PoliticsDialog politicsDialog;

	public CornerInfoDialog cornerInfo;

	public PersonInfoDialog personInfo;

	public ItemListDialog itemList;

	public FrancineDialog francine;

	public StationInfoDialog station;

	private List<BaseHUDDialog> _dialogs;

	private List<IAnimatedSubManager<HUDManager>> _animatedSubs;

	public override void OnPreInteractive()
	{
		AbstractSessionManager.InitializeSubmanagers(this);
		_animatedSubs = TypeUtils.GetMemberInstances<IAnimatedSubManager<HUDManager>>(this).ToList();
		TypeUtils.MakeMemberInstances<BaseHUDDialog>(this);
		_dialogs = TypeUtils.GetMemberInstances<BaseHUDDialog>(this).ToList();
		_dialogs.ForEach(delegate(BaseHUDDialog dialog)
		{
			dialog.Initialize();
		});
		_dialogs.ForEach(delegate(BaseHUDDialog dialog)
		{
			if (dialog.ShowAtStartup)
			{
				dialog.Show();
			}
		});
	}

	public override void OnReleased()
	{
		Game.serv.ui.HideAllDialogsAndPopups();
		_dialogs.ForEach(delegate(BaseHUDDialog dialog)
		{
			dialog.Release();
		});
		_dialogs.Clear();
		TypeUtils.RemoveMemberInstances<BaseHUDDialog>(this);
		_animatedSubs = null;
		AbstractSessionManager.ReleaseSubmanagers(this);
	}

	public void UpdateAnimations(GameAnimUpdate anim)
	{
		foreach (BaseHUDDialog dialog in _dialogs)
		{
			if (dialog.IsShowing)
			{
				dialog.UpdateAnimations(anim);
			}
		}
		foreach (IAnimatedSubManager<HUDManager> animatedSub in _animatedSubs)
		{
			animatedSub.UpdateAnimations(anim);
		}
	}

	public void Show(BaseHUDDialog dialog)
	{
		HideGroup(dialog);
		if (!dialog.IsShowing)
		{
			Game.serv.ui.ShowUIDialog(dialog);
		}
	}

	private void HideGroup(BaseHUDDialog dialog)
	{
		if (dialog.Group != BaseHUDDialog.GroupType.None)
		{
			HideGroup(dialog.Group, dialog);
		}
	}

	public BaseHUDDialog FindShowingInGroup(BaseHUDDialog.GroupType group, BaseHUDDialog skip = null)
	{
		foreach (BaseHUDDialog dialog in _dialogs)
		{
			if (dialog.IsShowing && dialog.Group == group && dialog != skip)
			{
				return dialog;
			}
		}
		return null;
	}

	public void HideGroup(BaseHUDDialog.GroupType group, BaseHUDDialog skip = null)
	{
		BaseHUDDialog baseHUDDialog = FindShowingInGroup(group, skip);
		if (baseHUDDialog != null)
		{
			Hide(baseHUDDialog);
		}
	}

	public void Hide(BaseHUDDialog dialog)
	{
		if (dialog.IsShowing)
		{
			Game.serv.ui.HideUIDialog(dialog);
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.UIHUDDialogClosed, EntityID.INVALID, PlayerID.HumanPlayer, dialog));
		}
	}

	public void Toggle(BaseHUDDialog dialog, bool show)
	{
		if (show && !dialog.IsShowing)
		{
			Show(dialog);
		}
		if (!show && dialog.IsShowing)
		{
			Hide(dialog);
		}
	}

	public void Toggle(BaseHUDDialog dialog)
	{
		Toggle(dialog, !dialog.IsShowing);
	}

	public bool CanClosePopup()
	{
		return Game.serv.ui.TopPopupUnsafe != null;
	}

	public bool CloseNextPopup()
	{
		if (Game.serv.ui.TopPopupUnsafe is BasePopup basePopup)
		{
			basePopup.Close();
			return true;
		}
		return false;
	}

	public bool CanCloseDialog()
	{
		if (GetLastShowingFloatingDialog() == null)
		{
			return FindShowingInGroup(BaseHUDDialog.GroupType.ConvoGroup) != null;
		}
		return true;
	}

	public bool CloseNextDialog()
	{
		BaseHUDDialog lastShowingFloatingDialog = GetLastShowingFloatingDialog();
		if (lastShowingFloatingDialog != null)
		{
			lastShowingFloatingDialog.Hide();
			return true;
		}
		BaseHUDDialog baseHUDDialog = FindShowingInGroup(BaseHUDDialog.GroupType.ConvoGroup);
		if (baseHUDDialog != null)
		{
			Hide(baseHUDDialog);
			return true;
		}
		return false;
	}

	private BaseHUDDialog GetLastShowingFloatingDialog()
	{
		for (int num = _dialogs.Count - 1; num >= 0; num--)
		{
			BaseHUDDialog baseHUDDialog = _dialogs[num];
			if (baseHUDDialog.IsShowing && baseHUDDialog.UIReference.Type == UIType.NonModalFloating)
			{
				return baseHUDDialog;
			}
		}
		return null;
	}
}

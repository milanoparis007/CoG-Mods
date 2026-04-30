using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.AI;
using Game.Session.Sim;
using Game.UI.Mouseovers;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Tickers;

public sealed class TickerBar : BaseHUDDialog
{
	private static class ScopeOutTickerUtil
	{
		internal static TickerIcon FindIcon(BuildingAndBusinessData data)
		{
			if (data.building?.components.building?.IsSafehouse != true)
			{
				return TickerIcon.MakeRaw(BuildingUtil.FindBuildingIcon(data.building));
			}
			return TickerIcon.SAFEHOUSE;
		}

		internal static string FindMessage(BuildingAndBusinessData bbd)
		{
			if (bbd.building.components.building.IsSafehouseNotOf(PlayerID.HumanPlayer))
			{
				return GetSafehouseMessage(bbd.building);
			}
			BizConfig biz = bbd.biz.config.biz;
			BizData biz2 = bbd.biz.data.biz;
			Entity owner = bbd.owner;
			string text = Loc.Get("ui.tickers.scopeout.business.message", "bizname", biz2.bizname, "type", Loc.Get(biz.locname), "ownername", owner.data.person.FullName);
			string text2 = BuildingUtil.DescribeBuildingAtScopeOut(bbd.building);
			if (!string.IsNullOrWhiteSpace(text2))
			{
				text += Loc.Get("ui.tickers.scopeout.business.observations", "recipes", text2);
			}
			return text;
		}

		private static string GetSafehouseMessage(Entity building)
		{
			PlayerID safehouseOwner = building.components.building.SafehouseOwner;
			bool flag = SafehouseUtils.CanRaidSafehouse(building, PlayerID.HumanPlayer);
			bool ofInterest = Game.ctx.players.WithID(safehouseOwner).OfInterest;
			string text = Loc.Get((flag && ofInterest) ? "ui.tickers.safehouse.message.canraid.interest" : (flag ? "ui.tickers.safehouse.message.canraid" : "ui.tickers.safehouse.message.cannotraid"));
			return Loc.Get("ui.tickers.safehouse.message.format", "message", text);
		}
	}

	private GameObject _templates;

	private GameObject _containerBar;

	private GameObject _containerButtons;

	private GameObject _button;

	private RectTransform _containerBarRect;

	private List<BaseTicker> _tickers;

	private List<TickerData> _startupTickers;

	private const float _buttonWidth = 42f;

	private const float _padding = 90f;

	private const float _innerPadding = 0f;

	private const float _buttonSpeed = 0.25f;

	private const float _buttonSlideInHeight = 30f;

	private const float _buttonFinalHeight = 7f;

	private readonly Dictionary<TickerType, Func<BaseTicker>> _factories = new Dictionary<TickerType, Func<BaseTicker>>(new TickerTypeEqualityComparer())
	{
		{
			TickerType.TextPopup,
			() => new TickerText()
		},
		{
			TickerType.CombatResults,
			() => new TickerCombatResults()
		}
	};

	private const string TEMPLATES = "Templates";

	private const string TMPL_TICKER = "Templates/Ticker Button";

	private const string MAIN_CONTAINER = "Container";

	private const string BTN_CONTAINER = "Container/Buttons";

	public override bool ShowAtStartup => true;

	public override TweenType Tween => TweenType.None;

	public override UIReference UIReference => UIElements.TickerBar;

	private float ButtonWidth => 42f * Game.serv.ui.UIScaleFactor;

	private float Padding => 90f * Game.serv.ui.UIScaleFactor;

	private float InnerPadding => 0f * Game.serv.ui.UIScaleFactor;

	private float ButtonSlideInY => 30f * Game.serv.ui.UIScaleFactor;

	private float ButtonFinalY => 7f * Game.serv.ui.UIScaleFactor;

	private float ButtonSpeed => 0.25f;

	internal override void Initialize()
	{
		base.Initialize();
		_templates = _go.GetChild("Templates");
		_templates.SetActive(value: false);
		_button = _go.GetChild("Templates/Ticker Button");
		_containerBar = _go.GetChild("Container");
		_containerBar.SetActive(value: false);
		_containerBarRect = _containerBar.GetComponent<RectTransform>();
		_containerButtons = _go.GetChild("Container/Buttons");
		_containerButtons.transform.DestroyAllChildren();
		_tickers = new List<BaseTicker>();
		_startupTickers = new List<TickerData>();
		Game.serv.mouseovers.Register(MouseoverType.TickerButton, new TickerButtonMouseover());
		Game.ctx.events.AddListener(SessionEventType.ConversationStarted, OnConversationStarted);
		Game.ctx.events.AddListener(SessionEventType.HumanPlayerTurnStarted, OnHumanTurnStarted);
		Game.ctx.events.AddListener(SessionEventType.HumanPlayerTurnEnded, OnHumanTurnEnded);
	}

	internal override void Release()
	{
		Game.ctx.events.RemoveListener(SessionEventType.HumanPlayerTurnEnded, OnHumanTurnEnded);
		Game.ctx.events.RemoveListener(SessionEventType.HumanPlayerTurnStarted, OnHumanTurnStarted);
		Game.ctx.events.RemoveListener(SessionEventType.ConversationStarted, OnConversationStarted);
		Game.serv.mouseovers.Unregister(MouseoverType.TickerButton);
		RemoveAll();
		LeanTween.cancel(_containerBar);
		_tickers = null;
		_startupTickers = null;
		_templates = (_button = (_containerButtons = (_containerBar = null)));
		_containerBarRect = null;
	}

	public void AddTicker(TickerData data)
	{
		if (Game.ctx.IsInteractive)
		{
			AddAndUpdateTicker(data);
			return;
		}
		_startupTickers.Add(data);
		PumpTickerQueue();
	}

	private void PumpTickerQueue()
	{
		if (_startupTickers.Count <= 0 || !Game.ctx.IsInteractive || Game.ctx.clock.CurrentTurn <= 1)
		{
			return;
		}
		foreach (TickerData startupTicker in _startupTickers)
		{
			AddAndUpdateTicker(startupTicker);
		}
		_startupTickers.Clear();
	}

	private void AddAndUpdateTicker(TickerData data)
	{
		Func<BaseTicker> func = _factories.FindOrNullAndWarn(data.type, "Unknown ticker type!");
		if (func != null)
		{
			BaseTicker baseTicker = func();
			GameObject gameObject = UnityEngine.Object.Instantiate(_button, _containerButtons.transform);
			Vector2 vector = new Vector2((float)_tickers.Count * (ButtonWidth + InnerPadding) * 0.5f, 0f);
			gameObject.GetComponent<RectTransform>().anchoredPosition += Vector2.up * ButtonSlideInY + vector;
			baseTicker.Initialize(data, gameObject);
			_tickers.Add(baseTicker);
			_containerBar.SetActive(value: true);
			Game.ctx.simman.businesses.AddEvent(new BusinessTracker.EventHistoryListItem(data));
			UpdateContainerSize();
			UpdateTickerPositions();
			Game.ctx.events.EnqueueOnce(new SessionEvent(SessionEventType.UITickerShown, EntityID.INVALID, PlayerID.HumanPlayer, data));
		}
	}

	private void UpdateContainerSize()
	{
		Vector2 sizeDelta = _containerBarRect.sizeDelta;
		sizeDelta.x = (float)_tickers.Count * ButtonWidth + (float)Math.Max(_tickers.Count - 1, 0) * InnerPadding + Padding * 2f;
		LeanTween.cancel(_containerBar);
		LeanTween.value(_containerBar, OnUpdateContainerSize, _containerBarRect.sizeDelta, sizeDelta, ButtonSpeed);
	}

	private void OnUpdateContainerSize(Vector2 newSize)
	{
		_containerBarRect.sizeDelta = newSize;
	}

	private void UpdateTickerPositions()
	{
		Vector2 vector = (Vector2)_containerBar.transform.position - Vector2.right * ((ButtonWidth + InnerPadding) * ((float)(_tickers.Count - 1) / 2f)) + Vector2.down * ButtonFinalY;
		for (int i = 0; i < _tickers.Count; i++)
		{
			LeanTween.cancel(_tickers[i].go);
			float num = (ButtonWidth + InnerPadding) * (float)i;
			LeanTween.move(_tickers[i].go, vector + Vector2.right * num, ButtonSpeed);
		}
	}

	public void RemoveTicker(TickerData data)
	{
		int num = FindTickerIndex(data);
		if (num >= 0)
		{
			RemoveTickerAt(num);
		}
	}

	private int FindTickerIndex(TickerData data)
	{
		int i = 0;
		for (int count = _tickers.Count; i < count; i++)
		{
			if (_tickers[i].data == data)
			{
				return i;
			}
		}
		return -1;
	}

	private void RemoveTickerAt(int index)
	{
		BaseTicker baseTicker = _tickers[index];
		if (baseTicker == null)
		{
			Logger.Warning("Unknown ticker during removal: " + index);
			return;
		}
		LeanTween.cancel(baseTicker.go);
		UnityEngine.Object.Destroy(baseTicker.go);
		baseTicker.Release();
		_tickers.RemoveAt(index);
		_containerBar.SetActive(_tickers.Count > 0);
		UpdateTickerPositions();
		UpdateContainerSize();
	}

	public void RemoveAll()
	{
		while (_tickers.Count > 0)
		{
			RemoveTickerAt(_tickers.Count - 1);
		}
	}

	private GameObject FindTickerUnderMouse()
	{
		using ListPool<GameObject>.PooledBlockList pooledBlockList = ListPool<GameObject>.Allocate();
		_tickers.SelectInto((BaseTicker t) => t.go, pooledBlockList);
		return UIUtil.FindInputOverUIElementsOrNull(pooledBlockList);
	}

	internal bool IsClickOverTicker()
	{
		return FindTickerUnderMouse() != null;
	}

	internal void HandleRightClick()
	{
		GameObject tickergo = FindTickerUnderMouse();
		BaseTicker baseTicker = _tickers.Find((BaseTicker t) => t.go == tickergo);
		if (baseTicker != null)
		{
			RemoveTicker(baseTicker.data);
		}
	}

	private void OnHumanTurnStarted(SessionEvent _)
	{
		PumpTickerQueue();
	}

	private void OnHumanTurnEnded(SessionEvent _)
	{
		for (int num = _tickers.Count - 1; num >= 0; num--)
		{
			switch (_tickers[num].data.persisted)
			{
			case TickerPersistType.Temporary:
				RemoveTickerAt(num);
				break;
			case TickerPersistType.GoonOfferPersist:
			{
				PlayerAI obj = _tickers[num].FindTarget()?.data.agent.pid.FindPlayer()?.ai;
				if (obj == null || obj.goon?.LootStatus != GoonLootState.Offered)
				{
					RemoveTickerAt(num);
				}
				break;
			}
			case TickerPersistType.CarDamagePersist:
				if ((_tickers[num].FindTarget()?.data.mobile?.health ?? ((Fixnum)0)) >= 50)
				{
					RemoveTickerAt(num);
				}
				break;
			case TickerPersistType.DebtorPersist:
			{
				Entity gambler = _tickers[num].FindTarget();
				GamblerState gamblerState = Game.ctx.players.Human.gambling.FindGamblerState(gambler);
				if (gamblerState == null || !gamblerState.DebtIsDue || gamblerState == null || gamblerState.repaymentInProgress.IsSet)
				{
					RemoveTickerAt(num);
				}
				break;
			}
			case TickerPersistType.PolTutorialPersist:
			{
				int campaignStartMonth = Game.serv.globals.settings.politics.elections.campaignStartMonth;
				if (Game.ctx.clock.Now.ToDate().Month >= campaignStartMonth)
				{
					RemoveTickerAt(num);
				}
				break;
			}
			case TickerPersistType.LegislativePersist:
			{
				int legislationStartMonth = Game.serv.globals.settings.politics.elections.legislationStartMonth;
				if (Game.ctx.clock.Now.ToDate().Month >= legislationStartMonth)
				{
					RemoveTickerAt(num);
				}
				break;
			}
			}
		}
	}

	private void OnConversationStarted(SessionEvent sev)
	{
		if (sev.ctx is VisitState { AtValidBiz: not false } visitState)
		{
			TickerTarget target = visitState.building.Id;
			_tickers.Find((BaseTicker t) => TickerTarget.Equals(t.data.target, target));
		}
	}

	public void AddTextTicker(TickerIcon icon, TickerTitle title, string message, TickerTarget target = default(TickerTarget), TickerPersistType persisted = TickerPersistType.Temporary)
	{
		AddTicker(new TickerData
		{
			type = TickerType.TextPopup,
			icon = icon,
			title = title,
			message = message,
			target = target,
			date = Game.ctx.clock.Now,
			persisted = persisted
		});
	}

	internal void AddLevelupTicker(string message, TickerTarget target)
	{
		AddTicker(new TickerLevelupData
		{
			type = TickerType.TextPopup,
			icon = TickerIcon.LEVELUP,
			title = TickerTitle.LEVELUP,
			message = message,
			target = target,
			date = Game.ctx.clock.Now
		});
	}

	internal void AddSchemeTicker(string message, TickerTarget target)
	{
		AddTicker(new TickerSchemeData
		{
			type = TickerType.TextPopup,
			icon = TickerIcon.SCHEME_UPDATE,
			title = TickerTitle.SCHEME_UPDATE,
			message = message,
			target = target,
			date = Game.ctx.clock.Now
		});
	}

	internal void AddThroneTicker(string message, bool toThrone, bool isUnlock = false)
	{
		AddTicker(new TickerThroneData
		{
			type = TickerType.TextPopup,
			icon = (isUnlock ? TickerIcon.THRONE_UNLOCK : TickerIcon.THRONE),
			title = TickerTitle.THRONE,
			message = message,
			date = Game.ctx.clock.Now,
			toThrone = toThrone
		});
	}

	internal void AddPromoteTicker(string message, RoleDef role)
	{
		AddTicker(new TickerAssignRoleData
		{
			type = TickerType.TextPopup,
			icon = TickerIcon.PROMOTE,
			title = TickerTitle.PROMOTE,
			message = message,
			date = Game.ctx.clock.Now,
			role = role
		});
	}

	internal void AddLawTicker(string message, bool enacted, PoliticsSettings.Law law)
	{
		AddTicker(new TickerLawbookData
		{
			type = TickerType.TextPopup,
			icon = TickerIcon.LAW,
			title = (enacted ? TickerTitle.LAW_ENACTED : TickerTitle.LAW_REVOKED),
			message = message,
			date = Game.ctx.clock.Now
		});
	}

	internal void AddLawbookTicker(string message)
	{
		AddTicker(new TickerLawbookData
		{
			type = TickerType.TextPopup,
			icon = TickerIcon.LAW,
			title = TickerTitle.POLITICS,
			message = message,
			date = Game.ctx.clock.Now,
			persisted = TickerPersistType.LegislativePersist
		});
	}

	internal void AddTickerScopeOut(EntityID buildingId)
	{
		BuildingAndBusinessData buildingAndBusinessData = BuildingUtil.FindDataForBuilding(buildingId);
		TickerIcon icon = ScopeOutTickerUtil.FindIcon(buildingAndBusinessData);
		string message = ScopeOutTickerUtil.FindMessage(buildingAndBusinessData);
		AddTextTicker(icon, TickerTitle.SCOPEOUT, message, buildingId);
	}

	internal void AddTickerCombatResults(CombatSummary summary, List<CombatResults> results)
	{
		TickerCombatResultsData data = new TickerCombatResultsData
		{
			type = TickerType.CombatResults,
			icon = TickerIcon.COMBAT_RESULTS,
			title = TickerTitle.COMBAT_RESULTS,
			message = summary.summary,
			target = summary.corner,
			date = Game.ctx.clock.Now,
			summary = summary,
			results = results
		};
		AddTicker(data);
	}
}

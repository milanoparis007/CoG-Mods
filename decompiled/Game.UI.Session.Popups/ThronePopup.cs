using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Player;
using Game.UI.Mouseovers;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Popups;

public class ThronePopup : BasePopup
{
	public const string TEMPLATES = "Templates";

	public const string BUTTON_CLOSE = "Panel/Close";

	public const string BUTTON_INFO = "Panel/Info";

	public const string THRONE_BACKGROUND = "Panel/Dialog Background Only";

	public const string TROPHIES_CONTAINER = "Panel/Throne Items";

	public const string TROPHY_TEMPLATE = "Templates/Trophy Image";

	public const string TITLE = "Panel/Title";

	public PlayerThrone humanThrone;

	public ThroneSettings Settings;

	private GameObject _trophyContainer;

	private GameObject _template;

	public static readonly Label SKYSCRAPER_ID = new Label("Skyscraper Throne");

	public static readonly Label LODGE_ID = new Label("Lodge Throne");

	public const float FRAC_OF_NATIVE = 0.3667f;

	public override UIReference UIReference => UIElements.ThroneRoomPopup;

	protected override void InitializeOnPush()
	{
		_go.SetActive("Templates", value: false);
		_go.GetButton("Panel/Close").onClick.AddListener(Close);
		_go.GetButton("Panel/Info").onClick.AddListener(OnInfoClick);
		humanThrone = Game.ctx.players.Human.throne;
		Settings = Game.serv.globals.settings.throne;
		_trophyContainer = _go.GetChild("Panel/Throne Items");
		_template = _go.GetChild("Templates/Trophy Image");
		Game.serv.mouseovers.Register(MouseoverType.TrophyInfo, new TrophyMouseover());
	}

	protected override void ReleaseOnPop()
	{
		Game.serv.mouseovers.Unregister(MouseoverType.TrophyInfo);
		_go.GetButton("Panel/Close").onClick.RemoveListener(Close);
		_go.GetButton("Panel/Info").onClick.RemoveListener(OnInfoClick);
		humanThrone = null;
		Settings = null;
		_trophyContainer = null;
		_template = null;
	}

	public override void OnActivated(bool pushed)
	{
		base.OnActivated(pushed);
		RefreshView();
	}

	public override void OnDeactivated(bool popped)
	{
		base.OnDeactivated(popped);
		Game.ctx.players.Human.throne.UpdateSeen();
	}

	public void RefreshView()
	{
		RefreshBackground();
		if (humanThrone.GetThroneStyle().IsSet)
		{
			RefreshTrophies();
		}
	}

	public void RefreshBackground()
	{
		ThroneSettings.ThroneType throneTypeForId = Game.serv.globals.settings.throne.GetThroneTypeForId(humanThrone.GetThroneStyle());
		Sprite throneSprite = humanThrone.GetThroneSprite(throneTypeForId);
		_go.SetImage("Panel/Dialog Background Only", throneSprite);
		_go.SetText("Panel/Title", Loc.Get(throneTypeForId.loctitle));
	}

	public void RefreshTrophies()
	{
		List<IdToSignature> list = new List<IdToSignature>();
		foreach (Trophy trophy in Settings.trophies)
		{
			string signature = humanThrone.GetSignature(trophy.id);
			if (signature != null)
			{
				list.Add(new IdToSignature(trophy.id, signature));
			}
		}
		_trophyContainer.EnsureChildCount(list, _template);
		_trophyContainer.InitializeChildren(list, InitializeTrophy);
	}

	public void InitializeTrophy(int i, GameObject card, IdToSignature pair)
	{
		Trophy trophyForId = Settings.GetTrophyForId(pair.id);
		card.GetImage().alphaHitTestMinimumThreshold = 0.8f;
		RectTransform component = card.GetComponent<RectTransform>();
		Sprite throneSprite = humanThrone.GetThroneSprite(trophyForId);
		card.SetImage(throneSprite);
		card.GetImage().SetNativeSize();
		card.SetUIElementWidth(Mathf.Floor(component.sizeDelta.x * 0.3667f));
		card.SetUIElementHeight(Mathf.Floor(component.sizeDelta.y * 0.3667f));
		Vector2 trophyPosition = humanThrone.GetTrophyPosition(Settings.GetTrophyForId(pair.id));
		component.anchoredPosition = trophyPosition * 0.3667f;
		card.GetOrAddComponent<TrophyCtx>().Set(pair.id, pair.signature);
		card.GetOrAddComponent<TrophyGlow>().Set(!humanThrone.HasSeen(pair.id), Time.realtimeSinceStartup);
	}

	public void OnInfoClick()
	{
		OkPopup.ShowOk(Loc.Get("ui.throne.info"), delegate
		{
		});
	}
}

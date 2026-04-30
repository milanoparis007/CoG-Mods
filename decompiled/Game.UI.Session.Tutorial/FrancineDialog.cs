using Game.Services;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Tutorial;

public sealed class FrancineDialog : BaseHUDDialog
{
	public enum ButtonType
	{
		Continue,
		Understood,
		Agreed,
		Waiting
	}

	public struct TutState
	{
		public ShowData data;

		public Anchor anchor;

		public ButtonType button;

		public bool IsValid => data.key != null;

		public bool IsHint => data?.hint ?? false;
	}

	public enum Anchor
	{
		BCenter,
		BLeft,
		BRight,
		CCenter,
		CLeft,
		CRight,
		TCenter,
		TLeft,
		TRight
	}

	public class ShowData
	{
		public string key;

		public string header;

		public string message;

		public bool hint;

		public Sprite sprite;
	}

	private const string PANEL = "Panel";

	private const string BUTTON_CLOSE = "Panel/Close Button";

	private const string BUTTON_OK = "Panel/Footer/Ok";

	private const string BUTTON_SKIP = "Panel/Footer/Quit";

	private const string PORTRAIT_IMAGE = "Panel/Portrait/Portrait";

	private const string HEADER = "Panel/Header";

	private const string TEXT = "Panel/Text";

	private const string IMAGE = "Panel/Image";

	public TutState tutstate;

	public override bool ShowAtStartup => false;

	public override TweenType Tween => TweenType.Left;

	public override UIReference UIReference => UIElements.FrancineDialog;

	internal override void Initialize()
	{
		base.Initialize();
		_go.SetButtonListener("Panel/Close Button", delegate
		{
			OnClose();
		});
		_go.SetButtonListener("Panel/Footer/Ok", delegate
		{
			OnClose();
		});
		_go.SetButtonListener("Panel/Footer/Quit", delegate
		{
			OnClose(skipLesson: true);
		});
	}

	internal override void Release()
	{
		_go.ClearButtonListeners("Panel/Close Button");
		_go.ClearButtonListeners("Panel/Footer/Ok");
		_go.ClearButtonListeners("Panel/Footer/Quit");
		base.Release();
	}

	public override void Show()
	{
	}

	public void Show(ShowData data, Anchor anchor = Anchor.BCenter, ButtonType button = ButtonType.Continue)
	{
		tutstate = new TutState
		{
			data = data,
			anchor = anchor,
			button = button
		};
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
		Sprite crewSprite = HUDUtil.GetCrewSprite(Game.ctx.players.Human.social.FindFrancine());
		_go.SetImageOrHide("Panel/Portrait/Portrait", crewSprite);
		_go.SetText("Panel/Header", tutstate.data.header);
		_go.SetText("Panel/Text", tutstate.data.message);
		_go.SetImageOrHide("Panel/Image", tutstate.data.sprite);
		bool interactable = tutstate.button != ButtonType.Waiting;
		string text = (tutstate.IsHint ? Loc.Get("tut.fr.button-hint-ok") : ((tutstate.button == ButtonType.Understood) ? Loc.Get("tut.fr.button-understood") : ((tutstate.button == ButtonType.Waiting) ? Loc.Get("tut.fr.button-waiting") : ((tutstate.button == ButtonType.Agreed) ? Loc.Get("tut.fr.button-agreed") : Loc.Get("tut.fr.button-continue")))));
		string text2 = (tutstate.IsHint ? Loc.Get("tut.fr.button-hint-skip") : Loc.Get("tut.fr.button-skip"));
		_go.SetChildText("Panel/Footer/Ok", text);
		_go.SetChildText("Panel/Footer/Quit", text2);
		_go.GetButton("Panel/Footer/Ok").interactable = interactable;
		_go.GetButton("Panel/Close Button").interactable = interactable;
		SetAnchor(tutstate.anchor);
		_go.transform.SetAsLastSibling();
	}

	private void SetAnchor(Anchor anchor)
	{
		Vector2 vector = new Vector2(1f, 0f);
		Vector2 vector2 = new Vector2(1f, 0f);
		Vector2 anchoredPosition = new Vector2(0f, 0f);
		switch (anchor)
		{
		case Anchor.BCenter:
			vector2 = new Vector2(0.5f, 0f);
			vector = vector2;
			anchoredPosition = new Vector2(0f, 20f);
			break;
		case Anchor.BLeft:
			vector2 = new Vector2(0f, 0f);
			vector = vector2;
			anchoredPosition = new Vector2(20f, 20f);
			break;
		case Anchor.BRight:
			vector2 = new Vector2(1f, 0f);
			vector = vector2;
			anchoredPosition = new Vector2(-20f, 20f);
			break;
		case Anchor.CLeft:
			vector2 = new Vector2(0f, 0.5f);
			vector = vector2;
			anchoredPosition = new Vector2(20f, 0f);
			break;
		case Anchor.CRight:
			vector2 = new Vector2(1f, 0.5f);
			vector = vector2;
			anchoredPosition = new Vector2(-20f, 0f);
			break;
		case Anchor.CCenter:
			vector2 = new Vector2(0.5f, 0.5f);
			vector = vector2;
			anchoredPosition = new Vector2(0f, 0f);
			break;
		case Anchor.TCenter:
			vector2 = new Vector2(0.5f, 1f);
			vector = vector2;
			anchoredPosition = new Vector2(0f, -20f);
			break;
		case Anchor.TLeft:
			vector2 = new Vector2(0f, 1f);
			vector = vector2;
			anchoredPosition = new Vector2(20f, -20f);
			break;
		case Anchor.TRight:
			vector2 = new Vector2(1f, 1f);
			vector = vector2;
			anchoredPosition = new Vector2(-20f, -20f);
			break;
		}
		RectTransform rect = _go.GetRect();
		Vector2 anchorMin = (rect.anchorMax = vector);
		rect.anchorMin = anchorMin;
		RectTransform rect2 = _go.GetChild("Panel").GetRect();
		rect2.pivot = vector2;
		rect2.anchoredPosition = anchoredPosition;
	}

	private void OnClose(bool skipLesson = false)
	{
		if (skipLesson)
		{
			if (tutstate.IsHint)
			{
				Game.ctx.tutorial.DontShowHintAgain(tutstate.data.key);
			}
			else
			{
				Game.ctx.tutorial.SkipCurrentLesson();
			}
		}
		Hide();
	}

	protected override void OnBeforeHide()
	{
		tutstate = default(TutState);
		base.OnBeforeHide();
	}
}

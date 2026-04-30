using Game.Core;
using Game.Services;
using Game.UI.Util;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI.Session;

public abstract class BaseHUDDialog : IUIDialog
{
	public enum Showing
	{
		Hidden,
		TweeningToShow,
		Showing,
		TweeningToHide
	}

	public enum TweenType
	{
		None,
		Down,
		Up,
		Left,
		Right
	}

	public enum GroupType
	{
		None,
		ConvoGroup
	}

	protected class MouseoverHider : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler
	{
		public void OnPointerEnter(PointerEventData eventData)
		{
			Game.ctx.selection.ClearFocus();
		}
	}

	private const float TWEEN_TIME = 0.1f;

	protected GameObject _go;

	protected RectTransform _rect;

	protected TweenType _tween;

	protected Vector2 _closedpos;

	protected Vector2 _openpos;

	public abstract bool ShowAtStartup { get; }

	public abstract TweenType Tween { get; }

	public abstract UIReference UIReference { get; }

	public virtual GroupType Group => GroupType.None;

	public Showing State { get; protected set; }

	public bool IsVisible => State != Showing.Hidden;

	public bool IsShowing
	{
		get
		{
			if (State != Showing.Showing)
			{
				return State == Showing.TweeningToShow;
			}
			return true;
		}
	}

	public PlayerID UIPlayer => PlayerID.HumanPlayer;

	protected virtual void OnBeforeShow()
	{
	}

	protected virtual void OnAfterShow()
	{
	}

	protected virtual void OnBeforeHide()
	{
	}

	protected virtual void OnAfterHide()
	{
	}

	protected virtual void RefreshContents()
	{
	}

	internal virtual void UpdateAnimations(GameAnimUpdate anim)
	{
	}

	internal virtual void Initialize()
	{
		_go = Game.serv.ui.GetUI(UIReference);
		_rect = _go.GetComponent<RectTransform>();
		_openpos = _rect.anchoredPosition;
		_closedpos = _openpos - FindTweenDelta(Tween, _rect);
		_rect.anchoredPosition = ((Tween == TweenType.None) ? _openpos : _closedpos);
		_go.AddComponent<MouseoverHider>();
		UISoundUtil.AttachSFXToAllClickables(_go);
	}

	internal virtual void Release()
	{
		Hide();
		Object.Destroy(_go.GetComponent<MouseoverHider>());
		LeanTween.cancel(_go);
		_rect.anchoredPosition = _openpos;
		_rect = null;
		_go = null;
	}

	public virtual void RequestShow(UIService service)
	{
		_go.SetActive(value: true);
		OnBeforeShow();
		RefreshContents();
		if (Tween == TweenType.None)
		{
			OnShowingFinished();
			return;
		}
		State = Showing.TweeningToShow;
		LeanTween.cancel(_go);
		LeanTween.value(_go, delegate(Vector2 pos)
		{
			_rect.anchoredPosition = pos;
		}, _rect.anchoredPosition, _openpos, 0.1f).setOnComplete(OnShowingFinished).setEase(LeanTweenType.easeOutCubic);
	}

	private void OnShowingFinished()
	{
		State = Showing.Showing;
		OnAfterShow();
	}

	public virtual void RequestsHide(UIService service)
	{
		OnBeforeHide();
		if (Tween == TweenType.None || Game.ctx.IsPreReleaseDone)
		{
			OnHidingFinished();
			return;
		}
		State = Showing.TweeningToHide;
		LeanTween.cancel(_go);
		LeanTween.value(_go, delegate(Vector2 pos)
		{
			_rect.anchoredPosition = pos;
		}, _rect.anchoredPosition, _closedpos, 0.1f).setOnComplete(OnHidingFinished).setEase(LeanTweenType.easeOutCubic);
	}

	private void OnHidingFinished()
	{
		State = Showing.Hidden;
		_go.SetActive(value: false);
		OnAfterHide();
	}

	public virtual void Show()
	{
		Game.ctx.hud.Show(this);
	}

	public virtual void Hide()
	{
		Game.ctx.hud.Hide(this);
	}

	public virtual void Toggle()
	{
		Game.ctx.hud.Toggle(this);
	}

	private static Vector2 FindTweenDelta(TweenType tween, RectTransform rect)
	{
		return tween switch
		{
			TweenType.Left => new Vector2(0f - rect.sizeDelta.x, 0f), 
			TweenType.Right => new Vector2(rect.sizeDelta.x, 0f), 
			TweenType.Up => new Vector2(0f, rect.sizeDelta.y), 
			TweenType.Down => new Vector2(0f, 0f - rect.sizeDelta.y), 
			_ => Vector2.zero, 
		};
	}
}

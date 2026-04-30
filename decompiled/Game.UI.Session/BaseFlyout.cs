using Game.Core;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session;

public abstract class BaseFlyout
{
	internal UIFlyoutName type;

	internal GameObject go;

	internal WorldPos wstart;

	internal Vector2 sdelta;

	internal float duration;

	internal float p;

	public bool IsExpired => p >= 1f;

	public virtual void OnAfterCreate()
	{
	}

	public virtual void Reset()
	{
	}

	public virtual void OnBeforeDestroy()
	{
	}

	public float GetProgress()
	{
		return p;
	}

	public void Start(WorldPos pos, float seconds, float sdelta)
	{
		wstart = pos;
		this.sdelta = new Vector2(0f, sdelta);
		duration = seconds;
		p = 0f;
		LeanTween.value(go, SetPositionHelper, 0f, 1f, duration).setEase(LeanTweenType.easeInCubic).onComplete = ForceExpire;
		SetPositionHelper(p);
	}

	public void StopAnimation()
	{
		LeanTween.cancel(go);
	}

	private void SetPositionHelper(float fraction)
	{
		p = MathUtil.Clamp(fraction, 0f, 1f);
		Vector2 anchoredPosition = Game.serv.camera.WorldToScreenPos(wstart) / Game.serv.ui.UIScaleFactor + sdelta.Scale(p);
		go.GetComponent<RectTransform>().anchoredPosition = anchoredPosition;
	}

	public void ForceExpire()
	{
		StopAnimation();
		Game.ctx.hud.flyouts.FreeExpiredFlyout(this);
	}
}

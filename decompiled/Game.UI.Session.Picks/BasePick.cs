using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Picks;

public abstract class BasePick
{
	public GameObject go;

	public RectTransform gorect;

	public bool? active;

	public bool hasPointer;

	public abstract PickType Type { get; }

	public PickTarget Target { get; private set; }

	public bool IsShowing
	{
		get
		{
			if (active.HasValue)
			{
				return active.Value;
			}
			return false;
		}
	}

	protected virtual bool CanShowPick()
	{
		return true;
	}

	public void SetPositionAndVisibility(Vector2 anchoredPos, bool show)
	{
		bool num = !active.HasValue;
		show = show && CanShowPick();
		if (active != show)
		{
			active = show;
			go.SetActive(show);
		}
		if (num || show)
		{
			gorect.anchoredPosition = anchoredPos;
		}
	}

	public virtual void SetTarget(PickTarget target)
	{
		Target = target;
		go.GetOrAddComponent<BasePickMouseTrigger>().pick = this;
	}

	public virtual void Reset()
	{
		BasePickMouseTrigger component = go.GetComponent<BasePickMouseTrigger>();
		if (component != null)
		{
			component.pick = null;
			Object.Destroy(component);
		}
		go.SetActive(value: false);
		Target = default(PickTarget);
		active = null;
		hasPointer = false;
	}

	public abstract void RefreshContents();

	public abstract void OnClick();

	public abstract string MakeMouseoverMessage();

	public virtual Vector3 MakeSceneVector()
	{
		return Game.serv.camera.WorldToSceneVector(Target.FindEntity().data.board.worldpos);
	}

	public virtual Vector2 MakeScreenVector()
	{
		Vector3 scene = MakeSceneVector();
		return Game.serv.camera.SceneToScreenPos(scene) / Game.serv.ui.UIScaleFactor;
	}

	public virtual void OnPointerEnterExit(bool onPointerEnter)
	{
		hasPointer = onPointerEnter;
		if (onPointerEnter)
		{
			PushToFront();
		}
	}

	public void PushToFront()
	{
		go.transform.SetAsLastSibling();
		Game.ctx.hud.picks.OnPickSortingChanged();
	}
}

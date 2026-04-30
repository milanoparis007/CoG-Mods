using UnityEngine;

namespace Game.UI.Mouseovers;

public abstract class BaseMouseover
{
	internal GameObject go;

	internal GameObject context;

	public bool Created => go != null;

	public bool Showing
	{
		get
		{
			if (go != null)
			{
				return go.activeSelf;
			}
			return false;
		}
	}

	public abstract UIMouseoverName MouseoverAssetName { get; }

	public abstract void RefreshContents();

	public virtual void OnAfterCreate(GameObject go)
	{
		this.go = go;
	}

	public virtual void OnBeforeDestroy()
	{
		go = null;
	}

	public virtual void OnShow()
	{
	}

	public virtual void OnHide()
	{
	}

	internal virtual void SetContext(GameObject context)
	{
		this.context = context;
	}

	internal virtual void SetScreenPosition(Vector2 screenpos)
	{
		RectTransform component = go.GetComponent<RectTransform>();
		component.anchoredPosition = screenpos;
		Rect rect = component.rect;
		float width = go.transform.parent.GetComponent<RectTransform>().rect.width;
		float num = screenpos.x + rect.xMax;
		float num2 = screenpos.y + rect.yMin;
		if (num > width)
		{
			component.anchoredPosition += new Vector2(width - num, 0f);
		}
		if (num2 < 0f)
		{
			component.anchoredPosition += new Vector2(0f, 0f - num2);
		}
	}
}

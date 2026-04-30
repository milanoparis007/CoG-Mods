using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI.Mouseovers;

[AddComponentMenu("Game UI/Mouseover Trigger")]
public class MouseoverTrigger : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler
{
	public MouseoverType type;

	public MouseoverAnchor anchor = MouseoverAnchor.GameObject;

	public Vector2 anchorOffset = new Vector2(0f, 0f);

	protected Vector2? _lastpos;

	public bool showing { get; private set; }

	public void OnPointerEnter(PointerEventData eventData)
	{
		showing = true;
		Refresh();
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		Game.serv.mouseovers?.OnMouseOut(type);
		showing = false;
		_lastpos = null;
	}

	public void OnDisable()
	{
		if (showing)
		{
			OnPointerExit(null);
		}
	}

	public void Update()
	{
		if (showing && anchor == MouseoverAnchor.Mouse)
		{
			Refresh();
		}
	}

	public virtual void Refresh()
	{
		Vector2 vector = FindPosition();
		if (!_lastpos.HasValue || !(_lastpos.Value == vector))
		{
			_lastpos = vector;
			Game.serv.mouseovers.OnMouseOverOrMove(type, vector, base.gameObject);
		}
	}

	protected Vector2 FindPosition()
	{
		Vector3 vector = Input.mousePosition;
		switch (anchor)
		{
		case MouseoverAnchor.GameObject:
			vector = (Vector2)base.gameObject.transform.position;
			break;
		default:
			Logger.Warning("Unexpected mouse over anchor: " + anchor);
			break;
		case MouseoverAnchor.Mouse:
			break;
		}
		vector = ScreenPosToMouseoverPos(vector, anchorOffset);
		return vector;
	}

	public static Vector2 ScreenPosToMouseoverPos(Vector2 pos, Vector2 anchorOffset)
	{
		float uIScaleFactor = Game.serv.ui.UIScaleFactor;
		pos = new Vector2(pos.x / uIScaleFactor, pos.y / uIScaleFactor);
		pos = new Vector2(pos.x + anchorOffset.x, pos.y + anchorOffset.y);
		return pos;
	}
}

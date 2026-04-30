using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HoverEventButton : Button
{
	public ButtonPointerEvent onPointerEnter = new ButtonPointerEvent();

	public ButtonPointerEvent onPointerExit = new ButtonPointerEvent();

	public override void OnPointerEnter(PointerEventData eventData)
	{
		base.OnPointerEnter(eventData);
		onPointerEnter.Invoke();
	}

	public override void OnPointerExit(PointerEventData eventData)
	{
		base.OnPointerExit(eventData);
		onPointerExit.Invoke();
	}
}

using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI.Session.Picks;

public class BasePickMouseTrigger : MonoBehaviour, IPointerEnterHandler, IEventSystemHandler, IPointerExitHandler
{
	public bool showing;

	public BasePick pick;

	public void OnPointerEnter(PointerEventData eventData)
	{
		showing = true;
		pick.OnPointerEnterExit(showing);
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		showing = false;
		pick.OnPointerEnterExit(showing);
	}
}

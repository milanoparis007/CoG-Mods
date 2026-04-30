using System.Collections.Generic;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI.Util;

[AddComponentMenu("Game UI/Ignore Mouse")]
public class IgnoreMouse : MonoBehaviour
{
	public bool IgnoreScroll = true;

	private static List<RaycastResult> _tmp_rays = new List<RaycastResult>(16);

	public void SetIgnore(bool ignoreScroll)
	{
		IgnoreScroll = ignoreScroll;
	}

	public static bool CheckIfAllUIElementsIgnoreScroll()
	{
		PointerEventData eventData = new PointerEventData(EventSystem.current)
		{
			pointerId = -1,
			position = Input.mousePosition
		};
		EventSystem.current.RaycastAll(eventData, _tmp_rays);
		foreach (RaycastResult tmp_ray in _tmp_rays)
		{
			IgnoreMouse componentInObjectOrParents = tmp_ray.gameObject.GetComponentInObjectOrParents<IgnoreMouse>();
			if (!(componentInObjectOrParents != null) || !componentInObjectOrParents.IgnoreScroll)
			{
				return false;
			}
		}
		return true;
	}
}

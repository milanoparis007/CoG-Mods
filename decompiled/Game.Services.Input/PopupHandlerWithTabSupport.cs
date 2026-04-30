using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Services.Input;

public class PopupHandlerWithTabSupport : BasicKeyboardHandler
{
	public PopupHandlerWithTabSupport()
		: base(Priority.HighestModalDialog, new List<KeyInput>
		{
			new KeyInput(KeyCode.Tab, SelectNextUI)
		}, Fallthrough.OnlyIfNotProcessed)
	{
	}

	private static void SelectNextUI()
	{
		EventSystem current = EventSystem.current;
		if (current.currentSelectedGameObject == null)
		{
			return;
		}
		Selectable component = current.currentSelectedGameObject.GetComponent<Selectable>();
		if (component == null)
		{
			return;
		}
		Selectable selectable = (KeyUtil.IsShiftDown ? component.FindSelectableOnLeft() : component.FindSelectableOnRight());
		if (!(selectable == null))
		{
			TMP_InputField component2 = selectable.GetComponent<TMP_InputField>();
			if (!(component2 == null))
			{
				component2.OnPointerClick(new PointerEventData(current));
				current.SetSelectedGameObject(selectable.gameObject, new BaseEventData(current));
			}
		}
	}
}

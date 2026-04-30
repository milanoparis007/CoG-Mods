using System;
using Game.Session.Assets;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Services.Audio;

public class PlayUISound : MonoBehaviour, IPointerDownHandler, IEventSystemHandler
{
	public SFXType EffectCategory = SFXType.ClickButton;

	public Func<PointerEventData, SFXType> EffectGenerator;

	public void OnPointerDown(PointerEventData eventData)
	{
		SFXType fx = EffectGenerator?.Invoke(eventData) ?? EffectCategory;
		if (IsEnabled())
		{
			Game.serv.audio.PlayUISFX(fx);
		}
	}

	private bool IsEnabled()
	{
		Button component = GetComponent<Button>();
		if (component != null && !component.interactable)
		{
			return false;
		}
		Toggle component2 = GetComponent<Toggle>();
		if (component2 != null && !component2.interactable)
		{
			return false;
		}
		return true;
	}

	public void SetEffectGenerator(Func<PointerEventData, SFXType> gen)
	{
		EffectGenerator = gen;
		EffectCategory = SFXType.None;
	}

	public static void UpdateEffectOn(GameObject go, string childname, SFXType type)
	{
		PlayUISound componentInChildren = go.GetChild(childname).GetComponentInChildren<PlayUISound>();
		if (componentInChildren != null)
		{
			componentInChildren.EffectCategory = type;
		}
	}
}

using Game.Services;
using TMPro;
using UnityEngine;

namespace Game.UI.Util;

[RequireComponent(typeof(TextMeshProUGUI))]
[AddComponentMenu("Game UI/Localize Text")]
public class LocalizeText : MonoBehaviour
{
	public string lockey;

	private void Awake()
	{
		Refresh();
	}

	public void Refresh()
	{
		TextMeshProUGUI component = GetComponent<TextMeshProUGUI>();
		string text = Loc.Get(lockey);
		component.text = text;
	}
}

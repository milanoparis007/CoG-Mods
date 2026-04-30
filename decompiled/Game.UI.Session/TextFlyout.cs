using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session;

public class TextFlyout : BaseFlyout
{
	public void Set(string text, Color? color)
	{
		go.SetActive(value: true);
		if (color.HasValue)
		{
			text = TextUtil.ColorWrap(text, color.Value);
		}
		go.GetText("Text").text = text;
	}
}

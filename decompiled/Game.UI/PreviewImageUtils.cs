using UnityEngine;
using UnityEngine.UI;

namespace Game.UI;

public static class PreviewImageUtils
{
	public static void HidePreview(RawImage preview, GameObject border, bool deleteOldTexture)
	{
		Texture texture = preview.texture;
		preview.color = new Color(0f, 0f, 0f, 0f);
		preview.texture = null;
		if (deleteOldTexture && texture != null)
		{
			Object.Destroy(texture);
		}
		border.SetActive(value: false);
	}

	public static void ShowPreview(Texture2D texture, RawImage preview, RectTransform mask, GameObject border)
	{
		preview.color = Color.white;
		preview.texture = texture;
		float num = texture.width;
		float num2 = texture.height;
		float num3 = mask.rect.width / num;
		float num4 = mask.rect.height / num2;
		float num5 = ((num3 > num4) ? num3 : num4);
		preview.GetComponent<RectTransform>().sizeDelta = new Vector2(num * num5, num2 * num5);
		border.SetActive(value: true);
	}
}

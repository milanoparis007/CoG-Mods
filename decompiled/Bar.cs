using UnityEngine;
using UnityEngine.UI;

public class Bar : MonoBehaviour
{
	public Image image;

	public Button button;

	private void Start()
	{
	}

	public void SetColor(Color color)
	{
		image.color = color;
	}
}

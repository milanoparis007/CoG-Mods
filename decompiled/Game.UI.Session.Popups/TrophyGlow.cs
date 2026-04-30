using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.Popups;

public class TrophyGlow : MonoBehaviour
{
	public bool doGlow;

	public float startTime;

	public void Update()
	{
		float num = Time.realtimeSinceStartup - startTime;
		if (doGlow && num <= 3.55f)
		{
			float a = 0.5f + Mathf.Abs(Mathf.Sin(4f * num)) * 0.5f;
			base.gameObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, a);
		}
		else
		{
			base.gameObject.GetComponent<Image>().color = new Color(1f, 1f, 1f);
		}
	}

	public void Set(bool shouldGlow, float startTime)
	{
		doGlow = shouldGlow;
		this.startTime = startTime;
	}
}

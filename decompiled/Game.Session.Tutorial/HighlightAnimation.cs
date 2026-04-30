using System;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Tutorial;

public sealed class HighlightAnimation : MonoBehaviour
{
	public GameObject target;

	public Vector2 startSize = Vector2.zero;

	public float startTime;

	public float speedMultiplier = 3f;

	public float dSizeMultiplier = 10f;

	public float dSizeDelta = 8f;

	public float resizeTime = 0.25f;

	public void SetTarget(GameObject target)
	{
		this.target = target;
	}

	private void Start()
	{
		startTime = Time.realtimeSinceStartup;
		SetPosition();
		SetInitialSize();
	}

	private void SetPosition()
	{
		RectTransform rect = target.GetRect();
		Vector2 center = rect.rect.center;
		Vector3 position = rect.TransformPoint(center);
		base.transform.position = position;
	}

	private void SetInitialSize()
	{
		Vector2 size = target.GetRect().rect.size;
		if (startSize.x != 0f)
		{
			size.x = startSize.x;
		}
		if (startSize.y != 0f)
		{
			size.y = startSize.y;
		}
		base.gameObject.GetRect().sizeDelta = (startSize = size);
	}

	private void Update()
	{
		if (!(target == null))
		{
			SetPosition();
			float num = Time.realtimeSinceStartup - startTime;
			float num2 = (float)Math.Cos(num * speedMultiplier);
			float num3 = num2 * num2 * dSizeMultiplier + dSizeDelta;
			Vector2 vector = new Vector2(startSize.x + num3, startSize.y + num3);
			if (num <= resizeTime)
			{
				Vector2 a = new Vector2(startSize.x * 1.2f, startSize.y * 0f);
				float t = num / resizeTime;
				vector = Vector2.Lerp(a, vector, t);
			}
			base.gameObject.GetRect().sizeDelta = vector;
		}
	}
}

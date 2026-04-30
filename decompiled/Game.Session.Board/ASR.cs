using System.Collections.Generic;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Board;

public struct ASR
{
	public float start;

	public float end;

	public ASR(IntRange range)
	{
		start = range.from;
		end = range.to;
	}

	public ASR(FloatRange range)
	{
		start = range.from;
		end = range.to;
	}

	public static void GetValues(float input, float min, float max, float slopeWidth, List<ASR> asrs, List<float> outputs, out int currentSeason)
	{
		int count = asrs.Count;
		currentSeason = 0;
		for (int i = 0; i < count; i++)
		{
			ASR aSR = asrs[i];
			if (aSR.start < aSR.end)
			{
				float value = 0f;
				int num = -1;
				float num2 = aSR.start;
				float num3 = aSR.start + slopeWidth;
				float num4 = aSR.end - slopeWidth;
				float num5 = aSR.end;
				if (input < num2 || input > num5)
				{
					value = 0f;
				}
				else if (input >= num2 && input < num3)
				{
					float num6 = input - num2;
					float num7 = num3 - num2;
					value = Mathf.Lerp(0f, 1f, num6 / num7);
				}
				else if (input >= num3 && input < num4)
				{
					value = 1f;
					num = i;
				}
				else if (input >= num4 && input < num5)
				{
					float num8 = input - num4;
					float num9 = num5 - num4;
					value = Mathf.Lerp(0f, 1f, 1f - num8 / num9);
					num = i;
				}
				outputs[i] = value;
				if (num != -1)
				{
					currentSeason = num;
				}
			}
			else
			{
				if (!(aSR.end < aSR.start))
				{
					continue;
				}
				float value2 = 0f;
				int num10 = -1;
				if (input < aSR.start && input > aSR.end)
				{
					value2 = 0f;
				}
				else
				{
					float num11 = aSR.start;
					float num12 = aSR.start + slopeWidth;
					float num13 = aSR.end + max;
					float num14 = num13 - slopeWidth;
					if (input >= min && input <= aSR.end)
					{
						input += max;
					}
					if (input < num11 || input > num13)
					{
						value2 = 0f;
					}
					else if (input >= num11 && input < num12)
					{
						float num15 = input - num11;
						float num16 = num12 - num11;
						value2 = Mathf.Lerp(0f, 1f, num15 / num16);
					}
					else if (input >= num12 && input < num14)
					{
						value2 = 1f;
						num10 = i;
					}
					else if (input >= num14 && input < num13)
					{
						float num17 = input - num14;
						float num18 = num13 - num14;
						value2 = Mathf.Lerp(0f, 1f, 1f - num17 / num18);
						num10 = i;
					}
				}
				outputs[i] = value2;
				if (num10 != -1)
				{
					currentSeason = num10;
				}
			}
		}
	}
}

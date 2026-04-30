using System.Collections.Generic;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Assets;

public class VertexPaletteData
{
	public float groupIndex;

	public Matrix4x4 mat0;

	public Matrix4x4 mat1;

	public const int TOTAL_WIDTH = 32;

	public static void InitializeGlobals()
	{
		Shader.SetGlobalFloat("_PaletteGroupCount", 4f);
		Shader.SetGlobalInt("_Focus_ID", -1);
		Shader.SetGlobalInt("_Active_ID", -1);
	}

	public void ConsumeCategoryList(List<VertexPaletteCategoryInfo> info)
	{
		mat0 = Matrix4x4.zero;
		mat1 = Matrix4x4.zero;
		foreach (VertexPaletteCategoryInfo item in info)
		{
			float value = 1f / 32f * (float)item.value;
			int[] indices = item.indices;
			foreach (int num in indices)
			{
				if (num <= 15)
				{
					mat0[num, 0] = value;
				}
				else
				{
					mat1[num - 16] = value;
				}
			}
		}
		mat0 = mat0.transpose;
		mat1 = mat1.transpose;
	}

	public void GenerateNewValues(IRandom rng)
	{
		float randomValue = GetRandomValue(8, 32, rng);
		float randomValue2 = GetRandomValue(7, 32, rng);
		float randomValue3 = GetRandomValue(10, 32, rng);
		float randomValue4 = GetRandomValue(20, 32, rng);
		float randomValue5 = GetRandomValue(7, 32, rng);
		float randomValue6 = GetRandomValue(3, 32, rng);
		float randomValue7 = GetRandomValue(3, 32, rng);
		mat0.m00 = GetRandomValue(1, 32, rng);
		mat0.m01 = randomValue;
		mat0.m02 = randomValue;
		mat0.m03 = GetRandomValue(3, 32, rng);
		mat0.m10 = randomValue2;
		mat0.m11 = randomValue2;
		mat0.m12 = randomValue2;
		mat0.m13 = GetRandomValue(6, 32, rng);
		mat0.m20 = randomValue3;
		mat0.m21 = randomValue3;
		mat0.m22 = GetRandomValue(5, 32, rng);
		mat0.m23 = randomValue4;
		mat0.m30 = randomValue4;
		mat0.m31 = randomValue4;
		mat0.m32 = GetRandomValue(7, 32, rng);
		mat0.m33 = randomValue5;
		mat1.m00 = randomValue5;
		mat1.m01 = randomValue5;
		mat1.m02 = GetRandomValue(6, 32, rng);
		mat1.m03 = GetRandomValue(3, 32, rng);
		mat1.m10 = randomValue6;
		mat1.m11 = randomValue6;
		mat1.m12 = GetRandomValue(5, 32, rng);
		mat1.m13 = GetRandomValue(4, 32, rng);
		mat1.m20 = randomValue7;
		mat1.m21 = GetRandomValue(3, 32, rng);
		mat1.m22 = GetRandomValue(3, 32, rng);
		mat1.m23 = randomValue7;
		mat1.m30 = GetRandomValue(1, 32, rng);
		mat1.m31 = GetRandomValue(1, 32, rng);
		mat1.m32 = GetRandomValue(1, 32, rng);
		mat1.m33 = GetRandomValue(1, 32, rng);
	}

	private float GetRandomValue(int numVariants, int totalWidth, IRandom rng)
	{
		return 1f / (float)totalWidth * (float)(numVariants - 1) * rng.GenerateFloat();
	}

	public void SetPropertyBlock(ref MaterialPropertyBlock block)
	{
		block.SetFloat("_GroupIndex", groupIndex);
		block.SetMatrix("_Matrix0", mat0);
		block.SetMatrix("_Matrix1", mat1);
	}
}

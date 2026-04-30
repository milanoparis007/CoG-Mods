using SomaSim.Util;
using UnityEngine;

namespace Game.Core;

public abstract class Heatmap
{
	public sealed class Config
	{
		public readonly bool clearBeforeUpdate = true;

		public bool updateDuringInteractive = true;

		public float clearValue;

		public HeatmapConvolutionKernel convoKernel;

		public Color baseColor = Color.white;

		public Color fillColor = Color.green;
	}

	public Label tag;

	public HeatmapSize heatmapSize;

	public IntSize cellSize;

	public float[,] data;

	private float[,] _temp;

	public Config config;

	public abstract HeatmapType Type { get; }

	public abstract Config MakeConfig();

	public Heatmap Allocate(Label tag, IntSize cellSize)
	{
		this.tag = tag;
		this.cellSize = cellSize;
		IntSize mapSize = Game.ctx.session.mapconfig.map.mapSize;
		heatmapSize = new HeatmapSize(Mathf.CeilToInt((float)mapSize.width / (float)cellSize.width), Mathf.CeilToInt((float)mapSize.height / (float)cellSize.height));
		data = new float[heatmapSize.width, heatmapSize.height];
		_temp = new float[heatmapSize.width, heatmapSize.height];
		config = MakeConfig();
		return this;
	}

	public HeatmapPos WorldToHeatmap(WorldPos wpos)
	{
		return new HeatmapPos((int)(wpos.x / (float)cellSize.width), (int)(wpos.y / (float)cellSize.height));
	}

	public IntSize HeatmapToMapSize(HeatmapSize hsize)
	{
		return new IntSize(hsize.width * cellSize.width, hsize.height * cellSize.height);
	}

	public float GetValueFast(WorldPos pos)
	{
		return GetValueFast(WorldToHeatmap(pos));
	}

	public float GetValueFast(HeatmapPos pos)
	{
		return data[pos.x, pos.y];
	}

	public float GetValueSafe(WorldPos pos)
	{
		return GetValueSafe(WorldToHeatmap(pos));
	}

	public float GetValueSafe(HeatmapPos pos)
	{
		if (pos.x < 0 || pos.x >= heatmapSize.width || pos.y < 0 || pos.y >= heatmapSize.height)
		{
			return 0f;
		}
		return data[pos.x, pos.y];
	}

	public void UpdateCityGen(int times)
	{
		for (int i = 0; i < times; i++)
		{
			bool clear = config.clearBeforeUpdate && i == 0;
			UpdateOnce(clear);
		}
	}

	public void UpdateInteractive()
	{
		if (config.updateDuringInteractive)
		{
			UpdateOnce(config.clearBeforeUpdate);
		}
	}

	private void UpdateOnce(bool clear)
	{
		if (clear)
		{
			Clear();
		}
		PopulateHeatmap();
		if (config.convoKernel != null)
		{
			RunConvolution();
		}
	}

	protected void Clear()
	{
		float clearValue = config.clearValue;
		int width = heatmapSize.width;
		int height = heatmapSize.height;
		for (int i = 0; i < width; i++)
		{
			for (int j = 0; j < height; j++)
			{
				data[i, j] = clearValue;
			}
		}
	}

	protected virtual void PopulateHeatmap()
	{
	}

	protected void RunConvolution()
	{
		float[] values = config.convoKernel.values;
		int i = 1;
		for (int num = heatmapSize.width - 1; i < num; i++)
		{
			int j = 1;
			for (int num2 = heatmapSize.height - 1; j < num2; j++)
			{
				float num3 = 0f;
				num3 += values[0] * data[i - 1, j - 1];
				num3 += values[1] * data[i - 1, j];
				num3 += values[2] * data[i - 1, j + 1];
				num3 += values[3] * data[i, j - 1];
				num3 += values[4] * data[i, j];
				num3 += values[5] * data[i, j + 1];
				num3 += values[6] * data[i + 1, j - 1];
				num3 += values[7] * data[i + 1, j];
				num3 += values[8] * data[i + 1, j + 1];
				_temp[i, j] = num3;
			}
		}
		Array2DCopy(_temp, data, heatmapSize);
	}

	internal static void Array2DCopy(float[,] source, float[,] destination, HeatmapSize size)
	{
		int i = 1;
		for (int num = size.width - 1; i < num; i++)
		{
			int j = 1;
			for (int num2 = size.height - 1; j < num2; j++)
			{
				destination[i, j] = source[i, j];
			}
		}
	}

	protected void AddToHeatmap(HeatmapPos hpos, float delta)
	{
		float num = data[hpos.x, hpos.y] + delta;
		data[hpos.x, hpos.y] = ((num > 1f) ? 1f : ((num < 0f) ? 0f : num));
	}

	protected void AddToHeatmap(WorldPos wpos, float delta)
	{
		HeatmapPos heatmapPos = WorldToHeatmap(wpos);
		float num = data[heatmapPos.x, heatmapPos.y] + delta;
		data[heatmapPos.x, heatmapPos.y] = ((num > 1f) ? 1f : ((num < 0f) ? 0f : num));
	}

	protected void AddToHeatmapAroundNode(WorldPos pos, float delta, int spread = 0)
	{
		HeatmapPos heatmapPos = WorldToHeatmap(pos);
		int num = MathUtil.Clamp(heatmapPos.x - spread, 0, heatmapSize.width - 1);
		int num2 = MathUtil.Clamp(heatmapPos.x + spread, 0, heatmapSize.width - 1);
		int num3 = MathUtil.Clamp(heatmapPos.y - spread, 0, heatmapSize.height - 1);
		int num4 = MathUtil.Clamp(heatmapPos.y + spread, 0, heatmapSize.height - 1);
		for (int i = num; i <= num2; i++)
		{
			for (int j = num3; j <= num4; j++)
			{
				float num5 = data[i, j] + delta;
				data[i, j] = ((num5 > 1f) ? 1f : num5);
			}
		}
	}

	public Texture2D GetBilinearTextureFromMap(Color col)
	{
		HeatmapSize heatmapSize = this.heatmapSize;
		Texture2D texture2D = new Texture2D(heatmapSize.width, heatmapSize.height)
		{
			filterMode = FilterMode.Bilinear
		};
		Color[] array = new Color[heatmapSize.width * heatmapSize.height];
		float[,] array2 = data;
		int i = 0;
		for (int width = heatmapSize.width; i < width; i++)
		{
			int j = 0;
			for (int height = heatmapSize.height; j < height; j++)
			{
				Color color = col;
				color.a = array2[i, j];
				array[j * heatmapSize.width + i] = color;
			}
		}
		texture2D.SetPixels(array);
		texture2D.Apply();
		return texture2D;
	}
}

using UnityEngine;
using UnityEngine.UI;

namespace AwesomeCharts;

public class LineChartBackground : MaskableGraphic
{
	[SerializeField]
	private Vector2[] points;

	[SerializeField]
	private AxisBounds axisBounds;

	[SerializeField]
	private Texture texture;

	public Vector2[] Points
	{
		get
		{
			return points;
		}
		set
		{
			points = value;
			SetAllDirty();
		}
	}

	public AxisBounds AxisBounds
	{
		get
		{
			return axisBounds;
		}
		set
		{
			axisBounds = value;
			SetAllDirty();
		}
	}

	public Texture Texture
	{
		get
		{
			return texture;
		}
		set
		{
			if (!(texture == value))
			{
				texture = value;
				SetVerticesDirty();
				SetMaterialDirty();
			}
		}
	}

	public override Texture mainTexture => texture ?? Graphic.s_WhiteTexture;

	protected override void OnPopulateMesh(VertexHelper vh)
	{
		vh.Clear();
		if (Points.Length < 2)
		{
			return;
		}
		Vector2 vector = points[0];
		Vector2 vector2 = new Vector2(points[0].x, 0f);
		float num = 0f;
		float num2 = 0f;
		Vector2[] array = points;
		for (int i = 0; i < array.Length; i++)
		{
			Vector2 vector3 = array[i];
			if (vector3.x > num)
			{
				num = vector3.x;
			}
			if (vector3.y > num2)
			{
				num2 = vector3.y;
			}
		}
		for (int j = 1; j < Points.Length; j++)
		{
			Vector2 vector4 = points[j];
			Vector2 vector5 = new Vector2(points[j].x, 0f);
			vh.AddUIVertexQuad(CreateUIVertices(new Vector2[4] { vector2, vector, vector4, vector5 }, new Vector2[4]
			{
				GetCorrectUV(vector2, num, num2),
				GetCorrectUV(vector, num, num2),
				GetCorrectUV(vector4, num, num2),
				GetCorrectUV(vector5, num, num2)
			}));
			vector = vector4;
			vector2 = vector5;
		}
	}

	private Vector2 GetCorrectUV(Vector2 point, float maxX, float maxY)
	{
		return new Vector2(point.x / maxX, point.y / maxY);
	}

	private UIVertex[] CreateUIVertices(Vector2[] vertices, Vector2[] uvs)
	{
		UIVertex[] array = new UIVertex[4];
		for (int i = 0; i < vertices.Length; i++)
		{
			UIVertex simpleVert = UIVertex.simpleVert;
			simpleVert.color = color;
			simpleVert.position = vertices[i];
			simpleVert.uv0 = uvs[i];
			array[i] = simpleVert;
		}
		return array;
	}
}

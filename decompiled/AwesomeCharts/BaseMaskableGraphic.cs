using UnityEngine;
using UnityEngine.UI;

namespace AwesomeCharts;

public class BaseMaskableGraphic : MaskableGraphic
{
	protected Vector2[] CreateDefaultUVs()
	{
		return new Vector2[4]
		{
			new Vector2(0f, 0f),
			new Vector2(0f, 1f),
			new Vector2(1f, 1f),
			new Vector2(1f, 0f)
		};
	}

	protected UIVertex[] CreateUIVertices(Vector2[] vertices, Vector2[] uvs, Color color)
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

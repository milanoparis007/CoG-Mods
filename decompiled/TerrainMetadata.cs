using UnityEngine;

[CreateAssetMenu(fileName = "TerrainMetadata", menuName = "Terrain Metadata", order = 1)]
public class TerrainMetadata : ScriptableObject
{
	public Gradient _terrainColors;

	public Color Sample(float percentage)
	{
		return _terrainColors.Evaluate(percentage);
	}
}

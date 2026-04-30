using UnityEngine;

public class WaterOil : MonoBehaviour
{
	private void Start()
	{
		MaterialPropertyBlock materialPropertyBlock = new MaterialPropertyBlock();
		MeshRenderer component = GetComponent<MeshRenderer>();
		component.GetPropertyBlock(materialPropertyBlock);
		materialPropertyBlock.SetInt("_OilFlag", 1);
		component.SetPropertyBlock(materialPropertyBlock);
	}
}

using System.Collections.Generic;
using UnityEngine;

namespace AwesomeCharts;

public class ACMonoBehaviour : MonoBehaviour
{
	private List<GameObject> objectsToRemove = new List<GameObject>();

	protected virtual void Update()
	{
		ExecuteObjectsRemoval();
	}

	private void ExecuteObjectsRemoval()
	{
		foreach (GameObject item in objectsToRemove)
		{
			Object.DestroyImmediate(item);
		}
		objectsToRemove.Clear();
	}

	public void DestroyDelayed(GameObject target)
	{
		target.SetActive(value: false);
		objectsToRemove.Add(target);
	}
}

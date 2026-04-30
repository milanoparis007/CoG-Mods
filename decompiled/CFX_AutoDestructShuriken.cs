using System.Collections;
using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class CFX_AutoDestructShuriken : MonoBehaviour
{
	public bool OnlyDeactivate;

	public bool DestroyParentAlso;

	private void OnEnable()
	{
		StartCoroutine(CheckIfAlive());
	}

	private IEnumerator CheckIfAlive()
	{
		ParticleSystem ps = GetComponent<ParticleSystem>();
		while (ps != null)
		{
			yield return new WaitForSeconds(0.5f);
			if (!ps.IsAlive(withChildren: true))
			{
				GameObject gameObject = base.gameObject.transform.parent.gameObject;
				DeactivateOrDestroy(base.gameObject, OnlyDeactivate);
				if (DestroyParentAlso)
				{
					DeactivateOrDestroy(gameObject, OnlyDeactivate);
				}
				break;
			}
		}
	}

	private static void DeactivateOrDestroy(GameObject gameObject, bool OnlyDeactivate)
	{
		if (OnlyDeactivate)
		{
			gameObject.SetActive(value: false);
		}
		else
		{
			Object.Destroy(gameObject);
		}
	}
}

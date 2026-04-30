using System.Collections.Generic;
using UnityEngine;

namespace Game.Session.Assets;

[AddComponentMenu("Game Assets/PFX Definitions")]
public class PFXDefinitions : MonoBehaviour
{
	public List<PFXOneShot> pfx;

	public static PFXDefinitions Instance { get; private set; }

	private void Awake()
	{
		Instance = this;
	}

	public PFXOneShot FindOrNull(PFXType type)
	{
		foreach (PFXOneShot item in pfx)
		{
			if (item.type == type)
			{
				return item;
			}
		}
		return null;
	}
}

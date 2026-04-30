using System;
using UnityEngine;

namespace Game.Session.Assets;

[Serializable]
public class SFXDefinition
{
	public SFXType type;

	public AudioClip clip;

	public AudioSource track;
}

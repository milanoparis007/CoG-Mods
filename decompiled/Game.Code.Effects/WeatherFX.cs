using SomaSim.Util;
using UnityEngine;

namespace Game.Code.Effects;

public class WeatherFX : MonoBehaviour
{
	public enum Type
	{
		None,
		Rain,
		Snow,
		Dust
	}

	public ParticleSystem Rain;

	public ParticleSystem Snow;

	public ParticleSystem Dust;

	public bool IsVisible => base.gameObject.activeSelf;

	public void Start()
	{
		SetVisible(visible: false);
		StopAll();
	}

	public void SetVisible(bool visible)
	{
		base.gameObject.SetActive(visible);
	}

	public Type GetShowing()
	{
		if (Rain.isPlaying)
		{
			return Type.Rain;
		}
		if (Snow.isPlaying)
		{
			return Type.Snow;
		}
		if (Dust.isPlaying)
		{
			return Type.Dust;
		}
		return Type.None;
	}

	public void StartEffect(Type type, bool prewarm)
	{
		if (GetShowing() != type)
		{
			StopAll();
		}
		ParticleSystem particleSystem = Get(type);
		if (particleSystem != null)
		{
			ParticleSystem.MainModule main = particleSystem.main;
			main.prewarm = prewarm;
			particleSystem.Play();
		}
	}

	public void StopEffect(Type type)
	{
		ParticleSystem particleSystem = Get(type);
		if (particleSystem != null)
		{
			particleSystem.Stop();
		}
	}

	public void StopAll()
	{
		foreach (Type valuesAs in EnumUtil<Type>.GetValuesAsList())
		{
			StopEffect(valuesAs);
		}
	}

	private ParticleSystem Get(Type type)
	{
		return type switch
		{
			Type.Rain => Rain, 
			Type.Snow => Snow, 
			Type.Dust => Dust, 
			Type.None => null, 
			_ => null, 
		};
	}
}

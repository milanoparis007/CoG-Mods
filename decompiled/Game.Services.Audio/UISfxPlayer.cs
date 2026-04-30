using Game.Session.Assets;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Services.Audio;

public class UISfxPlayer : MonoBehaviour
{
	public SFXDefinition[] effects;

	public AudioClip[] carMoveClips;

	public AudioClip[] truckMoveClips;

	public AudioClip[] crewSelectClips;

	public AudioSource carTruckMoveAudioTrack;

	public AudioSource crewSelectTrack;

	[Space(20f)]
	public AudioMixer mixer;

	public void Play(SFXType type)
	{
		SFXDefinition sFXDefinition = FindDef(type);
		sFXDefinition?.track.PlayOneShot(sFXDefinition.clip);
	}

	private SFXDefinition FindDef(SFXType type)
	{
		int i = 0;
		for (int num = effects.Length; i < num; i++)
		{
			if (effects[i].type == type)
			{
				return effects[i];
			}
		}
		return null;
	}

	internal void PlayDriving(bool car)
	{
		AudioClip[] array = (car ? carMoveClips : truckMoveClips);
		int num = Random.Range(0, array.Length);
		carTruckMoveAudioTrack.PlayOneShot(array[num]);
	}

	internal void PlayCrewSelect()
	{
		int num = Random.Range(0, crewSelectClips.Length);
		crewSelectTrack.PlayOneShot(crewSelectClips[num]);
	}
}

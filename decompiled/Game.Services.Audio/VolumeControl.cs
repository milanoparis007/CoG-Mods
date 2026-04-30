using UnityEngine;
using UnityEngine.Audio;

namespace Game.Services.Audio;

public class VolumeControl
{
	public const string MUSIC_VOLUME_NAME = "Music Volume";

	public const string AMBIENT_VOLUME_NAME = "Ambient Volume";

	public const string UI_VOLUME_NAME = "UI Volume";

	public const float MAX_VOL = 0f;

	public const float MIN_VOL = -40f;

	public AudioMixer mixer;

	public string mixerName;

	public float minVolume;

	public float maxVolume;

	public float currentVolume;

	public VolumeControl(AudioMixer mixer, string mixerName)
	{
		this.mixer = mixer;
		this.mixerName = mixerName;
		minVolume = -40f;
		if (!mixer.GetFloat(mixerName, out maxVolume))
		{
			maxVolume = 0f;
		}
		SetVolume(1f);
	}

	public float GetVolume()
	{
		return currentVolume;
	}

	public void SetVolume(float volume)
	{
		currentVolume = volume;
		float value = ((volume == 0f) ? (-80f) : Mathf.Lerp(minVolume, maxVolume, volume));
		mixer.SetFloat(mixerName, value);
	}
}

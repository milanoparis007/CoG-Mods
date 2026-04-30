using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Services.Audio;

public class MusicPlayer : MonoBehaviour
{
	[Serializable]
	public class Theme
	{
		public AudioClip[] neutral;

		[Tooltip("If this list is empty, 'neutral' will be used. To mute it instead, add a single silence clip.")]
		public AudioClip[] summer;

		[Tooltip("If this list is empty, 'neutral' will be used. To mute it instead, add a single silence clip.")]
		public AudioClip[] winter;

		public AudioClip[] GetClips(AudioSeason season)
		{
			if (season == AudioSeason.Winter && winter != null && winter.Length != 0)
			{
				return winter;
			}
			if (season == AudioSeason.Summer && summer != null && summer.Length != 0)
			{
				return summer;
			}
			return neutral;
		}
	}

	[Serializable]
	public class ClipSet
	{
		public Theme coreGame;

		public Theme atlanticCity;

		public Theme newYork;

		public float volumeNeutral = 1f;

		public float volumeSummer = 1f;

		public float volumeWinter = 1f;

		public AnimationCurve volumeFromTimeOfDay = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(24f, 1f));

		public float GetVolume(AudioSeason season, float timeOfDay)
		{
			return season switch
			{
				AudioSeason.Winter => volumeWinter, 
				AudioSeason.Summer => volumeSummer, 
				_ => volumeNeutral, 
			} * volumeFromTimeOfDay.Evaluate(timeOfDay);
		}

		public Theme GetTheme(AudioTheme theme)
		{
			return theme switch
			{
				AudioTheme.CoreGame => coreGame, 
				AudioTheme.AtlanticCity => atlanticCity, 
				AudioTheme.NewYork => newYork, 
				_ => coreGame, 
			};
		}

		public AudioClip[] GetClips(AudioTheme theme, AudioSeason season)
		{
			return GetTheme(theme).GetClips(season);
		}
	}

	[Serializable]
	public enum AudioSeason
	{
		Spring,
		Summer,
		Fall,
		Winter
	}

	[Serializable]
	public enum AudioTheme
	{
		CoreGame,
		AtlanticCity,
		NewYork
	}

	public AudioMixer mixer;

	[Header("Main Menu Music")]
	public AudioSource MainMenuTrack;

	[Header("AudioSources")]
	public AudioSource Track1;

	public AudioSource Track2;

	public AudioSource Track3;

	public AudioSource Track4;

	public AudioSource Track5;

	public AudioSource Track6;

	public AudioSource Track7;

	public AudioSource Track8;

	[Header("AudioCueSources")]
	public AudioSource Track1Cue;

	public AudioSource Track2Cue;

	public AudioSource Track3Cue;

	public AudioSource Track4Cue;

	public AudioSource Track5Cue;

	public AudioSource Track6Cue;

	public AudioSource Track7Cue;

	public AudioSource Track8Cue;

	[Header("Current season based on game day")]
	public AudioSeason currentSeason;

	[Header("Current time based on game timer")]
	public float currentTimeOfDay;

	[Header("Audio Clip Set: Piano")]
	public ClipSet Track1Clips;

	[Header("Audio Clip Set: Drums")]
	public ClipSet Track2Clips;

	[Header("Audio Clip Set: Bass")]
	public ClipSet Track3Clips;

	[Header("Audio Clip Set: Clarinet")]
	public ClipSet Track4Clips;

	[Header("Audio Clip Set: Strings")]
	public ClipSet Track5Clips;

	[Header("Audio Clip Set: Trumpet")]
	public ClipSet Track6Clips;

	[Header("Audio Clip Set: Trombone")]
	public ClipSet Track7Clips;

	[Header("Audio Clip Set: Bells, Chimes, etc")]
	public ClipSet Track8Clips;

	[Header("Parameters")]
	public float trackTime;

	public float longTrackTime;

	private IGameAudioController _ctrl;

	private const float MAIN_MENU_TWEEN_TIME = 3f;

	public void StartAllTracks(IGameAudioController ctrl)
	{
		_ctrl = ctrl;
		PlayTrack1();
		PlayTrack2();
		PlayTrack3();
		PlayTrack4();
		PlayTrack5();
		PlayTrack6();
		PlayTrack7();
		PlayTrack8();
	}

	public void StopAllTracks()
	{
		StopTrack1();
		StopTrack2();
		StopTrack3();
		StopTrack4();
		StopTrack5();
		StopTrack6();
		StopTrack7();
		StopTrack8();
		_ctrl = null;
	}

	private void SetMainMenuVolume(float v)
	{
		MainMenuTrack.volume = v;
	}

	public void StartMainMenuMusic()
	{
		MainMenuTrack.volume = 0f;
		MainMenuTrack.PlayDelayed(0.01f);
		LeanTween.cancel(base.gameObject);
		LeanTween.value(base.gameObject, SetMainMenuVolume, 0f, 1f, 3f).setEase(LeanTweenType.easeInOutSine);
	}

	public void StopMainMenuMusic()
	{
		float volume = MainMenuTrack.volume;
		LeanTween.cancel(base.gameObject);
		LeanTween.value(base.gameObject, SetMainMenuVolume, volume, 0f, 3f).setEase(LeanTweenType.easeInOutSine).onComplete = delegate
		{
			MainMenuTrack.Stop();
		};
	}

	private void Play(AudioSource source, ClipSet clipset, string next, float time)
	{
		currentSeason = _ctrl?.GetSeason() ?? AudioSeason.Summer;
		currentTimeOfDay = _ctrl?.GetHourOfDay() ?? 12f;
		float volume = clipset.GetVolume(currentSeason, currentTimeOfDay);
		source.volume = volume;
		AudioTheme theme = _ctrl?.GetTheme() ?? AudioTheme.CoreGame;
		AudioClip[] clips = clipset.GetClips(theme, currentSeason);
		int num = UnityEngine.Random.Range(0, clips.Length);
		source.clip = clips[num];
		source.Play();
		Invoke(next, time);
	}

	private void Stop(AudioSource track, AudioSource cue, string trackname, string cuename)
	{
		track.Stop();
		cue.Stop();
		CancelInvoke(trackname);
		CancelInvoke(cuename);
	}

	private void PlayTrack1()
	{
		Play(Track1, Track1Clips, "PlayTrack1Cue", trackTime);
	}

	private void PlayTrack1Cue()
	{
		Play(Track1Cue, Track1Clips, "PlayTrack1", trackTime);
	}

	private void PlayTrack2()
	{
		Play(Track2, Track2Clips, "PlayTrack2Cue", trackTime);
	}

	private void PlayTrack2Cue()
	{
		Play(Track2Cue, Track2Clips, "PlayTrack2", trackTime);
	}

	private void PlayTrack3()
	{
		Play(Track3, Track3Clips, "PlayTrack3Cue", trackTime);
	}

	private void PlayTrack3Cue()
	{
		Play(Track3Cue, Track3Clips, "PlayTrack3", trackTime);
	}

	private void PlayTrack4()
	{
		Play(Track4, Track4Clips, "PlayTrack4Cue", trackTime);
	}

	private void PlayTrack4Cue()
	{
		Play(Track4Cue, Track4Clips, "PlayTrack4", trackTime);
	}

	private void PlayTrack5()
	{
		Play(Track5, Track5Clips, "PlayTrack5Cue", trackTime);
	}

	private void PlayTrack5Cue()
	{
		Play(Track5Cue, Track5Clips, "PlayTrack5", trackTime);
	}

	private void PlayTrack6()
	{
		Play(Track6, Track6Clips, "PlayTrack6Cue", trackTime);
	}

	private void PlayTrack6Cue()
	{
		Play(Track6Cue, Track6Clips, "PlayTrack6", trackTime);
	}

	private void PlayTrack7()
	{
		Play(Track7, Track7Clips, "PlayTrack7Cue", longTrackTime);
	}

	private void PlayTrack7Cue()
	{
		Play(Track7Cue, Track7Clips, "PlayTrack7", longTrackTime);
	}

	private void PlayTrack8()
	{
		Play(Track8, Track8Clips, "PlayTrack8Cue", longTrackTime);
	}

	private void PlayTrack8Cue()
	{
		Play(Track8Cue, Track8Clips, "PlayTrack8", longTrackTime);
	}

	private void StopTrack1()
	{
		Stop(Track1, Track1Cue, "PlayTrack1", "PlayTrack1Cue");
	}

	private void StopTrack2()
	{
		Stop(Track2, Track2Cue, "PlayTrack2", "PlayTrack2Cue");
	}

	private void StopTrack3()
	{
		Stop(Track3, Track3Cue, "PlayTrack3", "PlayTrack3Cue");
	}

	private void StopTrack4()
	{
		Stop(Track4, Track4Cue, "PlayTrack4", "PlayTrack4Cue");
	}

	private void StopTrack5()
	{
		Stop(Track5, Track5Cue, "PlayTrack5", "PlayTrack5Cue");
	}

	private void StopTrack6()
	{
		Stop(Track6, Track6Cue, "PlayTrack6", "PlayTrack6Cue");
	}

	private void StopTrack7()
	{
		Stop(Track7, Track7Cue, "PlayTrack7", "PlayTrack7Cue");
	}

	private void StopTrack8()
	{
		Stop(Track8, Track8Cue, "PlayTrack8", "PlayTrack8Cue");
	}
}

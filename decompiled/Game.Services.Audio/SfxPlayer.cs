using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Services.Audio;

public class SfxPlayer : MonoBehaviour
{
	[Serializable]
	public struct FromTo
	{
		public float from;

		public float to;
	}

	[Serializable]
	public class ClipSet
	{
		[Tooltip("Audio clips for this track")]
		public AudioClip[] clips;

		[Tooltip("Track volume based on time of day (from 0:00 to 24:00) where 1 is full volume and 0 is silence")]
		public AnimationCurve volumes = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(24f, 1f));

		[Tooltip("Random delay between one clip and the next, in seconds")]
		public FromTo randomDelaySeconds;

		[Tooltip("Random pan offset, where 0 is centered")]
		public FromTo randomPanOffset;
	}

	[Serializable]
	public class Loop
	{
		[Tooltip("Looping audio clip")]
		public AudioClip[] clips;

		[Tooltip("Track volume based on time of day (from 0:00 to 24:00) where 1 is full volume and 0 is silence")]
		public AnimationCurve volumes = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(8f, 1f), new Keyframe(10f, 1f), new Keyframe(18f, 1f), new Keyframe(22f, 1f));
	}

	private class ClipSetState
	{
		public string debugName;

		public ClipSet clipSet;

		public AudioSource source;

		public float nextPlayTime;

		public ClipSetState(AudioSource source, ClipSet clipSet, string debugName)
		{
			this.debugName = debugName;
			this.clipSet = clipSet;
			this.source = source;
			nextPlayTime = float.MaxValue;
		}

		public float GetRandomDelay()
		{
			return UnityEngine.Random.Range(clipSet.randomDelaySeconds.from, clipSet.randomDelaySeconds.to);
		}

		public float GetRandomPan()
		{
			return UnityEngine.Random.Range(clipSet.randomPanOffset.from, clipSet.randomPanOffset.to);
		}
	}

	private class LoopState
	{
		public string debugName;

		public Loop loop;

		public AudioSource source;

		public LoopState(AudioSource source, Loop loop, string debugName)
		{
			this.source = source;
			this.loop = loop;
			this.debugName = debugName;
		}
	}

	public AudioMixer mixer;

	[Header("Camera distance volume control")]
	public AnimationCurve clipVolumeFromDistance = new AnimationCurve(new Keyframe(50f, 1f), new Keyframe(100f, 0f));

	public AnimationCurve loopVolumeFromDistance = new AnimationCurve(new Keyframe(50f, 1f), new Keyframe(100f, 0f));

	public float clipMultiplierComputed = 1f;

	public float loopMultiplierComputed = 1f;

	[Header("Time of Day")]
	public bool manualOverride;

	public float currentHour;

	public bool playClipsAtFullVolume;

	[Header("Clip AudioSources")]
	public AudioSource SourceBirds;

	public AudioSource SourceHorns;

	public AudioSource SourceMiscCity;

	public AudioSource SourceOtherAnimals;

	public AudioSource SourceCityWind;

	[Header("Clip Sets")]
	public ClipSet ClipsBirds;

	public ClipSet ClipsHorns;

	public ClipSet ClipsMiscCity;

	public ClipSet ClipsOtherAnimals;

	public ClipSet ClipsCityWind;

	[Header("Loop AudioSources")]
	public AudioSource LoopSourceBirds;

	public AudioSource LoopSourceCarsAM;

	public AudioSource LoopSourceCarsPM;

	public AudioSource LoopSourceCrickets;

	public AudioSource LoopSourceDistantWind;

	[Header("Loops")]
	public Loop LoopBirds;

	public Loop LoopCarsAM;

	public Loop LoopCarsPM;

	public Loop LoopCrickets;

	public Loop LoopDistantWind;

	private IGameAudioController _ctrl;

	private ClipSetState[] AllClipStates;

	private LoopState[] AllLoopStates;

	private void Start()
	{
		AllClipStates = new ClipSetState[5]
		{
			new ClipSetState(SourceBirds, ClipsBirds, "ClipsBirds"),
			new ClipSetState(SourceHorns, ClipsHorns, "ClipsHorns"),
			new ClipSetState(SourceMiscCity, ClipsMiscCity, "ClipsMiscCity"),
			new ClipSetState(SourceOtherAnimals, ClipsOtherAnimals, "ClipsOtherAnimals"),
			new ClipSetState(SourceCityWind, ClipsCityWind, "ClipsCityWind")
		};
		AllLoopStates = new LoopState[4]
		{
			new LoopState(LoopSourceBirds, LoopBirds, "LoopBirds"),
			new LoopState(LoopSourceCarsAM, LoopCarsAM, "LoopCarsAM"),
			new LoopState(LoopSourceCarsPM, LoopCarsPM, "LoopCarsPM"),
			new LoopState(LoopSourceCrickets, LoopCrickets, "LoopCrickets")
		};
	}

	private void Update()
	{
		if (Game.ctx == null)
		{
			return;
		}
		if (!manualOverride)
		{
			currentHour = _ctrl?.GetHourOfDay() ?? 0f;
		}
		float zoom = Game.serv.camera.GetZoom();
		clipMultiplierComputed = clipVolumeFromDistance.Evaluate(zoom);
		loopMultiplierComputed = loopVolumeFromDistance.Evaluate(zoom);
		ClipSetState[] allClipStates = AllClipStates;
		foreach (ClipSetState clipSetState in allClipStates)
		{
			UpdateClipVolume(clipSetState);
			if (Time.realtimeSinceStartup > clipSetState.nextPlayTime)
			{
				PlayRandomClip(clipSetState);
			}
		}
		LoopState[] allLoopStates = AllLoopStates;
		foreach (LoopState state in allLoopStates)
		{
			UpdateLoopVolume(state);
		}
	}

	private void UpdateClipVolume(ClipSetState state)
	{
		float num = ((manualOverride && playClipsAtFullVolume) ? 1f : state.clipSet.volumes.Evaluate(currentHour));
		state.source.volume = num * clipMultiplierComputed;
	}

	private void UpdateLoopVolume(LoopState state)
	{
		float num = state.loop.volumes.Evaluate(currentHour);
		state.source.volume = num * loopMultiplierComputed;
	}

	public void StartAll(IGameAudioController ctrl)
	{
		_ctrl = ctrl;
		ClipSetState[] allClipStates = AllClipStates;
		foreach (ClipSetState state in allClipStates)
		{
			StartClip(state);
		}
		LoopState[] allLoopStates = AllLoopStates;
		foreach (LoopState state2 in allLoopStates)
		{
			StartLoop(state2);
		}
	}

	public void StopAll()
	{
		ClipSetState[] allClipStates = AllClipStates;
		foreach (ClipSetState state in allClipStates)
		{
			StopClip(state);
		}
		LoopState[] allLoopStates = AllLoopStates;
		foreach (LoopState state2 in allLoopStates)
		{
			StopLoop(state2);
		}
		_ctrl = null;
	}

	private void PlayRandomClip(ClipSetState state)
	{
		if (state.clipSet.clips.Length != 0)
		{
			int num = UnityEngine.Random.Range(0, state.clipSet.clips.Length);
			AudioClip audioClip = state.clipSet.clips[num];
			state.source.clip = audioClip;
			state.source.loop = false;
			state.source.panStereo = state.GetRandomPan();
			state.source.Play();
			state.nextPlayTime = Time.realtimeSinceStartup + audioClip.length + state.GetRandomDelay();
			UpdateClipVolume(state);
		}
	}

	private void StartClip(ClipSetState state)
	{
		state.nextPlayTime = Time.realtimeSinceStartup + state.GetRandomDelay();
	}

	private void StopClip(ClipSetState state)
	{
		if (state.source.isPlaying)
		{
			state.source.Stop();
		}
		state.nextPlayTime = float.MaxValue;
	}

	private void StartLoop(LoopState state)
	{
		if (state.loop.clips.Length != 1)
		{
			Logger.Error($"Invalid number of loop clips in {state.debugName}: expected 1, found {state.loop.clips.Length}");
			return;
		}
		AudioClip clip = state.loop.clips[0];
		state.source.clip = clip;
		state.source.loop = true;
		state.source.Play();
		UpdateLoopVolume(state);
	}

	private void StopLoop(LoopState state)
	{
		if (state.source.isPlaying)
		{
			state.source.Stop();
		}
	}
}

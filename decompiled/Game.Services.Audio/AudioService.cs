using Game.Services.Filesystem;
using Game.Session.Assets;
using UnityEngine;

namespace Game.Services.Audio;

public class AudioService : AbstractService
{
	private MusicPlayer _music;

	private SfxPlayer _ambient;

	private UISfxPlayer _uisfx;

	private VolumeControl _musicvol;

	private VolumeControl _ambientvol;

	private VolumeControl _uisfxvol;

	public override void OnLoaded()
	{
		base.OnLoaded();
		GamePreferences game = Game.serv.saveload.prefs.game;
		GameObject gameObject = GameObject.Find("MusicPlayer");
		_music = gameObject.GetComponent<MusicPlayer>();
		_musicvol = new VolumeControl(_music.mixer, "Music Volume");
		_musicvol.SetVolume(game.volmusic);
		GameObject gameObject2 = GameObject.Find("AmbientPlayer");
		_ambient = gameObject2.GetComponent<SfxPlayer>();
		_ambientvol = new VolumeControl(_ambient.mixer, "Ambient Volume");
		_ambientvol.SetVolume(game.volambient);
		GameObject gameObject3 = GameObject.Find("UISoundPlayer");
		_uisfx = gameObject3.GetComponent<UISfxPlayer>();
		_uisfxvol = new VolumeControl(_uisfx.mixer, "UI Volume");
		_uisfxvol.SetVolume(game.voluisfx);
	}

	public override void OnReleased()
	{
		base.OnReleased();
		_ambient = null;
		_music = null;
	}

	public void Update()
	{
		_ = _music == null;
	}

	public void MusicStartMainMenu()
	{
		_music.StopAllTracks();
		_ambient.StopAll();
		_music.StartMainMenuMusic();
	}

	public void MusicStartProcedural()
	{
		IGameAudioController ctrl = AudioUtil.MakeAudioController();
		_music.StartAllTracks(ctrl);
		_ambient.StartAll(ctrl);
		_music.StopMainMenuMusic();
	}

	public void MusicStopAll()
	{
		_music.StopAllTracks();
		_ambient.StopAll();
		_music.StopMainMenuMusic();
	}

	public float GetMusicVolume()
	{
		return _musicvol.GetVolume();
	}

	public void SetMusicVolume(float volume)
	{
		_musicvol.SetVolume(volume);
	}

	public float GetUiSfxVolume()
	{
		return _uisfxvol.GetVolume();
	}

	public void SetUiSfxVolume(float volume)
	{
		_uisfxvol.SetVolume(volume);
	}

	public float GetAmbientVolume()
	{
		return _ambientvol.GetVolume();
	}

	public void SetAmbientVolume(float volume)
	{
		_ambientvol.SetVolume(volume);
	}

	public void PlayUISFX(SFXType fx)
	{
		if (fx != SFXType.None && !(_uisfx == null))
		{
			_uisfx.Play(fx);
		}
	}

	public void PlayDrivingSFX(bool car)
	{
		if (_uisfx != null)
		{
			_uisfx.PlayDriving(car);
		}
	}

	public void PlayerCrewSelectSFX()
	{
		if (_uisfx != null)
		{
			_uisfx.PlayCrewSelect();
		}
	}
}

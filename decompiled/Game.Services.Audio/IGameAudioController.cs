namespace Game.Services.Audio;

public interface IGameAudioController
{
	float GetHourOfDay();

	MusicPlayer.AudioSeason GetSeason();

	MusicPlayer.AudioTheme GetTheme();
}

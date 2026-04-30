using Game.Services.Audio;
using Game.Session.Board;

namespace Game.Session.Assets;

public static class AudioUtil
{
	public class GameAudioController : IGameAudioController
	{
		public float GetHourOfDay()
		{
			return Game.ctx?.seasons?.FindDisplayedTimeOfDay() ?? 12f;
		}

		public MusicPlayer.AudioSeason GetSeason()
		{
			return (Game.ctx?.seasons?.CurrentSeason?.season ?? Season.Summer) switch
			{
				Season.Spring => MusicPlayer.AudioSeason.Spring, 
				Season.Summer => MusicPlayer.AudioSeason.Summer, 
				Season.Fall => MusicPlayer.AudioSeason.Fall, 
				Season.Winter => MusicPlayer.AudioSeason.Winter, 
				_ => MusicPlayer.AudioSeason.Summer, 
			};
		}

		public MusicPlayer.AudioTheme GetTheme()
		{
			if (Game.ctx?.session?.mapconfig?.id == "atlantic-city")
			{
				return MusicPlayer.AudioTheme.AtlanticCity;
			}
			return MusicPlayer.AudioTheme.CoreGame;
		}
	}

	public static IGameAudioController MakeAudioController()
	{
		return new GameAudioController();
	}
}

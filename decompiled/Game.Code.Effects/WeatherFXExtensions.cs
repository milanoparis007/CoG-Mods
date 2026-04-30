using Game.Services;
using SomaSim.Util;

namespace Game.Code.Effects;

public static class WeatherFXExtensions
{
	public static WeatherFX GetWeatherFX(this CameraService camera)
	{
		return camera.DollyGO.GetChild<WeatherFX>("Weather FX");
	}
}

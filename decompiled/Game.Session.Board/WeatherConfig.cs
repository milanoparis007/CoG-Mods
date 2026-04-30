using Game.Code.Effects;
using SomaSim.Util;

namespace Game.Session.Board;

public class WeatherConfig
{
	public const float DEFAULT_DURATION = 30f;

	public WeatherFX.Type type;

	public FloatRange hoursOfDay;

	public float prob;

	public float durationSeconds = 30f;
}

using SomaSim.Util;

namespace Game.Session.Board;

public class SeasonData
{
	public Season season;

	public IntRange dateRange;

	public FloatRange foliageHueShift;

	public float groundHueShift;

	public float groundSaturationShift;

	public float foliageSaturationShift;

	public FColor32 sunColor;

	public float sunIntensity;

	public FColor32 ambientColor;

	public float ambientIntensity;

	public FColor32 fogColor;

	public float fogIntensity;

	public WeatherConfig weather;
}

using Game.Core;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Entities;

public sealed class BuildingData : BaseData
{
	public class Health
	{
		public Fixnum max;

		public Fixnum current;

		public bool IsMax => current >= max;

		public bool IsNotMax => current < max;

		public bool IsZero => current <= 0;

		public static Health Make(Entity building, PlayerID owner)
		{
			Fixnum fixnum = BuildingSettings.EvaluateMaxBuildingHealth(building, owner);
			return new Health
			{
				current = fixnum,
				max = fixnum
			};
		}
	}

	public PlayerID safehouse;

	public PlayerID outpost;

	public EntityID business;

	public bool interesting;

	public bool potential;

	public bool suppressPick;

	public Health health;

	public PlayerFlags scoped = new PlayerFlags();

	public PlayerSingleFlag controlled = new PlayerSingleFlag();

	public TagList tags;
}

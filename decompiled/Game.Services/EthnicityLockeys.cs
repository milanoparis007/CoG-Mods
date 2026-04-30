using SomaSim.Util;

namespace Game.Services;

public sealed class EthnicityLockeys
{
	public string firstNamesMale;

	public string firstNamesFemale;

	public string lastNamesMale;

	public string lastNamesFemale;

	public string adjEthnicity;

	public string icon;

	public string GetRandomFirstName(IRandom rng, Gender gender)
	{
		return Loc.Get((gender == Gender.M) ? firstNamesMale : firstNamesFemale, rng);
	}

	public string GetRandomLastName(IRandom rng, Gender gender)
	{
		return Loc.Get((gender == Gender.M) ? lastNamesMale : lastNamesFemale, rng);
	}

	public string GetRandomFirstName(LocReplacementContext ctx, Gender gender)
	{
		return Loc.Get((gender == Gender.M) ? firstNamesMale : firstNamesFemale, ctx);
	}

	public string GetRandomLastName(LocReplacementContext ctx, Gender gender)
	{
		return Loc.Get((gender == Gender.M) ? lastNamesMale : lastNamesFemale, ctx);
	}

	public string GetEthnicityKey()
	{
		return adjEthnicity;
	}

	public string GetEthnicity()
	{
		return Loc.Get(adjEthnicity);
	}

	public string GetEthnicity(LocReplacementContext ctx)
	{
		return Loc.Get(adjEthnicity, ctx);
	}
}

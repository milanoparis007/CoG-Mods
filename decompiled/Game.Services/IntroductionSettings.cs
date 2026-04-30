using SomaSim.Util;

namespace Game.Services;

public sealed class IntroductionSettings : IValidatingSettings
{
	public float localFriendWeight;

	public float playerBizWeight;

	public float transactionWeight;

	public Fixnum transactionLifetimeDayz;

	public int countToIntroduce;

	public void Validate()
	{
	}

	public IntroductionType GetRandomIntroType(IRandom rng)
	{
		float num = rng.GenerateFloat();
		num -= localFriendWeight;
		if (num < 0f)
		{
			return IntroductionType.LocalFriend;
		}
		num -= playerBizWeight;
		if (!(num < 0f))
		{
			return IntroductionType.Transactions;
		}
		return IntroductionType.Business;
	}
}

using System.Collections.Generic;
using Game.Session.Data;

namespace Game.Services;

public class ConvoStateDef
{
	public enum ButtonGenerator
	{
		None,
		CustomBuySellList,
		CustomChoiceButtons,
		CustomExpirationButtons,
		CustomQuestDeliveryButtons,
		CustomDebtRepaymentButtons,
		CustomGamblerBanButtons,
		CustomGamblingModuleOptions,
		CustomSkillButtons,
		CustomSchemeButtons,
		CustomCampaignActionButtons,
		CustomJointWarTargetButtons
	}

	public enum CustomViewType
	{
		None
	}

	public ConvoBlurbList npcsays;

	public List<ConvoButtonDef> buttons;

	public CustomViewType customView;

	public ButtonGenerator dynamicButtons;

	public ConvoButtonDef dynamicTemplate;
}

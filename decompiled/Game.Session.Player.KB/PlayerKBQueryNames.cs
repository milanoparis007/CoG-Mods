using Game.Core;

namespace Game.Session.Player.KB;

public static class PlayerKBQueryNames
{
	public static readonly Label OWNER_ACTIVE_QUEST = (Label)"kb-has-active-quest-with-owner";

	public static readonly Label OWNER_EXPIRED_QUEST = (Label)"kb-has-expired-quest-with-owner";

	public static readonly Label OWNER_CASH_BOOST = (Label)"kb-cash-boost";

	public static readonly Label OWNER_CAN_HIRE_CREW = (Label)"kb-can-hire-new-crew";

	public static readonly Label OWNER_CAN_LEARN_SKILL = (Label)"kb-can-learn-new-skills";

	public static readonly Label OWNER_CAN_INTRO_FOR_BIZ = (Label)"kb-can-intro-for-business";

	public static readonly Label OWNER_CAN_TAKE_BIZ = (Label)"kb-can-take-over-business";

	public static readonly Label OWNER_IN_CIVIC_BUILDING = (Label)"kb-civic-buidling";

	public static readonly Label QREQ_SHOULD_START = (Label)"kb-qreq-should-start";

	public static readonly Label QREQ_SHOULD_FINISH = (Label)"kb-qreq-should-finish";

	public static readonly Label OUTPOST_IS_HUMAN = (Label)"kb-front-belongs-to-human";

	public static readonly Label OUTPOST_IS_AI = (Label)"kb-front-belongs-to-ai";

	public static readonly Label OUTPOST_SUPPORT = (Label)"kb-front-needs-support";

	public static readonly Label OUTPOST_SUPPORT_URGENT = (Label)"kb-front-needs-support-urgent";

	public static readonly Label OUTPOST_COLLECT = (Label)"kb-front-ready-to-collect";

	public static readonly Label OUTPOST_EXPANDING = (Label)"kb-front-expanding";

	public static readonly Label OUTPOST_CAN_EXPAND = (Label)"kb-front-can-expand";

	public static readonly Label OUTPOST_ACTIVE = (Label)"kb-front-active";

	public static readonly Label BIZ_RECENT_TRADE_AI = (Label)"kb-biz-recent-trade-ai";

	public static readonly Label BIZ_RECENT_TRADE_HUMAN = (Label)"kb-biz-recent-trade-human";

	public static readonly Label BIZ_EXTORTED = (Label)"kb-extorted-biz";

	public static readonly Label BIZ_STALLED = (Label)"kb-production-stalled";

	public static readonly Label BIZ_DELIVERY_SUCCESS = (Label)"kb-delivery-success";

	public static readonly Label BIZ_DELIVERY_INSUFFICIENT = (Label)"kb-delivery-insufficient";

	public static readonly Label BIZ_DELIVERY_FAILED = (Label)"kb-delivery-failed";

	public static readonly Label BUILDING_DAMAGED = (Label)"kb-building-damaged";

	public static readonly Label COP_PAIDOFF = (Label)"kb-cop-paidoff";

	public static readonly Label HEAL_PROMPT = (Label)"kb-heal-prompt";

	public static readonly Label CTRL_EMPTY_SLOT = (Label)"kb-controlled-empty-slot";

	public static readonly Label CTRL_UPGRADE_MODULE = (Label)"kb-controlled-upgrade-available";

	public static readonly Label RESEVENT_READY = (Label)"kb-resevent-ready-this-turn";

	public static readonly Label CORNER_TERRITORY_OWNED = (Label)"kb-corner-territory-owned";

	public static readonly Label CORNER_TERRITORY_EXPANDED = (Label)"kb-corner-territory-expanded";

	public static readonly Label CORNER_TERRITORY_CONTRACTED = (Label)"kb-corner-territory-contracted";

	public static readonly Label CORNER_TERRITORY_PUMPING = (Label)"kb-corner-territory-pumping";

	public static readonly Label CORNER_TERRITORY_STOLEN = (Label)"kb-corner-territory-stolen";

	public static readonly Label CORNER_HEAT_DOWN = (Label)"kb-corner-cops-heat-down";

	public static readonly Label CORNER_HEAT_UP = (Label)"kb-corner-cops-heat-up";

	public static readonly Label CORNER_HEAT_COPS_SOON = (Label)"kb-corner-cops-imminent";

	public static readonly Label CORNER_HEAT_COPS_RECENT = (Label)"kb-corner-cops-recent";

	public static readonly Label CASINO_OUT_OF_CASH = (Label)"kb-casino-out-of-cash";

	public static readonly Label CANADA_RELATED = (Label)"kb-canada-related";
}

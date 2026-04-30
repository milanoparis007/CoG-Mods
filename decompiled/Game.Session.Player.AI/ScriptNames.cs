using Game.Core;

namespace Game.Session.Player.AI;

public static class ScriptNames
{
	public static readonly Label AUTOMATION_SCRIPT = (Label)"perform-automation-step";

	public static readonly Label ATTACK_TARGET = (Label)"attack-target";

	public static readonly Label ATTACK_BUILDING = (Label)"attack-building";

	public static readonly Label FORCE_CLOSE_BUSINESS = (Label)"force-close-business";

	public static readonly Label WAIT_AT_RALLY_POINT = (Label)"wait-at-rally-point";

	public static readonly Label BUY_ITEM_SCRIPT = (Label)"go-buy-item";

	public static readonly Label SELL_ITEM_SCRIPT = (Label)"go-sell-item";

	public static readonly Label INSTALL_BACKROOM = (Label)"install-backroom-at-building";

	public static readonly Label REFILL_CASH = (Label)"handle-cash-at-safehouse";

	public static readonly Label OUTPOST_START = (Label)"go-start-new-outpost";

	public static readonly Label OUTPOST_VISIT = (Label)"go-visit-outpost";

	public static readonly Label OUTPOST_STEAL = (Label)"go-steal-outpost";

	public static readonly Label EXPLORE_NODE = (Label)"go-explore-node";

	public static readonly Label SCOPE_BUILDING = (Label)"go-scope-building";

	public static readonly Label GOTO_MEETING_POINT = (Label)"go-meeting-point";

	public static readonly Label SETUP_CONTROLLED = (Label)"setup-controlled";

	public static readonly Label SETUP_CONTROLLED_GAMBLING = (Label)"setup-controlled-gambling";

	public static readonly Label BURGLE = (Label)"burgle";

	public static readonly Label EXTORT = (Label)"extort";

	public static readonly Label VANDALIZE = (Label)"vandalize";

	public static readonly Label COP_RAID = (Label)"cop-visit-corner";

	public static readonly Label COP_BEAT = (Label)"cop-patrol-beat";

	public static readonly Label COP_COLLECT = (Label)"cop-collect-vehicle";

	public static readonly Label FED_INVESTIGATE = (Label)"fed-investigate";

	public static readonly Label FED_SLEEP = (Label)"fed-sleep";

	public static readonly Label FED_GOTO = (Label)"fed-goto-precinct";

	public static readonly Label PROTECT_OUTPOST = (Label)"protect-at-outpost";

	public static readonly Label SCHEME_LEAVE_BOARD = (Label)"leave-board";

	public static readonly Label SCHEME_RETURN_BOARD = (Label)"return-board";
}

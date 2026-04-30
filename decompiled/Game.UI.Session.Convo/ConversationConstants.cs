using Game.Core;

namespace Game.UI.Session.Convo;

public static class ConversationConstants
{
	public static readonly Label TOPLEVEL_CONVO = new Label("toplevel-state");

	public static readonly Label TOPLEVEL_QREQ_START = new Label("initiative-qreq-start");

	public static readonly Label TOPLEVEL_QREQ_COLLECT = new Label("initiative-qreq-collect");

	public static readonly Label TOPLEVEL_RESEVENT = new Label("resevent-toplevel");

	public static readonly Label TOPLEVEL_HEAL = new Label("initiative-heal");

	public static readonly Label TOPLEVEL_GAMBLING_MANAGER = new Label("gambling-manager-toplevel");

	public static readonly Label TOPLEVEL_DEBTOR = new Label("debtor-toplevel");

	public static readonly Label TOPLEVEL_REPAYMENT_PENDING = new Label("repayment-pending-toplevel");

	public static readonly Label TOPLEVEL_REPAYMENT_READY = new Label("repayment-ready-toplevel");

	public static readonly Label TOPLEVEL_REPAYMENT_FAIL = new Label("repayment-fail-toplevel");

	public static readonly Label TOPLEVEL_REPAYMENT_CASH = new Label("repayment-cash-toplevel");

	public static readonly Label TOPLEVEL_GOONS = new Label("goon-toplevel");

	public static readonly Label TOPLEVEL_GANGS = new Label("gang-toplevel");

	public static readonly Label TOPLEVEL_GANG_BUILDING = new Label("gang-building-toplevel");

	public static readonly Label TOPLEVEL_COP = new Label("cop-toplevel");

	public static readonly Label TOPLEVEL_FED = new Label("fed-toplevel");

	public static readonly Label TOPLEVEL_CREW = new Label("crewpeep-toplevel");

	public static readonly Label TOPLEVEL_POLITICIAN = new Label("politician-toplevel");

	public static readonly Label CLAIM_CONVO = new Label("boost-state");

	public static readonly Label BUYSELL_CONVO = new Label("buysell-state");

	public static readonly Label BUY_SELECT_CONVO = new Label("buy-selection-state");

	public static readonly Label SELL_SELECT_CONVO = new Label("sell-selection-state");

	public static readonly Label BUY_TERRITORY_PROBLEM = new Label("buy-territory-problem-state");

	public static readonly Label SELL_TERRITORY_PROBLEM = new Label("sell-territory-problem-state");

	public static readonly Label HIRECREW_CONFIRM_CONVO = new Label("ticket-hirecrew-confirm");

	public static readonly Label HIREPOLITICAN_CONFIRM_CONVO = new Label("ticket-hirepolitician-confirm");

	public static readonly Label TICKET_INTRO_CONFIRM = new Label("ticket-intro-confirm");

	public static readonly Label TICKET_GOON_INTRO_CONFIRM = new Label("ticket-goon-intro-confirm");

	public static readonly Label TICKET_INTRO_NONE = new Label("ticket-intro-none");

	public static readonly Label TICKET_COP_INTRO = new Label("ticket-cop-intro");

	public static readonly Label TICKET_GAMBLING_HOUSE_CONFIRM = new Label("ticket-gambling-house-confirm");

	public static readonly Label TICKET_GAMBLING_HOUSE_TYPE_SELECT = new Label("ticket-gambling-house-type-select");

	public static readonly Label TICKET_GAMBLING_HOUSE_NONE = new Label("ticket-gambling-house-none");

	public static readonly Label TICKET_NPCBOOST_CONFIRM = new Label("ticket-npcboost-confirm");

	public static readonly Label TICKET_NPCBOOST_NONE = new Label("ticket-npcboost-none");

	public static readonly Label TICKET_GOONBOOST_CONFIRM = new Label("goon-ticket-npcboost-confirm");

	public static readonly Label TICKET_GOONBOOST_NONE = new Label("goon-ticket-npcboost-none");

	public static readonly Label DELIVERY_QUEST_PENDING_CONVO = new Label("delivery-quest-pending");

	public static readonly Label DELIVERY_QUEST_DONE_CONVO = new Label("delivery-quest-done");

	public static readonly Label SHOW_QUEST_CHOICES = new Label("show-quest-reward-choices");

	public static readonly Label SHOW_QUEST_EXPIRED = new Label("show-quest-expired");

	public static readonly Label QREQ_SINGLE_BLURB = new Label("show-qreq-blurb");

	public static readonly Label QREQ_DECISION_POINT = new Label("show-qreq-decision");

	public static readonly Label GAMBLING_BAN_CONFIRM = new Label("gambling-manager-ban-confirm-special");
}

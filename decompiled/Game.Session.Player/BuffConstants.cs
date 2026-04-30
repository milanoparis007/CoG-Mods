using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Player;

public static class BuffConstants
{
	public static readonly Label BUFF_ON_SCOPEOUT_NPC = (Label)"relbuff-scopeout-npc";

	public static readonly Label BUFF_ON_SCOPEOUT_SAMEETH = (Label)"relbuff-scopeout-ethnicity";

	public static readonly Label STARTING_BUFF_SELF = (Label)"relbuff-starting-self";

	public static readonly Label STARTING_BUFF_FAMILY = (Label)"relbuff-starting-fam";

	public static readonly Label STARTING_BUFF_FAMILY_EXT = (Label)"relbuff-starting-fam-ext";

	public static readonly Label STARTING_BUFF_OLDFRIENDS = (Label)"relbuff-starting-oldfriends";

	public static readonly Label STARTING_BUFF_FRANCINE = (Label)"relbuff-starting-francine";

	public static readonly Label RELBUFF_PLAYER_TO_CREW = (Label)"relbuff-player-to-crew";

	public static readonly Label RELBUFF_CREW_TO_PLAYER = (Label)"relbuff-crew-to-player";

	public static readonly Label RELBUFF_CREW_FAM_TO_PLAYER = (Label)"relbuff-crew-fam-to-player";

	public static readonly Label RELBUFF_WRONG_FOOT = (Label)"relbuff-goons-wrong-foot";

	public static readonly Label RELBUFF_GANG_AGREEMENT = (Label)"relbuff-gang-truce";

	public static readonly Label TRADE_ONETIME_STATS_BUFF = (Label)"relbuff-special-onetime-trade-history";

	public static readonly Label TRADE_RECURRING_STATS_BUFF = (Label)"relbuff-special-recurring-trade-history";

	public static readonly Label TRADE_FROM_CREW_PERSONALITY = (Label)"relbuff-from-crew-personality";

	public static readonly Label TICKET_INTRO = (Label)"relbuff-ticket-intro";

	public static readonly Label TICKET_NPCBOOST = (Label)"relbuff-ticket-npcboost";

	public static readonly Label TICKET_TAUGHT_SKILL = (Label)"relbuff-ticket-taught-skill";

	public static readonly Label RELBUFF_OUTPOST_MANAGER = (Label)"relbuff-outpost-manager";

	public static readonly Label RELBUFF_TIED_HOUSE = (Label)"relbuff-tied-house";

	public static readonly Label RELBUFF_PASSENGER_BRIBE = (Label)"relbuff-passenger-bribe";

	public static readonly Label RELBUFF_CARGO_BRIBE = (Label)"relbuff-cargo-bribe";

	public static readonly Label RELBUFF_CARGO_ACTIVE = (Label)"relbuff-cargo-active";

	public static readonly Label RELBUFF_TRIBUTE_PREFIX = (Label)"relbuff-tribute";

	public static readonly Label RELBUFF_TRIBUTE_ANYONE = (Label)"relbuff-tribute-any";

	public static readonly Label RELBUFF_TRIBUTE_AFTER_HELPING = (Label)"relbuff-tribute-after-helping";

	public static readonly Label RELBUFF_TRIBUTE_AFTERCANCEL = (Label)"relbuff-tribute-after-cancel";

	public static readonly Label RELBUFF_OUTPOST_FIRED = (Label)"relbuff-outpost-manager-fired";

	public static readonly Label RELBUFF_OUTPOST_NEG = (Label)"relbuff-outpost-manager-neg";

	public static readonly Label RELBUFF_OUTPOST_NEGFAM = (Label)"relbuff-outpost-manager-negfam";

	public static readonly Label RELBUFF_COP_DONATION = (Label)"relbuff-cop-donation";

	public static readonly Label RELBUFF_COP_AFTERDONATION = (Label)"relbuff-cop-afterdonation";

	public static readonly Label RELBUFF_BOTTLE_COOLDOWN = (Label)"relbuff-bottle-rack-cooldown";

	public static readonly Label RELBUFF_BOTTLE_RACK = (Label)"relbuff-bottle-rack";

	public static readonly Label RELBUFF_COP_BLACKMAIL = (Label)"relbuff-cop-blackmail";

	public static readonly Label RELBUFF_COP_RECOMMENDATION = (Label)"relbuff-cop-recommendation";

	public static readonly Label RELBUFF_CANADA_ACCESS = (Label)"relbuff-canada-access";

	public static readonly Label RELBUFF_CANADA_CASH = (Label)"relbuff-canada-cash";

	public static readonly Label RELBUFF_CANADA_COOLDOWN_FROZEN = (Label)"relbuff-canada-cooldown-frozen";

	public static readonly Label RELBUFF_CANADA_COOLDOWN_THAWED = (Label)"relbuff-canada-cooldown-thawed";

	public static readonly Label RELBUFF_TICKET_POL_BOOST = (Label)"relbuff-ticket-politician-boost";

	public static readonly Label RELBUFF_NY_POL_STARTER = (Label)"relbuff-ny-pol-starter";

	public static readonly List<Label> CANADA_RELBUFFS = new List<Label> { RELBUFF_CANADA_ACCESS, RELBUFF_CANADA_CASH, RELBUFF_CANADA_COOLDOWN_FROZEN, RELBUFF_CANADA_COOLDOWN_THAWED };

	public static readonly Label RESPECT_AT_OUTPOST = (Label)"respectbuff-at-outpost";

	public static readonly Label RESPECT_AROUND_OUTPOST = (Label)"respectbuff-around-outpost";

	public static readonly Label RESPECT_FORCECLOSE = (Label)"respectbuff-forceclose";

	public static readonly Label RESPECT_COP_FAVOR = (Label)"respectbuff-cop-favor-pos";

	public static readonly Label HEAT_COP_ARREST = (Label)"heatbuff-arrest";

	public static readonly Label HEAT_OUTPOST_EXP = (Label)"heatbuff-outpost-expansion";

	public static readonly Label HEAT_OUTPOST_FIRED = (Label)"heatbuff-outpost-manager-fired";

	public static readonly Label HEAT_FORCECLOSE = (Label)"heatbuff-forceclose";

	public static readonly Label HEAT_BUILDING_DESTROYED = (Label)"heatbuff-building-destroyed";

	public static readonly Label HEAT_TRADE_ONETIME = (Label)"heatbuff-special-onetime-trade-history";

	public static readonly Label HEAT_TRADE_RECURRING = (Label)"heatbuff-special-recurring-trade-history";

	public static readonly Label HEAT_VIOLENCE = (Label)"heatbuff-violence";

	public static readonly Label HEAT_KILLING = (Label)"heatbuff-killing";

	public static readonly Label HEAT_FROM_CREW_PERSONALITY = (Label)"heatbuff-from-crew-personality";
}

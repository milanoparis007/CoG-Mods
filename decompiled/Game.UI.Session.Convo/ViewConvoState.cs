using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Services.Audio;
using Game.Session.Assets;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using Game.Session.Sim.Modules;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.Convo;

public class ViewConvoState : ViewContents
{
	private ConvoState _state;

	private GameObject _tmplChoiceList;

	private GameObject _tmplChoiceButton;

	private GameObject _list;

	private static readonly Label GAMBLING_BASE = new Label("gambling-base");

	public ViewConvoState(ConvoState state)
	{
		_state = state;
	}

	public override GameObject CreatePanel(GameObject container)
	{
		_tmplChoiceList = _dialog.Go.GetChild("Templates/Convo Choice List");
		_tmplChoiceButton = _dialog.Go.GetChild("Templates/Convo Choice Button");
		return Object.Instantiate(_tmplChoiceList, container.transform);
	}

	public override void OnAfterInitialize()
	{
		base.OnAfterInitialize();
		_list = _panel.GetChild("List");
		_list.DestroyAllChildren();
		UpdateName();
	}

	public override void OnBeforeRelease()
	{
		_list = null;
		base.OnBeforeRelease();
	}

	public override void RefreshContents()
	{
		base.RefreshContents();
		switch (_state.def.dynamicButtons)
		{
		case ConvoStateDef.ButtonGenerator.CustomBuySellList:
			GenerateDynamicBuySellButtons();
			break;
		case ConvoStateDef.ButtonGenerator.CustomChoiceButtons:
			GenerateQuestChoiceButtons();
			break;
		case ConvoStateDef.ButtonGenerator.CustomQuestDeliveryButtons:
			GenerateQuestDeliveryButtons();
			break;
		case ConvoStateDef.ButtonGenerator.CustomExpirationButtons:
			GenerateExpiredQuestButtons();
			break;
		case ConvoStateDef.ButtonGenerator.CustomDebtRepaymentButtons:
			GenerateDebtRepaymentButtons();
			break;
		case ConvoStateDef.ButtonGenerator.CustomGamblerBanButtons:
			GenerateGamblerBanButtons();
			break;
		case ConvoStateDef.ButtonGenerator.CustomGamblingModuleOptions:
			GenerateGamblingModuleButtons();
			break;
		case ConvoStateDef.ButtonGenerator.CustomSkillButtons:
			GenerateSkillButtons();
			break;
		case ConvoStateDef.ButtonGenerator.CustomSchemeButtons:
			GenerateSchemeButtons();
			break;
		case ConvoStateDef.ButtonGenerator.CustomCampaignActionButtons:
			GenerateCampaignActionButtons();
			break;
		case ConvoStateDef.ButtonGenerator.CustomJointWarTargetButtons:
			GenerateJointWarTargetButtons();
			break;
		default:
			Logger.Warning("Unknown dynamic button type: " + _state.def.dynamicButtons);
			break;
		case ConvoStateDef.ButtonGenerator.None:
			break;
		}
		GenerateStaticButtons();
	}

	private void UpdateName()
	{
		string message = _dialog.Model.visit.peep?.data.person.FullName ?? "";
		string text = Game.ctx.players.Human.social.WrapInPlayerColor(message);
		_panel.SetText("Header/Header", text);
	}

	private void GenerateStaticButtons()
	{
		int num = _state.def.buttons?.Count ?? 0;
		for (int i = 0; i < num; i++)
		{
			ConvoButtonDef def = _state.def.buttons[i];
			ConvoButton convoButton = new ConvoButton(i, def, _state.data?.CloneData());
			_dialog.Controller.OnButtonPreshow(convoButton);
			if (convoButton.IsVisible(_dialog.Model.visit))
			{
				_dialog.Controller.OnButtonShow(convoButton);
				MakeButtonCard(_state, convoButton);
			}
		}
	}

	private GameObject MakeButtonCard(ConvoState state, ConvoButton button, string forcetext = null)
	{
		GameObject gameObject = Object.Instantiate(_tmplChoiceButton, _list.transform);
		Button button2 = gameObject.GetButton();
		gameObject.GetOrAddComponent<ConversationButtonContext>().SetData(_dialog, state, button);
		bool flag = button.IsEnabled(_dialog.Model.visit);
		string btext = ((forcetext != null) ? forcetext : button.ProduceButtonText(_dialog.Model, flag));
		string message = button.ProduceButtonIcon(_dialog.Model);
		gameObject.SetText("Text", TextUtil.ColorEnabledIf(flag, btext));
		gameObject.SetText("Icon", TextUtil.ColorEnabledIf(flag, message));
		button2.interactable = flag;
		button2.onClick.SetListener(delegate
		{
			_dialog.Controller.OnConvoButtonPress(state, button, btext);
		});
		PlayUISound.UpdateEffectOn(gameObject, "", SFXType.ClickConvoConfirm);
		return gameObject;
	}

	private GameObject MakeQuestChoiceButtonCard(ConvoState state, ConvoButton button, QuestGrantChoice choice)
	{
		GameObject gameObject = MakeButtonCard(state, button);
		bool active = choice.visreqs == null || choice.visreqs.AllPass(_dialog.Model.visit);
		gameObject.SetActive(active);
		return gameObject;
	}

	private void GenerateQuestChoiceButtons()
	{
		ConvoDataQuestActive convoDataQuestActive = _state.data as ConvoDataQuestActive;
		ConvoButtonDef dynamicTemplate = _state.def.dynamicTemplate;
		List<QuestGrantChoice> choices = convoDataQuestActive.GetDef().choices;
		for (int i = 0; i < choices.Count; i++)
		{
			ConvoButton button = new ConvoButton(i, dynamicTemplate, convoDataQuestActive.CloneData());
			_dialog.Controller.OnButtonShow(button);
			MakeQuestChoiceButtonCard(_state, button, choices[i]);
		}
	}

	private void GenerateExpiredQuestButtons()
	{
		if (_state.data is ConvoDataQuestActive convoDataQuestActive && convoDataQuestActive.GetDef().expiration != null)
		{
			ConvoButton button = new ConvoButton(0, _state.def.dynamicTemplate, convoDataQuestActive.CloneData());
			_dialog.Controller.OnButtonShow(button);
			MakeButtonCard(_state, button);
		}
	}

	private void GenerateDebtRepaymentButtons()
	{
		ConvoData data = _state.data;
		ConvoDataGamblingDebtor data2 = data as ConvoDataGamblingDebtor;
		if (data2 == null)
		{
			return;
		}
		List<RepaymentChoiceAndSuccess> repayments = data2.repayments;
		ConvoButtonDef dynamicTemplate = _state.def.dynamicTemplate;
		for (int i = 0; i < repayments.Count; i++)
		{
			RepaymentChoiceAndSuccess repaymentChoiceAndSuccess = repayments[i];
			if (!(repaymentChoiceAndSuccess.choiceId == GamblingConstants.REPAYMENT_CASH))
			{
				GamblingRepayment repayment = Game.serv.globals.settings.gambling.FindRepaymentById(repaymentChoiceAndSuccess.choiceId);
				ConvoDataGamblingDebtor data3 = new ConvoDataGamblingDebtor(repayments, repaymentChoiceAndSuccess.choiceId, data2.nextCredit, repaymentChoiceAndSuccess.success, data2.gamblerId, data2.rollMoney);
				ConvoButton button = new ConvoButton(i, dynamicTemplate, data3);
				MakeRepaymentButtonCard(_state, button, repayment);
			}
		}
		GameObject MakeRepaymentButtonCard(ConvoState state, ConvoButton convoButton, GamblingRepayment gamblingRepayment)
		{
			GameObject gameObject = Object.Instantiate(_tmplChoiceButton, _list.transform);
			Button button2 = gameObject.GetButton();
			gameObject.GetOrAddComponent<ConversationButtonContext>().SetData(_dialog, state, convoButton);
			data2.selected = gamblingRepayment.id;
			VisitRequirementList reqs = gamblingRepayment.reqs;
			bool flag = (reqs == null || reqs.AllPass(_dialog.Model.visit)) && convoButton.IsEnabled(_dialog.Model.visit);
			string humanblurbmo = gamblingRepayment.convo.humanblurbmo;
			object[] replacements = data2.MakeReplacements(_dialog.Model.visit, 0);
			string forcedMO = Loc.Get(humanblurbmo, replacements) + ((reqs == null || reqs.Count == 0) ? "" : ("\n\n" + gamblingRepayment.reqs.Explain(_dialog.Model.visit)));
			string humanintro = gamblingRepayment.convo.humanintro;
			replacements = data2.MakeReplacements(_dialog.Model.visit, 0);
			string btext = Loc.Get(humanintro, replacements);
			string message = convoButton.ProduceButtonIcon(_dialog.Model);
			gameObject.GetOrAddComponent<ConversationButtonContext>().SetData(_dialog, state, convoButton, forcedMO);
			gameObject.SetText("Text", TextUtil.ColorEnabledIf(flag, btext));
			gameObject.SetText("Icon", TextUtil.ColorEnabledIf(flag, message));
			button2.interactable = flag;
			button2.onClick.SetListener(delegate
			{
				_dialog.Controller.OnConvoButtonPress(state, convoButton, btext);
			});
			PlayUISound.UpdateEffectOn(gameObject, "", SFXType.ClickConvoConfirm);
			return gameObject;
		}
	}

	private void GenerateGamblerBanButtons()
	{
		if (_state.data is ConvoDataGamblingCollect convoDataGamblingCollect)
		{
			List<EntityID> list = Game.ctx.players.Human.gambling.FindBannableGamblers(convoDataGamblingCollect.casino);
			ConvoButtonDef dynamicTemplate = _state.def.dynamicTemplate;
			for (int i = 0; i < list.Count; i++)
			{
				EntityID selected = list[i];
				ConvoDataGamblingCollect data = new ConvoDataGamblingCollect(convoDataGamblingCollect.delta, convoDataGamblingCollect.current, convoDataGamblingCollect.operation, convoDataGamblingCollect.vehAmt, convoDataGamblingCollect.casino, selected);
				ConvoButton button = new ConvoButton(i, dynamicTemplate, data);
				MakeBanGamblingCard(_state, button, selected);
			}
		}
		GameObject MakeBanGamblingCard(ConvoState state, ConvoButton convoButton, EntityID item)
		{
			_ = convoButton.state;
			GameObject gameObject = Object.Instantiate(_tmplChoiceButton, _list.transform);
			Button button2 = gameObject.GetButton();
			gameObject.GetOrAddComponent<ConversationButtonContext>().SetData(_dialog, state, convoButton);
			GamblingModule gambling = _dialog.Model.visit.building.components.modules.gambling;
			bool flag = false;
			foreach (AmenityData amenity in gambling.data.amenities)
			{
				AmenityDef amenityDef = Game.serv.globals.settings.gambling.FindAmenityById(amenity.defID);
				if (amenity.gamblers.Contains(item) && amenityDef.behavior.type == AmenityDef.AmenityBehavior.BehaviorType.Rake)
				{
					flag = true;
				}
			}
			string text;
			if (!flag)
			{
				object[] replacements = convoButton.state.data.MakeReplacements(_dialog.Model.visit, 0);
				text = Loc.Get("convo.gambling.ban-gambler.human-choose", replacements);
			}
			else
			{
				object[] replacements = convoButton.state.data.MakeReplacements(_dialog.Model.visit, 0);
				text = Loc.Get("convo.gambling.ban-gambler.human-choose.rake", replacements);
			}
			string btext = text;
			string message = convoButton.ProduceButtonIcon(_dialog.Model);
			gameObject.SetText("Text", TextUtil.ColorEnabledIf(predicate: true, btext));
			gameObject.SetText("Icon", TextUtil.ColorEnabledIf(predicate: true, message));
			button2.interactable = true;
			button2.onClick.SetListener(delegate
			{
				_dialog.Controller.OnConvoButtonPress(state, convoButton, btext);
			});
			PlayUISound.UpdateEffectOn(gameObject, "", SFXType.ClickConvoConfirm);
			return gameObject;
		}
	}

	private void GenerateGamblingModuleButtons()
	{
		if (!(_state.data is ConvoDataGamblingHouseSelection convoDataGamblingHouseSelection))
		{
			return;
		}
		ModQuery query = new ModQuery(PlayerID.HumanPlayer, EntityID.INVALID, _dialog.Model.visit.crew.peepId);
		List<IModuleConfig> list = ModulesUtil.FindAllGamblingModuleDefsExpensive().ToList();
		ConvoButtonDef dynamicTemplate = _state.def.dynamicTemplate;
		for (int i = 0; i < list.Count; i++)
		{
			GamblingModuleConfig gamblingModuleConfig = list[i] as GamblingModuleConfig;
			if (!(gamblingModuleConfig.Id == GAMBLING_BASE))
			{
				Fixnum fixnum = gamblingModuleConfig.gambling.purchaseCost.Evaluate(query);
				ConvoDataGamblingHouseSelection data = new ConvoDataGamblingHouseSelection(convoDataGamblingHouseSelection.entries, gamblingModuleConfig.Id, convoDataGamblingHouseSelection.selected);
				ConvoButton convoButton = new ConvoButton(i, dynamicTemplate, data);
				if (gamblingModuleConfig.gambling.installVisReqs.AllPass(_dialog.Model.visit, convoButton.state))
				{
					MakeButtonCard(_state, convoButton, Loc.Get(gamblingModuleConfig.Common.display.locInstallDetail, "cost", Loc.Money(fixnum.Abs)));
				}
			}
		}
	}

	private void GenerateDynamicBuySellButtons()
	{
		VisitState visit = _dialog.Model.visit;
		ConvoBuySellDefs convoBuySellDefs = ConvoBlurbUtils.GenerateBuySellDefs(visit);
		int num = 0;
		ConvoButtonDef dynamicTemplate = _state.def.dynamicTemplate;
		for (int i = 0; i < convoBuySellDefs.defs.Count; i++)
		{
			ConvoBuySellDefs.Item item = convoBuySellDefs.defs[i];
			if (!item.lockedIllegal && !item.lockedUnknown)
			{
				Fixnum item2 = visit.biz.components.biz.GetDiscount(visit.pid, item.elt.item.FindResource()).multiplier;
				ConvoDataBuySell data = new ConvoDataBuySell(item.elt, item.info, item.haveInCar, item.playerBuys, item2);
				ConvoButton button = new ConvoButton(i, dynamicTemplate, data);
				MakeButtonCard(_state, button);
				num++;
			}
		}
		MaybeMakeFakeButtonCards(num);
		void MakeFakeButtonCard(ConvoState state, string message, string icon, bool forceFirst)
		{
			GameObject gameObject = Object.Instantiate(_tmplChoiceButton, _list.transform);
			if (forceFirst)
			{
				gameObject.gameObject.transform.SetAsFirstSibling();
			}
			string forcedMO = null;
			gameObject.GetOrAddComponent<ConversationButtonContext>().SetData(_dialog, state, null, forcedMO);
			gameObject.SetText("Text", message);
			gameObject.SetText("Icon", icon);
			gameObject.GetButton().interactable = false;
		}
		void MaybeMakeFakeButtonCards(int count)
		{
			string pluralized = Loc.GetPluralized(Game.serv.globals.settings.people.businessSettings.buySellIllegalEtc.header, count);
			MakeFakeButtonCard(_state, pluralized, "", forceFirst: true);
		}
	}

	private static (string icon, string message, string mo) MakeDeliveryTexts(Resource res, int needed, int deliverable, bool all, bool some)
	{
		string item = res?.GetIcon() ?? Loc.Get("ui.viewinventory.invcard.cash.icon");
		string text = res?.GetIconAndName() ?? Loc.Get("ui.viewinventory.invcard.cash.icon-and-word");
		string text2 = res?.unitdef.GetQtyAndUnits(needed) ?? Loc.Money(needed);
		string text3 = res?.unitdef.GetQtyAndUnits(deliverable) ?? Loc.Money(deliverable);
		string key = (all ? "convo.delivery-items-all-button" : (some ? "convo.delivery-items-some-button" : "convo.delivery-items-none-button"));
		string key2 = (all ? "convo.delivery-items-all-button.mo" : (some ? "convo.delivery-items-some-button.mo" : "convo.delivery-items-none-button.mo"));
		string item2 = Loc.Get(key, "current-resources", text3, "remaining-resources", text2, "icon-and-res", text);
		string item3 = Loc.Get(key2);
		return (icon: item, message: item2, mo: item3);
	}

	private GameObject MakeCardAndUpdateData(ConvoState state, ConvoButton button, InventoryModule inv, ResOrCash item)
	{
		Resource res = (item.IsCash ? null : item.raq.FindResource());
		Fixnum fixnum = (item.IsCash ? item.money.cash : item.raq.qty);
		Fixnum obj = (item.IsCash ? inv.data.money.cash : inv.data.Get(res).qty);
		Fixnum fixnum2 = Fixnum.Min(obj, fixnum);
		bool all = obj >= fixnum;
		bool flag = obj > 0;
		(string icon, string message, string mo) tuple = MakeDeliveryTexts(res, (int)fixnum, (int)fixnum2, all, flag);
		string item2 = tuple.icon;
		string item3 = tuple.message;
		string item4 = tuple.mo;
		GameObject gameObject = MakeButtonCard(state, button);
		button.GetData<ConvoDataQuestActive>().delivered = (item.IsCash ? new ResOrCash(new Money(fixnum2)) : new ResOrCash(item.raq.SetQuantity(fixnum2)));
		gameObject.SetText("Text", TextUtil.ColorEnabledIf(flag, item3));
		gameObject.SetText("Icon", item2);
		gameObject.GetComponent<ConversationButtonContext>().forcedMO = item4;
		gameObject.GetButton().interactable = flag;
		return gameObject;
	}

	private void GenerateQuestDeliveryButtons()
	{
		ConvoDataQuestActive convoDataQuestActive = _state.data as ConvoDataQuestActive;
		InventoryModule inventory = ModulesUtil.GetInventory(_dialog.Model.visit.vehicle);
		if (inventory == null)
		{
			return;
		}
		List<ResOrCash> list = Game.ctx.quests.MakeListOfResourcesOutstanding(convoDataQuestActive.quuid);
		for (int i = 0; i < list.Count; i++)
		{
			ResOrCash item = list[i];
			if (item.IsCash ? item.money.IsNotZero : item.raq.qty.IsNotZero)
			{
				ConvoButton button = new ConvoButton(i, _state.def.dynamicTemplate, convoDataQuestActive.CloneData());
				MakeCardAndUpdateData(_state, button, inventory, item);
			}
		}
	}

	private void GenerateSkillButtons()
	{
		ConvoDataLearnSkill convoDataLearnSkill = _state.data as ConvoDataLearnSkill;
		List<Label> learnableSkills = convoDataLearnSkill.learnableSkills;
		ConvoButtonDef dynamicTemplate = _state.def.dynamicTemplate;
		for (int i = 0; i < learnableSkills.Count; i++)
		{
			SkillDef skill = Game.serv.globals.settings.skills.GetSkill(learnableSkills[i]);
			ConvoDataLearnSkill data = new ConvoDataLearnSkill(convoDataLearnSkill.learnableSkills, _dialog.Model.visit, learnableSkills[i]);
			ConvoButton button = new ConvoButton(i, dynamicTemplate, data);
			MakeButtonCard(_state, button, Loc.Get("convo.ticket-skills-select-npc.skillchoice", "skillname", skill.GetName()));
		}
	}

	private void GenerateSchemeButtons()
	{
		List<SchemeDef> allSchemeDefs = Game.serv.globals.settings.schemes.GetAllSchemeDefs();
		for (int i = 0; i < allSchemeDefs.Count; i++)
		{
			ConvoButtonDef convoButtonDef = Game.serv.serializer.instance.Clone(_state.def.dynamicTemplate);
			SchemeDef schemeDef = allSchemeDefs[i];
			ConvoDataScheme data = new ConvoDataScheme(allSchemeDefs[i].id);
			if (!schemeDef.startup.visreqs.AllPass(_dialog.Model.visit))
			{
				continue;
			}
			convoButtonDef.reqs = schemeDef.startup.reqs;
			string text = Loc.Get(schemeDef.display.locconvo) + "\n";
			foreach (ResOrCash item in schemeDef.startup.cost)
			{
				if (item.IsCash)
				{
					text = text + Loc.Get("convo.crew.scheme-cost.money", "cash", Loc.Money(-item.money)) + "\n";
				}
				if (item.IsResource)
				{
					string text2 = text;
					object[] obj = new object[2] { "resAndQty", null };
					ResourceAndQty raq = item.raq;
					obj[1] = raq.MakeQuantityLocString();
					text = text2 + Loc.Get("convo.crew.scheme-cost.res", obj) + "\n";
				}
			}
			ConvoButton button = new ConvoButton(i, convoButtonDef, data);
			MakeButtonCard(_state, button, text);
		}
	}

	private void GenerateCampaignActionButtons()
	{
		List<PoliticsSettings.CandidateAction> playerActionDefs = Game.serv.globals.settings.politics.npcCandidateAI.playerActionDefs;
		Entity npc = _dialog.Model.visit.npc;
		Ward wardForID = Game.ctx.simman.politics.GetWardForID(_dialog.Model.visit.building.components.board.GetNode().precinctId);
		Label archetype = Game.ctx.simman.politics.GetPoliticianData(npc.Id).archetypeId;
		List<PoliticsSettings.CandidateAction> list = playerActionDefs.Where((PoliticsSettings.CandidateAction x) => CanPerformAction(x)).ToList();
		for (int num = 0; num < list.Count; num++)
		{
			ConvoButtonDef convoButtonDef = Game.serv.serializer.instance.Clone(_state.def.dynamicTemplate);
			PoliticsSettings.CandidateAction candidateAction = list[num];
			ConvoDataPolitician data = new ConvoDataPolitician(npc.Id, candidateAction.id, wardForID.id);
			VisitRequirementList visreqs = candidateAction.visreqs;
			if (visreqs == null || visreqs.AllPass(_dialog.Model.visit))
			{
				convoButtonDef.reqs = candidateAction.reqs;
				ConvoButton convoButton = new ConvoButton(num, convoButtonDef, data);
				GameObject obj = MakeButtonCard(_state, convoButton, Loc.Get(candidateAction.locconvo, "price", Loc.Money(candidateAction.warchestCost.Abs)));
				string forcedMO = ((candidateAction.reqs != null) ? candidateAction.reqs.Explain(_dialog.Model.visit, convoButton.state) : null);
				obj.GetOrAddComponent<ConversationButtonContext>().SetData(_dialog, _state, convoButton, forcedMO);
			}
		}
		bool CanPerformAction(PoliticsSettings.CandidateAction action)
		{
			if (action.displayInConvo)
			{
				return IsRightArchetype(action);
			}
			return false;
		}
		bool IsRightArchetype(PoliticsSettings.CandidateAction action)
		{
			if (action.validArchetypes != null)
			{
				return action.validArchetypes.Contains(archetype);
			}
			return true;
		}
	}

	private void GenerateJointWarTargetButtons()
	{
		List<PlayerID> list = (from x in Game.ctx.players.Human.meetings.GetPlayersAlreadyMet()
			where IsValidJointWarTarget(x)
			select x).ToList();
		_ = _state.data;
		for (int num = 0; num < list.Count; num++)
		{
			ConvoButtonDef def = Game.serv.serializer.instance.Clone(_state.def.dynamicTemplate);
			PlayerID playerID = list[num];
			ConvoDataGangRequests data = new ConvoDataGangRequests(PlayerID.HumanPlayer, _dialog.Model.visit.npc.data.agent.pid, accept: false, 0, 0, playerID);
			ConvoButton button = new ConvoButton(num, def, data);
			MakeButtonCard(_state, button, Loc.Get("convo.gangs.joint-war-choose-target.say", "outfitName", playerID.FindPlayer().social.FindPlayerGroupNameColorized()));
		}
		bool IsValidJointWarTarget(PlayerID player)
		{
			if (player.FindPlayer().IsJustGang && player.FindPlayer().ai.combat.GetAggroAndTruce(PlayerID.HumanPlayer).isAggro)
			{
				return !_dialog.Model.visit.npc.data.agent.pid.FindPlayer().ai.combat.HasJointWar(PlayerID.HumanPlayer, player);
			}
			return false;
		}
	}
}

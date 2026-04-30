using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Services.Input;
using Game.Session;
using Game.Session.Data;
using Game.Session.Player;
using Game.UI.Mouseovers;
using Game.UI.Session.Popups;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI;

public class LawPopup : BasePopup
{
	private const string CLOSE_BUTTON = "Close/";

	private const string NEXT_BUTTON = "Footer/NextCrew";

	private const string PREV_BUTTON = "Footer/PrevCrew";

	private const string TEMPLATE_ACTIVE_LAW = "Templates/Enacted Law";

	private const string TEMPLATE_AVAILABLE_LAW = "Templates/Available Law";

	private const string TEMPLATE_NO_ACTIVE_LAW = "Templates/No Laws Active";

	public GameObject _templateActiveLaw;

	public GameObject _templateAvailableLaw;

	public GameObject _templateNoActiveLawWarning;

	private const string CITY_HEADER = "Panel/Header/Title";

	private const string YEAR_HEADER = "Panel/Header/Year";

	private const string STAMP = "Panel/Header/Stamp";

	private const string STAMP_CITY = "Panel/Header/Stamp/City";

	private const string STAMP_DATE = "Panel/Header/Stamp/Date";

	private const string INFLUENCE_HEADER = "Panel/Footer/Influence Header";

	private const string INFLUENCE_VALUE = "Panel/Footer/Influence/Text";

	private const string SIGNATURE_DESC = "Panel/Footer/Signature Desc";

	private const string ACTIVE_CONTAINER = "Panel/Active/Viewport/Content";

	private const string AVAILABLE_CONTAINER = "Panel/Available/Viewport/Content";

	private const string ACTIVE_HEADER_INSTRUCTION = "Panel/Active Header/Instructions";

	private const string AVAILABLE_HEADER_INSTRUCTION = "Panel/Available Header/Instructions";

	private const string LAW_NAME = "Name";

	private const string LAW_BUTTON = "Enact Button";

	private const string LAW_CHECK = "Check";

	private const string LAW_BUTTON_TEXT = "Enact Button/Text";

	public override UIReference UIReference => UIElements.LawShopPopup;

	public bool IsLegislativeSession => Game.ctx.clock.Now.ToDate().Month == Game.serv.globals.settings.politics.elections.legislationStartMonth;

	protected override void InitializeOnPush()
	{
		_templateActiveLaw = _go.GetChild("Templates/Enacted Law");
		_templateAvailableLaw = _go.GetChild("Templates/Available Law");
		_templateNoActiveLawWarning = _go.GetChild("Templates/No Laws Active");
		RefreshDialog();
		_go.GetButton("Close/").onClick.AddListener(Close);
		Game.serv.mouseovers.Register(MouseoverType.PoliticsLaw, new LawDefMouseover());
		Game.serv.mouseovers.Register(MouseoverType.PoliticsInfluenceTotal, new PoliticalInfluenceMouseover());
	}

	public override void OnActivated(bool pushed)
	{
		base.OnActivated(pushed);
		Game.ctx.events.EnqueueOnce(SessionEventType.LawbookLook);
	}

	protected override void ReleaseOnPop()
	{
		Game.serv.mouseovers.Unregister(MouseoverType.PoliticsLaw);
		Game.serv.mouseovers.Unregister(MouseoverType.PoliticsInfluenceTotal);
		_go.GetButton("Close/").onClick.RemoveListener(Close);
		_templateActiveLaw = (_templateAvailableLaw = (_templateNoActiveLawWarning = null));
	}

	protected override void InitializeKeyHandler()
	{
		_keyhandler = new PopupHandlerWithTabSupport();
	}

	private void StartGame()
	{
	}

	public void RefreshDialog()
	{
		InitializeHeader();
		InitializeStamp();
		InitializeFooter();
		InitializeOnTheBooks();
		InitializeShop();
	}

	private void InitializeHeader()
	{
		_go.SetText("Panel/Header/Title", Loc.Get("ui.law-shop.header.title", "city", Game.ctx.session.mapconfig.CityName.ToUpper()));
		_go.SetText("Panel/Header/Year", Loc.Get("ui.law-shop.header.year", "year", Game.ctx.clock.Now.ToDate().Year));
	}

	private void InitializeStamp()
	{
		_go.GetChild("Panel/Header/Stamp").SetActive(!IsLegislativeSession);
		_go.SetText("Panel/Header/Stamp/City", Game.ctx.session.mapconfig.CityName.ToUpper());
		_go.SetText("Panel/Header/Stamp/Date", Loc.FormatDateMonthShortened(Game.ctx.clock.GetFirstTurnOfMonthThisYear(Game.serv.globals.settings.politics.elections.legislationEndMonth), showyear: true));
	}

	private void InitializeFooter()
	{
		_go.SetText("Panel/Footer/Influence Header", Loc.Get("ui.law-shop.footer.influence"));
		_go.SetText("Panel/Footer/Influence/Text", Loc.Get("ui.law-shop.influence-value", "value", Game.ctx.simman.politics.GetCurrentInfluence(PlayerID.HumanPlayer)));
		_go.SetText("Panel/Footer/Signature Desc", Loc.Get("ui.law-shop.footer.signature-desc", "city", Game.ctx.session.mapconfig.CityName));
	}

	private void InitializeOnTheBooks()
	{
		GameObject child = _go.GetChild("Panel/Active/Viewport/Content");
		List<Label> enactedLaws = Game.ctx.simman.politics.GetEnactedLaws();
		List<PoliticsSettings.Law> data = enactedLaws.Select((Label x) => Game.serv.globals.settings.politics.FindLawDef(x)).ToList();
		child.EnsureChildCount(data, _templateActiveLaw);
		child.InitializeChildren(data, InitLaw);
		if (enactedLaws.Count == 0)
		{
			Object.Instantiate(_templateNoActiveLawWarning, child.transform);
		}
		_go.GetChild("Panel/Active Header/Instructions").SetActive(IsLegislativeSession);
	}

	private void InitializeShop()
	{
		Game.ctx.simman.politics.GetElectionStage();
		GameObject child = _go.GetChild("Panel/Available/Viewport/Content");
		List<PoliticsSettings.Law> lawDefs = Game.serv.globals.settings.politics.lawSettings.lawDefs;
		CrewAssignment crew = Game.ctx.players.Human.crew.GetCrewForPlayerPeep();
		List<PoliticsSettings.Law> data = lawDefs.Where((PoliticsSettings.Law x) => IsBuyableLaw(x)).ToList();
		child.EnsureChildCount(data, _templateAvailableLaw);
		child.InitializeChildren(data, InitLaw);
		_go.GetChild("Panel/Available Header/Instructions").SetActive(IsLegislativeSession);
		bool IsBuyableLaw(PoliticsSettings.Law law)
		{
			if (!Game.ctx.simman.politics.IsLawEnacted(law.id))
			{
				return law.visreqs?.AllPass(new VisitState(crew, Game.ctx.clock.Now, PlayerID.HumanPlayer)) ?? true;
			}
			return false;
		}
	}

	private void InitLaw(int i, GameObject card, PoliticsSettings.Law info)
	{
		bool enacted = Game.ctx.simman.politics.IsLawEnacted(info.id);
		card.SetText("Name", Loc.Get(info.locname));
		card.GetText("Name").color = ((enacted || IsLegislativeSession) ? ColorUtil.HexToColor("#595959") : ColorUtil.HexToColor("8d8d8d"));
		card.GetOrAddComponent<LawCtx>().Set(info.id);
		if (enacted)
		{
			card.GetChild("Check").SetActive(!IsLegislativeSession);
		}
		card.SetText("Enact Button/Text", Loc.Get("ui.law-shop.influence-value", "value", enacted ? info.GetRevokePrice() : info.buyPrice));
		card.GetButton("Enact Button").onClick.SetListener(delegate
		{
			OnClickBuyEnactOrRevokeLaw(info);
		});
		card.GetButton("Enact Button").interactable = CanBuyLaw();
		card.GetChild("Enact Button").SetActive(IsLegislativeSession);
		bool CanBuyLaw()
		{
			if (Game.ctx.simman.politics.GetCurrentInfluence(PlayerID.HumanPlayer) >= (enacted ? info.GetRevokePrice() : info.buyPrice))
			{
				return info.reqs?.AllPass(new VisitState(CrewAssignment.EMPTY, Game.ctx.clock.Now, PlayerID.HumanPlayer)) ?? true;
			}
			return false;
		}
	}

	public void OnClickBuyEnactOrRevokeLaw(PoliticsSettings.Law law)
	{
		if (!Game.ctx.simman.politics.IsLawEnacted(law.id))
		{
			OkPopup.ShowOkCancel(Loc.Get("ui.law-shop.buy-confirm", "influence", Loc.Get("ui.law-shop.influence-value", "value", law.buyPrice), "law", Loc.Get(law.locname)), delegate
			{
				BuyEnactLaw(law);
			}, delegate
			{
			});
		}
		else
		{
			OkPopup.ShowOkCancel(Loc.Get("ui.law-shop.revoke-confirm", "influence", Loc.Get("ui.law-shop.influence-value", "value", law.GetRevokePrice()), "law", Loc.Get(law.locname)), delegate
			{
				BuyRevokeLaw(law);
			}, delegate
			{
			});
		}
		RefreshDialog();
	}

	public void BuyEnactLaw(PoliticsSettings.Law law)
	{
		Game.ctx.simman.politics.DoChangeInfluenceWithoutLogging(PlayerID.HumanPlayer, -law.buyPrice);
		Game.ctx.simman.politics.EnactLaw(law);
		RefreshDialog();
	}

	public void BuyRevokeLaw(PoliticsSettings.Law law)
	{
		Game.ctx.simman.politics.DoChangeInfluenceWithoutLogging(PlayerID.HumanPlayer, -law.GetRevokePrice());
		Game.ctx.simman.politics.RevokeLaw(law);
		RefreshDialog();
	}
}

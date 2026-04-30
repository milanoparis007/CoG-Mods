using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Player.KB;
using SomaSim.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI.Session.Crew;

public sealed class CrewInfoListDialog : BaseHUDDialog
{
	private class CardInfo
	{
		public CrewAssignment crew;

		public Entity hireling;

		public Entity gotoLocation;

		public bool isCurrent
		{
			get
			{
				if (crew.IsValid)
				{
					return crew.IsNotDead;
				}
				return false;
			}
		}

		public bool isPast
		{
			get
			{
				if (crew.IsValid)
				{
					return crew.IsDead;
				}
				return false;
			}
		}

		public bool isPotential
		{
			get
			{
				if (crew.IsNotValid)
				{
					return hireling != null;
				}
				return false;
			}
		}

		public Entity GetPeep()
		{
			return hireling ?? crew.GetPeep();
		}
	}

	private class CardContext : MonoBehaviour
	{
		public CardInfo info;

		public void Set(CardInfo info)
		{
			this.info = info;
		}
	}

	private const string TEMPLATES = "Templates";

	private const string TMPL_CREW_CARD = "Templates/Crew Info List Card";

	private const string TOGGLE_CURRENT = "Panel/Toggles/Current";

	private const string TOGGLE_PAST = "Panel/Toggles/Past";

	private const string TOGGLE_POTENTIAL = "Panel/Toggles/Potential";

	private const string CLOSE_BUTTON = "Panel/Close Button";

	private const string CARD_CONTAINER = "Panel/Scroll View Items/Viewport/Content";

	private const string DESC_CONTAINER = "Panel/Scroll View Text/Viewport/Content";

	private const string DESC_TEXT = "Panel/Scroll View Text/Viewport/Content/Text";

	private const string FILTER_DROP = "Panel/Trait Filter";

	private GameObject _tmplCard;

	private Toggle _toggleCurrent;

	private Toggle _togglePast;

	private Toggle _togglePotential;

	private GameObject _cardContainer;

	private GameObject _descContainer;

	private TextMeshProUGUI _descText;

	private TMP_Dropdown _filter;

	private TraitList _traitList;

	private const string CARD_TEXT = "Text";

	private const string CARD_PORTRAIT = "Portrait/Portrait";

	private const string CARD_GOTO = "Goto";

	public override bool ShowAtStartup => false;

	public override TweenType Tween => TweenType.Right;

	public override UIReference UIReference => UIElements.CrewInfoListDialog;

	internal override void Initialize()
	{
		base.Initialize();
		_go.SetActive("Templates", value: false);
		_tmplCard = _go.GetChild("Templates/Crew Info List Card");
		_go.SetButtonListener("Panel/Close Button", OnCloseButton);
		_toggleCurrent = _go.GetToggle("Panel/Toggles/Current");
		_togglePast = _go.GetToggle("Panel/Toggles/Past");
		_togglePotential = _go.GetToggle("Panel/Toggles/Potential");
		_toggleCurrent.onValueChanged.SetListener(OnAnyToggleChanged);
		_togglePast.onValueChanged.SetListener(OnAnyToggleChanged);
		_togglePotential.onValueChanged.SetListener(OnAnyToggleChanged);
		_cardContainer = _go.GetChild("Panel/Scroll View Items/Viewport/Content");
		_descContainer = _go.GetChild("Panel/Scroll View Text/Viewport/Content");
		_descText = _go.GetText("Panel/Scroll View Text/Viewport/Content/Text");
		InitializeFilterDrop();
		Game.ctx.events.AddListener(SessionEventType.SelectionActivationChange, OnCloseEvent);
		Game.ctx.events.AddListener(SessionEventType.HumanPlayerTurnEnded, OnCloseEvent);
	}

	internal override void Release()
	{
		Game.ctx.events.RemoveListener(SessionEventType.SelectionActivationChange, OnCloseEvent);
		Game.ctx.events.RemoveListener(SessionEventType.HumanPlayerTurnEnded, OnCloseEvent);
		_descText = null;
		_cardContainer = (_descContainer = null);
		_toggleCurrent = (_togglePast = (_togglePotential = null));
		base.Release();
	}

	private void OnCloseEvent(SessionEvent obj)
	{
		Hide();
	}

	private void OnCloseButton()
	{
		Hide();
	}

	private void OnAnyToggleChanged(bool isOn)
	{
		RefreshContents();
	}

	private void InitializeFilterDrop()
	{
		_filter = _go.GetDropdown("Panel/Trait Filter");
		_traitList = new TraitList();
		_traitList.AddRange(Game.serv.globals.settings.people.traits.OrderBy((Trait x) => x.GetLocName()));
		_filter.SetOptions(Loc.Get("ui.crewlistinfo.any-trait"));
		_filter.AddOptions(_traitList.Select((Trait x) => x.GetLocName()).ToList());
		_filter.SetValueWithoutNotify(0);
		_filter.onValueChanged.SetListener(delegate
		{
			RefreshContents();
		});
	}

	protected override void RefreshContents()
	{
		List<CardInfo> list = new List<CardInfo>();
		Trait filterTrait = ((_filter.value == 0) ? null : _traitList.GetOrDefaultFast(_filter.value - 1));
		CompilePersonList(list, filterTrait);
		_cardContainer.EnsureChildCount(list, _tmplCard);
		_cardContainer.InitializeChildren(list, InitializeCard);
		_go.SetText("Panel/Scroll View Text/Viewport/Content/Text", "");
	}

	private void CompilePersonList(List<CardInfo> results, Trait filterTrait)
	{
		results.AddRange(GetCrewInfos(living: true, filterTrait));
		results.AddRange(GetCrewInfos(living: false, filterTrait));
		results.AddRange(GetPotentialInfos(filterTrait));
	}

	private void InitializeCard(int index, GameObject card, CardInfo info)
	{
		bool flag = CheckVisibility(info);
		card.SetActive(flag);
		if (flag)
		{
			card.GetOrAddComponent<CardContext>().Set(info);
			card.GetButton().onClick.SetListener(delegate
			{
				OnCardClick(info);
			});
			card.GetButton("Goto").onClick.SetListener(delegate
			{
				OnGotoClick(info);
			});
			card.SetActive("Goto", info.gotoLocation != null);
			Entity peep = info.GetPeep();
			string key = (info.isCurrent ? "ui.crewlistinfo.tmpl.current" : (info.isPast ? "ui.crewlistinfo.tmpl.past" : "ui.crewlistinfo.tmpl.potential"));
			string text = peep.data.person.FullName;
			if (info.isCurrent)
			{
				text = peep.components.agent.WrapWithRankIcon(text);
			}
			card.SetText("Text", Loc.Get(key, "name", text));
			card.SetImageOrHide("Portrait/Portrait", HUDUtil.GetCrewSprite(peep));
		}
	}

	private bool CheckVisibility(CardInfo info)
	{
		return (info.isCurrent ? _toggleCurrent : (info.isPast ? _togglePast : _togglePotential)).isOn;
	}

	private void OnGotoClick(CardInfo info)
	{
		PersonInfoUtil.TweenCameraToEntity(info.gotoLocation);
	}

	private void OnCardClick(CardInfo info)
	{
		_go.SetText("Panel/Scroll View Text/Viewport/Content/Text", GenerateDescription(info));
	}

	private IEnumerable<CardInfo> GetCrewInfos(bool living, Trait filterTrait)
	{
		return from crew in Game.ctx.players.Human.crew.AllCrew
			where crew.IsNotDead == living
			where filterTrait == null || crew.GetPeep().data.person.traitIds.Contains(filterTrait.id)
			select new CardInfo
			{
				crew = crew,
				gotoLocation = crew.GetTarget()
			} into card
			orderby card.crew.GetPeep().data.person.last.ToLower()
			select card;
	}

	private IEnumerable<CardInfo> GetPotentialInfos(Trait filterTrait)
	{
		Dictionary<EntityID, CardInfo> dictionary = new Dictionary<EntityID, CardInfo>(new EntityIDEqualityComparer());
		PlayerInfo human = Game.ctx.players.Human;
		foreach (Node item in human.territory.GetAllKnownNodesExpensive())
		{
			foreach (EntityID item2 in item.interesting)
			{
				Entity entity = item2.FindEntity();
				if (!human.territory.IsScoped(entity))
				{
					continue;
				}
				KBResult statusForBuilding = human.kb.GetStatusForBuilding(item2, PlayerKBQueryNames.OWNER_CAN_HIRE_CREW);
				if (statusForBuilding.IsNotValid || !statusForBuilding.passed)
				{
					continue;
				}
				Entity entity2 = BuildingUtil.FindOwnerOrManagerForAnyBuilding(entity);
				if (entity2 != null)
				{
					EntityID bestCrewCandidateFrom = human.crew.CrewGrowth.GetBestCrewCandidateFrom(entity2.Id);
					bool flag = true;
					if (filterTrait != null)
					{
						flag = bestCrewCandidateFrom.FindEntity().data.person.traitIds.Contains(filterTrait.id);
					}
					if (!dictionary.ContainsKey(bestCrewCandidateFrom) && flag)
					{
						CardInfo value = new CardInfo
						{
							hireling = bestCrewCandidateFrom.FindEntity(),
							gotoLocation = entity
						};
						dictionary.Add(bestCrewCandidateFrom, value);
					}
				}
			}
		}
		return dictionary.Values.OrderBy((CardInfo card) => card.hireling.data.person.last.ToLower());
	}

	private string GenerateDescription(CardInfo info)
	{
		Entity peep = info.GetPeep();
		PersonInfoUtil.Overview overview = PersonInfoUtil.GenerateOverview(peep, details: true);
		string text = GenerateHeader(peep, overview);
		string text2 = (info.isCurrent ? GenerateCurrentInfo(peep, overview) : (info.isPast ? GeneratePastInfo(peep, overview) : GeneratePotentialInfo(peep, info.gotoLocation, overview)));
		string text3 = (info.isPast ? "" : GenerateDetails(overview));
		string text4 = "";
		if (peep.data.agent.pid.IsAnyPlayer)
		{
			foreach (KeyValuePair<CrewStats, int> mostRelevantCrewStat in peep.components.agent.GetMostRelevantCrewStats())
			{
				if (mostRelevantCrewStat.Value != 0)
				{
					text4 = text4 + Loc.GetCrewStatWithValueSpace(mostRelevantCrewStat.Key, Loc.GetCrewStatUnit(mostRelevantCrewStat.Key, mostRelevantCrewStat.Value)) + "\n";
				}
			}
		}
		return (text + "\n\n" + text2 + "\n\n" + text3 + "\n\n" + text4).Trim();
	}

	private string GenerateHeader(Entity _, PersonInfoUtil.Overview data)
	{
		string workplaceOrUnemployed = data.GetWorkplaceOrUnemployed();
		string relAgeAndEth = data.GetRelAgeAndEth();
		return Loc.Get("ui.crewlistinfo.desc.header", "name", data.name, "workplace", workplaceOrUnemployed, "details", relAgeAndEth);
	}

	private string GenerateCurrentInfo(Entity peep, PersonInfoUtil.Overview _)
	{
		bool isDead = !peep.data.person.IsAlive;
		var (price, text) = Game.ctx.players.Human.finances.GetCrewSalary(peep.Id, isDead, explain: true);
		return Loc.Get("ui.crewlistinfo.desc.current", "money", Loc.Price(price), "details", text);
	}

	private string GeneratePastInfo(Entity peep, PersonInfoUtil.Overview _)
	{
		(bool, Price) supportPayments = Game.ctx.players.Human.crew.GetSupportPayments(peep.Id);
		string text = Loc.Price(supportPayments.Item1 ? supportPayments.Item2.Abs : default(Price));
		int yearsInt = peep.data.person.died.YearsInt;
		return Loc.Get("ui.crewlistinfo.desc.past", "money", text, "year", yearsInt);
	}

	private string GeneratePotentialInfo(Entity peep, Entity building, PersonInfoUtil.Overview data)
	{
		string firstName = peep.data.person.FirstName;
		string text = BuildingUtil.FindOwnerOrManagerForAnyBuilding(building)?.data.person.FullName;
		if (firstName != null && text != null)
		{
			return Loc.Get("ui.crewlistinfo.desc.potential", "firstname", firstName, "friendname", text);
		}
		return "";
	}

	private string GenerateDetails(PersonInfoUtil.Overview data)
	{
		return Loc.Get("ui.crewlistinfo.desc.details", "traits", data.traitsLong, "levelups", data.levelups ?? "");
	}
}

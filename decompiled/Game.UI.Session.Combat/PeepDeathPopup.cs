using System;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Sim;
using SomaSim.Util;

namespace Game.UI.Session.Combat;

public sealed class PeepDeathPopup : BasePopup
{
	private const string TEXT = "Panel/Header/Contents";

	private const string DECEASED = "Panel/Header/Deceased";

	private const string BTN_SUPPORT = "Panel/Footer/Support Button";

	private const string BTN_QUEST = "Panel/Footer/Help Button";

	private const string BTN_IGNORE = "Panel/Footer/Ignore Button";

	private CrewDeathEntry.PlayerChoice _choice;

	private CrewDeathEntry _entry;

	private Action<CrewDeathEntry> _callback;

	public override UIReference UIReference => UIElements.PeepDeathPopup;

	public PeepDeathPopup(CrewDeathEntry entry, Action<CrewDeathEntry> callback)
		: base(hides: true, blurs: true)
	{
		_entry = entry;
		_callback = callback;
	}

	protected override void InitializeOnPush()
	{
		_go.SetButtonListener("Panel/Footer/Support Button", delegate
		{
			OnClick(CrewDeathEntry.PlayerChoice.Support);
		});
		_go.SetButtonListener("Panel/Footer/Help Button", delegate
		{
			OnClick(CrewDeathEntry.PlayerChoice.Quest);
		});
		_go.SetButtonListener("Panel/Footer/Ignore Button", delegate
		{
			OnClick(CrewDeathEntry.PlayerChoice.Ignore);
		});
		_go.GetChild("Panel/Footer/Support Button").SetChildText(Loc.Get("ui.fight.crewdeath.support", "cost", Loc.Price(_entry.perTurn)));
		_go.GetChild("Panel/Footer/Help Button").SetChildText(Loc.Get("ui.fight.crewdeath.quest"));
		_go.GetChild("Panel/Footer/Ignore Button").SetChildText(Loc.Get("ui.fight.crewdeath.ignore"));
		RefreshContents();
	}

	private void OnClick(CrewDeathEntry.PlayerChoice choice)
	{
		_choice = choice;
		Close();
	}

	protected override void ReleaseOnPop()
	{
		CrewDeathEntry entry = _entry.SetChoice(_choice);
		TimerUtil.RunNextFrame(delegate
		{
			_callback(entry);
		});
	}

	private void RefreshContents()
	{
		Entity person = _entry.peepId.FindEntity();
		_ = Game.serv.globals.settings.people.combatSettings.crewDeath;
		bool value = false;
		_go.SetActive("Panel/Footer/Support Button", value: true);
		_go.SetActive("Panel/Footer/Help Button", value);
		_go.SetActive("Panel/Footer/Ignore Button", value: true);
		_go.GetImage("Panel/Header/Deceased").sprite = HUDUtil.GetCrewSprite(person);
		_go.SetText("Panel/Header/Contents", Explain());
	}

	private string Explain()
	{
		string peepFullName = NameUtils.GetPeepFullName(_entry.peepId);
		return Loc.Get("ui.fight.crewdeath.header", "name", peepFullName);
	}
}

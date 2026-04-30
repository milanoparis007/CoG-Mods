using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim;
using Game.UI.Mouseovers;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Combat;

public class CombatPopupResults : BasePopup
{
	private class CombatCardMouseover : BaseCustomTextMouseover
	{
		public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

		protected override string ProduceText()
		{
			CombatCardContext componentInParent = context.GetComponentInParent<CombatCardContext>();
			string fullName = componentInParent.peep.data.person.FullName;
			string text = componentInParent.MakeAllWeaponDesc();
			return Loc.Get("ui.combat.card.mo", "name", fullName, "weapons", text);
		}
	}

	private const string BTN_CLOSE = "Panel/Close Button";

	private const string BTN_CANCEL = "Panel/Footer/Cancel Button";

	private const string CONTENTS_HUMAN = "Panel/Human List/Viewport/Content";

	private const string CONTENTS_ENEMY = "Panel/Other List/Viewport/Content";

	private const string TEMPLATE = "Templates/Combat Results Line Item";

	private NodeID _nodeId;

	private GameObject _tmplCard;

	private GameObject _containerHuman;

	private GameObject _containerEnemy;

	private Action _onClose;

	private List<Entity> _mypeeps = new List<Entity>();

	private List<Entity> _enemies = new List<Entity>();

	private List<CombatResults> _results = new List<CombatResults>();

	private const string CARD_PORTRAIT = "Portrait/Portrait";

	private const string CARD_NAME = "Name";

	private const string CARD_ATTACK = "Attack";

	private const string CARD_ATTACKBAR = "Attack Bar";

	private const string CARD_TARGET = "Target";

	private const string CARD_TARGETBAR = "Target Bar";

	private const string CARD_ICON_W = "Weapon";

	private const string CARD_ICON_H = "Health";

	public override UIReference UIReference => UIElements.CombatPopupResults;

	public CombatPopupResults(List<CombatResults> results, bool isAI, Action onClose)
	{
		_results = results;
		_onClose = onClose;
		foreach (CombatResults result in _results)
		{
			Entity item = (isAI ? result.target.peep : result.attacker.peep);
			Entity item2 = (isAI ? result.attacker.peep : result.target.peep);
			if (!_mypeeps.Contains(item))
			{
				_mypeeps.Add(item);
			}
			if (!_enemies.Contains(item2))
			{
				_enemies.Add(item2);
			}
		}
	}

	protected override void InitializeOnPush()
	{
		_go.SetButtonListener("Panel/Close Button", OnClose);
		_go.SetButtonListener("Panel/Footer/Cancel Button", OnClose);
		_containerHuman = _go.GetChild("Panel/Human List/Viewport/Content");
		_containerEnemy = _go.GetChild("Panel/Other List/Viewport/Content");
		_tmplCard = _go.GetChild("Templates/Combat Results Line Item");
		Game.serv.mouseovers.Register(MouseoverType.CombatCard, new CombatCardMouseover());
	}

	protected override void ReleaseOnPop()
	{
		Game.serv.mouseovers.Unregister(MouseoverType.CombatCard);
		_containerHuman.DestroyAllChildren();
		_containerEnemy.DestroyAllChildren();
		_go.ClearButtonListeners("Panel/Footer/Cancel Button");
		_go.ClearButtonListeners("Panel/Close Button");
		_enemies = null;
		_mypeeps = null;
		_onClose = null;
	}

	private void OnClose()
	{
		Action onClose = _onClose;
		Game.serv.ui.RemovePopup(this);
		onClose?.Invoke();
	}

	public override void OnActivated(bool pushed)
	{
		base.OnActivated(pushed);
		RefreshContents();
	}

	private void RefreshContents()
	{
		_ = Game.ctx.players.Human;
		_containerHuman.EnsureChildCount(_mypeeps, _tmplCard);
		_containerHuman.InitializeChildren(_mypeeps, MakeCard);
		_containerEnemy.EnsureChildCount(_enemies, _tmplCard);
		_containerEnemy.InitializeChildren(_enemies, MakeCard);
	}

	private CombatResults FindResultForAttacker(Entity peep)
	{
		return _results.FirstOrDefault((CombatResults r) => r.attacker.peep == peep);
	}

	private WeaponConfig FindWeaponForAttacker(Entity peep)
	{
		return FindResultForAttacker(peep)?.attacker.weapon;
	}

	private void MakeCard(int index, GameObject card, Entity peep)
	{
		CombatResults[] array = _results.Where((CombatResults r) => r.target.peep == peep || r.attacker.peep == peep).ToArray();
		string text = "";
		string text2 = "";
		CombatResults[] array2 = array;
		foreach (CombatResults combatResults in array2)
		{
			bool num2 = combatResults.target.peep == peep;
			CombatResults.Entry entry = (num2 ? combatResults.target : combatResults.attacker);
			CombatResults.Entry attacker = (num2 ? combatResults.attacker : combatResults.target);
			if (num2)
			{
				text2 = text2 + entry.Describe(attacker, details: false) + "\n";
			}
			else
			{
				text = text + entry.Describe(attacker, details: false) + "\n";
			}
		}
		CombatCardContext combatCardContext = card.GetOrAddComponent<CombatCardContext>().Set(peep);
		card.SetImage("Portrait/Portrait", HUDUtil.GetCrewSprite(combatCardContext.peep));
		card.SetText("Name", combatCardContext.MakeCrewNameDesc());
		card.SetText("Weapon", combatCardContext.MakeWeapon(FindWeaponForAttacker(peep)));
		card.SetText("Health", combatCardContext.MakeHealth());
		bool value = !string.IsNullOrEmpty(text);
		bool value2 = !string.IsNullOrEmpty(text2);
		card.SetText("Attack", text);
		card.SetActive("Attack", value);
		card.SetActive("Attack Bar", value);
		card.SetText("Target", text2);
		card.SetActive("Target", value2);
		card.SetActive("Target Bar", value2);
	}
}

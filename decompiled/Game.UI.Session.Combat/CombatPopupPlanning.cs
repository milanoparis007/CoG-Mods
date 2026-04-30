using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Sim;
using Game.UI.Mouseovers;
using SomaSim.Util;
using TMPro;
using UnityEngine;

namespace Game.UI.Session.Combat;

public class CombatPopupPlanning : BasePopup
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

	private class CombatMethodMouseover : BaseCustomTextMouseover
	{
		public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

		protected override string ProduceText()
		{
			CombatCardContext componentInParent = context.GetComponentInParent<CombatCardContext>();
			if (!(componentInParent.gameObject.GetDropdown("Method/Dropdown").GetCurrentOptionsItem()?.data is WeaponConfig weapon))
			{
				return Loc.Get("ui.combat.option.nomethod");
			}
			return componentInParent.MakeWeaponDesc(weapon, shortDesc: false);
		}
	}

	private class CombatTargetMouseover : BaseCustomTextMouseover
	{
		public override UIMouseoverName MouseoverAssetName => UIMouseoverName.TextMouseoverBoundedTL;

		protected override string ProduceText()
		{
			if (!(context.GetComponentInParent<CombatCardContext>().gameObject.GetDropdown("Target/Dropdown").GetCurrentOptionsItem()?.data is Entity peep))
			{
				return Loc.Get("ui.combat.option.notarget");
			}
			return CombatCardContext.MakeCrewNameDesc(peep);
		}
	}

	private const string BTN_CLOSE = "Panel/Close Button";

	private const string BTN_FIGHT = "Panel/Footer/Fight Button";

	private const string BTN_CANCEL = "Panel/Footer/Cancel Button";

	private const string CONTENTS_HUMAN = "Panel/Human List/Viewport/Content";

	private const string CONTENTS_ENEMY = "Panel/Other List/Viewport/Content";

	private const string TMPL_HUMAN = "Templates/Combat Popup Crew Card";

	private const string TMPL_ENEMY = "Templates/Combat Popup Enemy Card";

	private NodeID _nodeId;

	private GameObject _tmplCardHuman;

	private GameObject _tmplCardEnemy;

	private GameObject _containerHuman;

	private GameObject _containerEnemy;

	private List<Entity> _mycrew = new List<Entity>();

	private List<Entity> _enemies = new List<Entity>();

	private const string CARD_PORTRAIT = "Portrait/Portrait";

	private const string CARD_NAME = "Name";

	private const string CARD_ICON_W = "Weapon";

	private const string CARD_ICON_H = "Health";

	private const string BTN_UP = "Up";

	private const string BTN_DOWN = "Down";

	private const string TXT_POSITION = "Position";

	private const string TARGET_ROW = "Target";

	private const string TARGET_DOWN = "Target/Dropdown";

	private const string METHOD_ROW = "Method";

	private const string METHOD_DOWN = "Method/Dropdown";

	public override UIReference UIReference => UIElements.CombatPopupPlanning;

	public CombatPopupPlanning(List<Entity> mycrew, List<Entity> enemies)
	{
		_mycrew = mycrew;
		_enemies = enemies;
	}

	protected override void InitializeOnPush()
	{
		_go.SetButtonListener("Panel/Close Button", OnCancel);
		_go.SetButtonListener("Panel/Footer/Cancel Button", OnCancel);
		_go.SetButtonListener("Panel/Footer/Fight Button", OnFight);
		_containerHuman = _go.GetChild("Panel/Human List/Viewport/Content");
		_containerEnemy = _go.GetChild("Panel/Other List/Viewport/Content");
		_tmplCardHuman = _go.GetChild("Templates/Combat Popup Crew Card");
		_tmplCardEnemy = _go.GetChild("Templates/Combat Popup Enemy Card");
		Game.serv.mouseovers.Register(MouseoverType.CombatCard, new CombatCardMouseover());
		Game.serv.mouseovers.Register(MouseoverType.CombatMethodButton, new CombatMethodMouseover());
		Game.serv.mouseovers.Register(MouseoverType.CombatTargetButton, new CombatTargetMouseover());
	}

	protected override void ReleaseOnPop()
	{
		Game.serv.mouseovers.Unregister(MouseoverType.CombatCard);
		Game.serv.mouseovers.Unregister(MouseoverType.CombatMethodButton);
		Game.serv.mouseovers.Unregister(MouseoverType.CombatTargetButton);
		_containerHuman.DestroyAllChildren();
		_containerEnemy.DestroyAllChildren();
		_go.ClearButtonListeners("Panel/Footer/Cancel Button");
		_go.ClearButtonListeners("Panel/Footer/Fight Button");
		_enemies = null;
		_mycrew = null;
	}

	private void OnCancel()
	{
		Close();
	}

	private void OnFight()
	{
		List<CombatResults> results = new List<CombatResults>();
		foreach (Entity item in _mycrew)
		{
			CombatResults combatResults = DoCombat(item);
			if (combatResults != null)
			{
				results.Add(combatResults);
			}
		}
		RefreshContents();
		Game.serv.ui.RemovePopup(this);
		Game.ctx.selection.ClearActive();
		TimerUtil.RunNextFrame(delegate
		{
			Game.ctx.simman.combat.ShowCombatResults(results, immediate: true, isAttackerAI: false);
		});
	}

	private CombatResults DoCombat(Entity peep)
	{
		Entity entity = FindTargetFor(peep);
		if (entity == null)
		{
			return null;
		}
		if (!entity.components.agent.HasHealthPointsLeft)
		{
			return null;
		}
		WeaponConfig weaponConfig = FindWeaponFor(peep);
		if (weaponConfig == null)
		{
			return null;
		}
		return Game.ctx.simman.combat.PerformHumanCombat(peep, entity, weaponConfig);
	}

	public override void OnActivated(bool pushed)
	{
		base.OnActivated(pushed);
		RefreshContents();
	}

	private void RefreshContents()
	{
		_ = Game.ctx.players.Human;
		_containerHuman.EnsureChildCount(_mycrew, _tmplCardHuman);
		_containerHuman.InitializeChildren(_mycrew, MakeCard);
		_containerEnemy.EnsureChildCount(_enemies, _tmplCardEnemy);
		_containerEnemy.InitializeChildren(_enemies, MakeCard);
		RefreshFightButton();
	}

	private GameObject FindCardForHumanCrew(Entity peep)
	{
		GameObject card = null;
		_containerHuman.transform.WalkChildren(delegate(Transform tr)
		{
			CombatCardContext component = tr.gameObject.GetComponent<CombatCardContext>();
			if (component != null && component.peep == peep)
			{
				card = tr.gameObject;
			}
		}, recursive: false);
		return card;
	}

	private Entity FindTargetFor(Entity peep)
	{
		GameObject gameObject = FindCardForHumanCrew(peep);
		TMP_Dropdown tMP_Dropdown = ((gameObject == null) ? null : gameObject.GetDropdown("Target/Dropdown"));
		if (!(tMP_Dropdown != null))
		{
			return null;
		}
		return tMP_Dropdown.GetCurrentOptionsItem()?.data as Entity;
	}

	private WeaponConfig FindWeaponFor(Entity peep)
	{
		GameObject gameObject = FindCardForHumanCrew(peep);
		TMP_Dropdown tMP_Dropdown = ((gameObject == null) ? null : gameObject.GetDropdown("Method/Dropdown"));
		if (!(tMP_Dropdown != null))
		{
			return null;
		}
		return tMP_Dropdown.GetCurrentOptionsItem()?.data as WeaponConfig;
	}

	private void RefreshFightButton()
	{
		bool interactable = false;
		foreach (Entity item in _mycrew)
		{
			if (FindTargetFor(item) != null)
			{
				interactable = true;
				break;
			}
		}
		_go.GetButton("Panel/Footer/Fight Button").interactable = interactable;
	}

	private void MakeCard(int index, GameObject card, Entity peep)
	{
		CombatCardContext ctx = card.GetOrAddComponent<CombatCardContext>().Set(peep);
		RefreshTargetRow(card, ctx);
		RefreshMethodRow(card, ctx);
		RefreshNameAndIcon(card, ctx);
	}

	private void RefreshNameAndIcon(GameObject card, CombatCardContext ctx)
	{
		WeaponConfig weapon = FindWeaponFor(ctx.peep);
		card.SetImage("Portrait/Portrait", HUDUtil.GetCrewSprite(ctx.peep));
		card.SetText("Name", ctx.MakeCrewNameDesc());
		card.SetText("Weapon", ctx.MakeWeapon(weapon));
		card.SetText("Health", ctx.MakeHealth());
	}

	private bool CanAttack(Entity peep)
	{
		if (Game.ctx.simman.combat.CanPayAttackCost(peep))
		{
			return CombatCardContext.GetCrewHealthInfo(peep).category.canwork;
		}
		return false;
	}

	private void RefreshTargetRow(GameObject card, CombatCardContext ctx)
	{
		if (!ctx.pid.IsHumanPlayer || ctx.peep == null)
		{
			return;
		}
		TMP_Dropdown dd = card.GetDropdown("Target/Dropdown");
		dd.onValueChanged.RemoveAllListeners();
		dd.options.Clear();
		if (CanAttack(ctx.peep))
		{
			foreach (Entity enemy in _enemies)
			{
				string fullName = enemy.data.person.FullName;
				dd.AddOptionsItem(fullName, enemy);
			}
			dd.AddOptionsItem(Loc.Get("ui.combat.option.nobody"));
		}
		else
		{
			dd.AddOptionsItem(Loc.Get("ui.combat.option.nopoints"));
		}
		dd.onValueChanged.SetListener(delegate(int index)
		{
			OnTargetRowSelection(dd, card, index);
		});
		OnTargetRowSelection(dd, card, dd.value);
		dd.interactable = dd.options.Count > 1;
		RefreshUpDownButtons(card);
	}

	private void RefreshAllUpDownButtons()
	{
		foreach (Transform item in _containerHuman.transform)
		{
			RefreshUpDownButtons(item.gameObject);
		}
	}

	private void RefreshUpDownButtons(GameObject card)
	{
		CombatCardContext ctx = card.GetComponent<CombatCardContext>();
		int siblingIndex = ctx.transform.GetSiblingIndex();
		int childCount = _containerHuman.transform.childCount;
		card.GetButton("Up").interactable = siblingIndex > 0;
		card.GetButton("Down").interactable = siblingIndex < childCount - 1;
		card.SetButtonListener("Up", delegate
		{
			OnArrow(ctx, -1);
		});
		card.SetButtonListener("Down", delegate
		{
			OnArrow(ctx, 1);
		});
		card.SetText("Position", Loc.FormatNumber(siblingIndex + 1));
	}

	private void OnArrow(CombatCardContext ctx, int delta)
	{
		int siblingIndex = ctx.transform.GetSiblingIndex();
		int num = siblingIndex + delta;
		if (num >= 0 && num <= ctx.transform.childCount - 1)
		{
			ctx.transform.SetSiblingIndex(num);
			_mycrew.Swap(siblingIndex, num);
			RefreshAllUpDownButtons();
		}
	}

	private void OnTargetRowSelection(TMP_Dropdown dd, GameObject card, int index)
	{
		bool interactable = dd.GetOptionsItem(index)?.data != null;
		card.GetDropdown("Method/Dropdown").interactable = interactable;
		Game.serv.mouseovers.Refresh(MouseoverType.CombatTargetButton);
		RefreshFightButton();
	}

	private void RefreshMethodRow(GameObject card, CombatCardContext ctx)
	{
		if (!ctx.pid.IsHumanPlayer || ctx.peep == null)
		{
			return;
		}
		TMP_Dropdown dropdown = card.GetDropdown("Method/Dropdown");
		dropdown.onValueChanged.RemoveAllListeners();
		dropdown.options.Clear();
		foreach (WeaponConfig allweapon in ctx.allweapons)
		{
			Resource resource = allweapon.FindResource();
			dropdown.AddOptionsItem(resource.GetIconAndName(), allweapon);
		}
		dropdown.onValueChanged.SetListener(delegate
		{
			OnMethodRowSelection(card, ctx);
		});
	}

	private void OnMethodRowSelection(GameObject card, CombatCardContext ctx)
	{
		RefreshNameAndIcon(card, ctx);
		Game.serv.mouseovers.Refresh(MouseoverType.CombatMethodButton);
	}
}

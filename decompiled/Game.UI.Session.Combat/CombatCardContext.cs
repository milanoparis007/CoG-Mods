using System.Collections.Generic;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using Game.UI.Util;
using SomaSim.Util;
using UnityEngine;

namespace Game.UI.Session.Combat;

public class CombatCardContext : MonoBehaviour
{
	public PlayerID pid = PlayerID.INVALID;

	public Entity peep;

	public Entity vehicle;

	public WeaponConfig bestWeapon;

	public List<WeaponConfig> allweapons = new List<WeaponConfig>();

	public void Reset()
	{
		pid = PlayerID.INVALID;
		peep = (vehicle = null);
		bestWeapon = null;
		allweapons = new List<WeaponConfig>();
	}

	public CombatCardContext Set(Entity peep)
	{
		this.peep = peep;
		pid = peep.data.agent.pid;
		vehicle = pid.FindPlayer().crew.FindVehicleAssignedToPeep(peep.Id).FindEntity();
		allweapons = Game.ctx.simman.combat.FindAllWeaponsSorted(vehicle);
		bestWeapon = Game.ctx.simman.combat.FindBestWeapon(vehicle);
		return this;
	}

	public static (Fixnum maxHealth, CombatSettings.HealthInfo category) GetCrewHealthInfo(Entity peep)
	{
		PlayerID playerID = peep.data.agent.pid;
		Fixnum health = peep.data.agent.health;
		CombatSettings combatSettings = Game.serv.globals.settings.people.combatSettings;
		Fixnum item = combatSettings.maxCrewHealth.Evaluate(playerID, peep, peep);
		CombatSettings.HealthInfo item2 = combatSettings.FindCrewHealthInfo(health);
		return (maxHealth: item, category: item2);
	}

	public static string MakeCrewNameDesc(Entity peep)
	{
		PlayerInfo playerInfo = peep.data.agent.pid.FindPlayer();
		StringBuilder stringBuilder = StringBuilderPool.Instance.Allocate();
		stringBuilder.AppendLine(peep.data.person.FullName);
		stringBuilder.AppendLine(playerInfo.social.FindPlayerGroupNameColorized());
		Fixnum health = peep.data.agent.health;
		var (fixnum, healthInfo) = GetCrewHealthInfo(peep);
		if (health < fixnum)
		{
			Color32 color = healthInfo.color.SetAlpha(0.5f);
			string text = TextUtil.ColorWrap(Loc.Get(healthInfo.locname), color);
			stringBuilder.AppendLine(Loc.Get("ui.combat.health", "name", text));
		}
		return stringBuilder.ToStringAndReturnToPool();
	}

	public string MakeCrewNameDesc()
	{
		return MakeCrewNameDesc(peep);
	}

	public string MakeWeapon(WeaponConfig weapon = null)
	{
		if (weapon == null)
		{
			weapon = bestWeapon;
		}
		string icon = weapon.FindResource().GetIcon();
		string text = weapon.DescribeDamageRange(peep);
		return Loc.Get("ui.combat.icons.weapon", "icon", icon, "value", text);
	}

	public string MakeHealth()
	{
		string text = Loc.FormatNumber(peep.components.agent.CurrentHealth);
		return Loc.Get("ui.combat.icons.health", "value", text);
	}

	public string MakeAllWeaponDesc()
	{
		return allweapons.SelectToString((WeaponConfig weapon) => MakeWeaponDesc(weapon, shortDesc: true), "\n");
	}

	public string MakeWeaponDesc(WeaponConfig weapon, bool shortDesc)
	{
		return weapon.Describe(peep, shortDesc);
	}
}

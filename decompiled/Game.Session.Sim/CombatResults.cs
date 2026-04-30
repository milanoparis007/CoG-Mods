using System.Text;
using Game.Core;
using Game.Services;
using Game.Session.Entities;
using Game.Session.Player;
using SomaSim.Util;

namespace Game.Session.Sim;

public sealed class CombatResults
{
	public struct Entry
	{
		public Entity peep;

		public bool wasHurt;

		public WeaponConfig weapon;

		public Fixnum damage;

		public Fixnum finalHealth;

		public Fixnum defended;

		public bool IsDead => finalHealth <= 0;

		public bool IsHumanCrew => peep.data.agent.pid.IsHumanPlayer;

		public Entry(Entity peep, WeaponConfig weapon, Fixnum damage, bool wasHurt, Fixnum defended)
		{
			this.peep = peep;
			this.weapon = weapon;
			this.wasHurt = wasHurt;
			this.damage = damage;
			finalHealth = peep.data.agent.health;
			this.defended = defended;
		}

		public Node GetNode()
		{
			return peep.components.agent.GetNode();
		}

		public string GetName()
		{
			return peep.data.person.ShortName;
		}

		public string GetGroupNameColor()
		{
			return peep.data.agent.pid.FindPlayer().social.FindPlayerGroupNameColorized();
		}

		public string GetGroupNamePlain()
		{
			return peep.data.agent.pid.FindPlayer().social.PlayerGroupName;
		}

		public CombatSettings.HealthInfo FindHealthType()
		{
			return Game.serv.globals.settings.people.combatSettings.FindCrewHealthInfo(finalHealth);
		}

		public string Describe(Entry attacker, bool details)
		{
			string text = Loc.FormatNumberPlusMinus(-1 * damage);
			string icon = attacker.weapon.FindResource().GetIcon();
			string text2 = Loc.Get(details ? "ui.combat.attacked-long" : "ui.combat.attacked", "icon", icon, "points", text, "name", attacker.GetName(), "victim", GetName());
			if (!details && defended != 0)
			{
				text2 = text2 + "\n" + Loc.Get("ui.combat.defended", "value", Loc.FormatNumber(defended));
			}
			return text2;
		}
	}

	public Entry attacker;

	public Entry target;

	public NodeID nodeId;

	public string Describe(bool addQuote)
	{
		StringBuilder sb = StringBuilderPool.AllocateInstance();
		if (addQuote)
		{
			string text = Loc.Get("ui.fight.quotewrap", "quote", Loc.Get("ui.fight.quote"));
			sb.AppendLine(text, 2);
		}
		sb.AppendLine(Loc.Get("ui.fight", "aname", attacker.GetName(), "agroup", attacker.GetGroupNameColor(), "tname", target.GetName(), "tgroup", target.GetGroupNameColor()), 2);
		sb.AppendLine(target.Describe(attacker, details: true), 2);
		sb.AppendLine(attacker.Describe(target, details: true), 2);
		return sb.ToStringAndReturnToPool().Trim();
	}
}

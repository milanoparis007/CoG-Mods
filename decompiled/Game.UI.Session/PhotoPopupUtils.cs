using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core;
using Game.Services;
using Game.Session.Board;
using Game.Session.Entities;
using Game.Session.Sim;
using Game.UI.Session.Combat;
using SomaSim.Util;

namespace Game.UI.Session;

public static class PhotoPopupUtils
{
	public static CombatSummary GenerateCombatSummary(List<CombatResults> results)
	{
		string summary = GenerateOutputString(results);
		List<PhotoConfig> photos = new List<PhotoConfig>
		{
			PickAttackPhoto(results),
			PickResultPhoto(results)
		};
		bool flag = results.Any((CombatResults r) => r.target.IsDead || r.attacker.IsDead);
		CrewDeathEntry humanCrewDied = CrewDeathEntry.EMPTY;
		NodeID nodeID = NodeID.INVALID;
		string cornerName = null;
		string text = null;
		foreach (CombatResults result in results)
		{
			text = text ?? result.target.GetGroupNamePlain();
			flag = flag || result.target.IsDead || result.attacker.IsDead;
			if (nodeID.IsNotValid && result.nodeId.IsValid)
			{
				nodeID = result.nodeId;
				cornerName = nodeID.FindNode().GetCornerNameShort();
			}
			Entity entity = ((result.target.IsDead && result.target.IsHumanCrew) ? result.target.peep : ((result.attacker.IsDead && result.attacker.IsHumanCrew) ? result.attacker.peep : null));
			if (entity != null)
			{
				humanCrewDied = MakeCrewDeathEntry(result, entity);
			}
		}
		return new CombatSummary
		{
			summary = summary,
			photos = photos,
			anyoneDied = flag,
			humanCrewDied = humanCrewDied,
			groupName = text,
			cornerName = cornerName,
			corner = nodeID
		};
	}

	private static CrewDeathEntry MakeCrewDeathEntry(CombatResults _, Entity deadHumanCrew)
	{
		Price item = Game.ctx.players.Human.finances.GetCrewSalary(deadHumanCrew.Id, isDead: true, explain: false).salary;
		return new CrewDeathEntry
		{
			peepId = deadHumanCrew.Id,
			perTurn = item,
			choice = CrewDeathEntry.PlayerChoice.Ignore
		};
	}

	public static void ShowCombatSummary(CombatSummary summary, List<CombatResults> results, bool isAttackerAI)
	{
		Game.serv.ui.AddPopup(new PhotoPopup(summary.photos, onPhotosClosed));
		Game.ctx.sfx.PlayCombatHappened();
		void onOkClosed()
		{
			if (summary.anyoneDied)
			{
				PhotoConfig value = summary.photos.LastOrDefaultFast();
				string header = Loc.Get("newspaper-headline.murder", "cornername", summary.cornerName, "groupname", summary.groupName);
				Game.serv.ui.AddPopup(new NewspaperPopup(header, value));
			}
		}
		void onPhotosClosed()
		{
			Game.serv.ui.AddPopup(new CombatPopupResults(results, isAttackerAI, onOkClosed));
		}
	}

	private static string GenerateOutputString(List<CombatResults> results)
	{
		if (results.Count == 0)
		{
			return Loc.Get("ui.combat.photo-popup.unsuccessful");
		}
		StringBuilder sb = StringBuilderPool.AllocateInstance();
		sb.AppendLine(Loc.Get("ui.combat.photo-popup.attack"), 2);
		foreach (CombatResults result in results)
		{
			sb.AppendLine(result.Describe(addQuote: false), 1);
		}
		return sb.ToStringAndReturnToPool();
	}

	private static PhotoConfig PickAttackPhoto(List<CombatResults> results)
	{
		UIPhotosSettings.CombatPhotos combat = Game.serv.globals.ui.photos.combat;
		if (results.Any((CombatResults r) => r.attacker.weapon.firearm))
		{
			return combat.attackGun;
		}
		if (results.Any((CombatResults r) => r.attacker.weapon.tool))
		{
			return combat.attackWeapon;
		}
		return combat.attackOther;
	}

	private static PhotoConfig PickResultPhoto(List<CombatResults> results)
	{
		UIPhotosSettings.CombatPhotos combat = Game.serv.globals.ui.photos.combat;
		CombatSettings combatSettings = Game.serv.globals.settings.people.combatSettings;
		foreach (CombatResults result in results)
		{
			if (result.target.finalHealth.IsZero)
			{
				return combat.resultDeath;
			}
			if (result.attacker.finalHealth.IsZero)
			{
				return combat.resultDeath;
			}
		}
		foreach (CombatResults result2 in results)
		{
			CombatSettings.HealthInfo healthInfo = combatSettings.FindCrewHealthInfo(result2.target.finalHealth);
			CombatSettings.HealthInfo healthInfo2 = combatSettings.FindCrewHealthInfo(result2.attacker.finalHealth);
			if (!healthInfo.canwork || !healthInfo2.canwork)
			{
				return combat.resultInjury;
			}
		}
		return combat.resultOther;
	}
}

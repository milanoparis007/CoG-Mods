using System.Collections.Generic;
using System.Reflection;
using Game;
using Game.Core;
using Game.Session.Data;
using Game.Session.Entities;
using Game.Session.Player;
using Game.Session.Sim;
using HarmonyLib;
using SomaSim.Util;

namespace GameplayTweaks
{
	/// <summary>
	/// Guards PoliticsManager.RunNewGamePoliticianSetup so we never call PickElement on an empty
	/// localPoliticians list (e.g. when mods reduce the NPC pool). Wards with no candidates are skipped.
	/// </summary>
	internal static class PoliticsManagerPatches
	{
		private static readonly FieldInfo PdataField = AccessTools.Field(typeof(PoliticsManager), "_pdata");

		public static void ApplyPatches(Harmony harmony)
		{
			harmony.Patch(
				AccessTools.Method(typeof(PoliticsManager), "RunNewGamePoliticianSetup"),
				prefix: new HarmonyMethod(typeof(PoliticsManagerPatches), nameof(RunNewGamePoliticianSetup_Prefix)));
		}

		private static bool RunNewGamePoliticianSetup_Prefix(PoliticsManager __instance)
		{
			GameplayTweaksPlugin.ResetRuntimeCampaignStateForNewGame("RunNewGamePoliticianSetup");
			var pdata = (PoliticsManagerPersistedData)PdataField.GetValue(__instance);
			if (pdata == null)
				return true; // let original run

			foreach (PlayerInfo item in Game.Game.ctx.players.all)
			{
				if (item.PID.IsHumanPlayer || item.IsJustGang)
					pdata.influence[item.PID] = new InfluenceWallet();
			}

			foreach (Ward ward in pdata.wards)
			{
				PrecinctID id = ward.id;
				ward.GenerateLocalPoliticians();
				List<EntityID> localPoliticians = ward.localPoliticians;
				if (localPoliticians == null || localPoliticians.Count == 0)
					continue;
				EntityID politician = pdata.rng.PickElement(localPoliticians);
				__instance.ElectPoliticianToWard(id, politician);
			}

			return false; // skip original
		}
	}
}

using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Session.Sim;
using HarmonyLib;

namespace AfterProhibitionEconomy
{
	internal static class PostBusinessUpdateDiagnosticsPatch
	{
		private static readonly HashSet<string> LoggedSources = new HashSet<string>(StringComparer.Ordinal);
		private static bool _loggedDisabled;

		internal static void ApplyPatch(Harmony harmony)
		{
			try
			{
				MethodInfo updateBusinessModulesMethod = AccessTools.Method(typeof(BusinessUpdate), "UpdateBusinessModules");
				if (updateBusinessModulesMethod != null)
				{
					HarmonyMethod postfix = new HarmonyMethod(typeof(PostBusinessUpdateDiagnosticsPatch), nameof(UpdateBusinessModulesPostfix))
					{
						priority = Priority.Last
					};
					harmony.Patch(
						updateBusinessModulesMethod,
						postfix: postfix);
					AfterProhibitionEconomyPlugin.Log?.LogInfo("post business update diagnostics patch applied target=BusinessUpdate.UpdateBusinessModules");
				}
				else
				{
					AfterProhibitionEconomyPlugin.Log?.LogWarning("post business update diagnostics patch skipped target=BusinessUpdate.UpdateBusinessModules reason=method-not-found");
				}
			}
			catch (Exception ex)
			{
				AfterProhibitionEconomyPlugin.Log?.LogWarning("post business update diagnostics patch setup failed: " + ex.Message);
			}
		}

		private static void UpdateBusinessModulesPostfix(bool initial)
		{
			try
			{
				if (!(AfterProhibitionEconomyPlugin.EnablePostBusinessUpdateDiagnostics?.Value ?? false))
				{
					if (!_loggedDisabled)
					{
						_loggedDisabled = true;
						AfterProhibitionEconomyPlugin.Log?.LogInfo("post-business-update-diagnostics disabled reason=startup-safety");
					}

					return;
				}

				if (global::Game.Game.ctx?.clock == null)
				{
					return;
				}

				string source = initial ? "business-update-initial" : "business-update";
				string key = source + "|day=" + global::Game.Game.ctx.clock.Now.days;
				if (!LoggedSources.Add(key))
				{
					return;
				}

				bool dirtyCashEnabled = AfterProhibitionEconomyPlugin.EnableDirtyCashRoutingSampleLog?.Value == true;
				bool frontResourceEnabled = AfterProhibitionEconomyPlugin.EnableFrontResourceAudit?.Value == true;
				AfterProhibitionEconomyPlugin.Log?.LogInfo(
					"post-business-update-diagnostics source=" + source +
					" dirtyCash=" + dirtyCashEnabled +
					" frontResource=" + frontResourceEnabled);

				if (dirtyCashEnabled)
				{
					DirtyCashRoutingClassifier.LogStartupSamples(
						source,
						AfterProhibitionEconomyPlugin.Log,
						Math.Max(0, AfterProhibitionEconomyPlugin.DirtyCashRoutingSampleLimit.Value));
				}

				if (frontResourceEnabled)
				{
					FrontResourceAudit.LogStartupAudit(
						source,
						AfterProhibitionEconomyPlugin.Log,
						Math.Max(0, AfterProhibitionEconomyPlugin.FrontResourceAuditSampleLimit.Value));
				}
			}
			catch (Exception ex)
			{
				AfterProhibitionEconomyPlugin.Log?.LogWarning("post business update diagnostics failed error=" + ex.GetType().Name + ":" + ex.Message);
			}
		}
	}
}

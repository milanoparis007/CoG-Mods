using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Data;
using Game.Session.Entities;
using SomaSim.Util;

namespace Game.Session.Sim.Modules;

public sealed class RecipeMods
{
	public struct Result
	{
		public ResourceAndQty resAndQty;

		public string explanation;

		public bool HasExplanation => !string.IsNullOrWhiteSpace(explanation);

		public Result(ResourceAndQty rq, string exp)
		{
			this = default(Result);
			resAndQty = rq;
			explanation = exp;
		}
	}

	public ModifierList produceqty;

	public ModifierList consumeqty;

	internal static Result ModQty(ResourceAndQty val, ModuleQuery mq, ModifierList list, IModule module, ModuleExpansionTarget evalTarget, bool explain)
	{
		Result result = new Result(val, null);
		ModQuery modQuery = mq.MakeManagerModQuery();
		if (list != null && list.Count > 0)
		{
			ResourceAndQty rq = new ResourceAndQty(val.id, list.Evaluate(modQuery, val.qty));
			string exp = (explain ? list.Explain(modQuery, val.qty) : null);
			result = new Result(rq, exp);
		}
		List<Label> list2 = module?.ModuleData?.Expansions;
		if (list2 != null && list2.Count > 0)
		{
			ModuleExpansions defs = module.FindExpansionDefs();
			foreach (Label item in list2)
			{
				result = ApplyExpansion(defs, evalTarget, item, modQuery, result, explain);
			}
		}
		Entity manager = mq.manager;
		if (manager != null && manager.components.agent.HasAnyLevelups)
		{
			List<ModuleLevelupConfig> list3 = module?.FindLevelupDefs();
			if (list3 != null)
			{
				result = ApplyLevelups(list3, evalTarget, mq, result, explain);
			}
		}
		return result;
	}

	private static Result ApplyExpansion(ModuleExpansions defs, ModuleExpansionTarget evalTarget, Label exp, ModQuery q, Result result, bool explain)
	{
		ModuleExpansionConfig moduleExpansionConfig = defs.FindExpansionOrNull(exp);
		if (moduleExpansionConfig == null)
		{
			return result;
		}
		if (moduleExpansionConfig.target != evalTarget)
		{
			return result;
		}
		Fixnum fixnum = moduleExpansionConfig.multiplier.Evaluate(q);
		ResourceAndQty resAndQty = result.resAndQty.SetQuantity(result.resAndQty.qty * fixnum);
		string text = result.explanation;
		if (explain)
		{
			string text2 = Loc.PercentagePlusMinus(fixnum - 1);
			string text3 = Loc.Get("ui.viewdescribemodule.expansion-line", "name", moduleExpansionConfig.display.GetLocName(), "percent", text2);
			text += text3;
		}
		return new Result
		{
			resAndQty = resAndQty,
			explanation = text
		};
	}

	private static Result ApplyLevelups(List<ModuleLevelupConfig> defs, ModuleExpansionTarget evalTarget, ModuleQuery q, Result result, bool explain)
	{
		AgentData agent = q.manager.data.agent;
		Fixnum fixnum = result.resAndQty.qty;
		string text = result.explanation;
		foreach (ModuleLevelupConfig def in defs)
		{
			if (def.target != evalTarget)
			{
				continue;
			}
			int levelupLevel = agent.xp.GetLevelupLevel(def.id);
			if (ValueUtil.TestCurrentValue(levelupLevel, def.@is, def.value))
			{
				fixnum = (fixnum + def.delta) * def.multiplier;
				if (explain)
				{
					string text2 = Loc.FormatNumberPlusMinus(fixnum - result.resAndQty.qty);
					LevelupChain levelup = Game.serv.globals.settings.skills.experience.GetLevelup(def.id);
					string text3 = Loc.Get("ui.viewdescribemodule.levelup-line", "delta", text2, "name", levelup.Describe(levelupLevel));
					text += text3;
				}
			}
		}
		ResourceAndQty resAndQty = result.resAndQty.SetQuantity(fixnum);
		return new Result
		{
			resAndQty = resAndQty,
			explanation = text
		};
	}
}

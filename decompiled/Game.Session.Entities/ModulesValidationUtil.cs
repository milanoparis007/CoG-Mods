using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Entities;

public static class ModulesValidationUtil
{
	public static void Validate(EntityConfig config)
	{
		if (!config.ident.parent.IsNotSet)
		{
			if (config.defmodule.def != null)
			{
				Validate(config.defmodule.def, config);
			}
			if (config.defmodule.expansions != null)
			{
				Validate(config.Template, config.defmodule.def, config.defmodule.expansions);
			}
			if (config.defmodule.levelups != null)
			{
				Validate(config.Template, config.defmodule.def, config.defmodule.levelups);
			}
		}
	}

	private static void Validate(Label id, IModuleConfig def, List<ModuleLevelupConfig> levelups)
	{
		_ = Game.serv.globals.settings.skills.experience;
		foreach (ModuleLevelupConfig levelup in levelups)
		{
			ValidateTargetForModule(id, def, levelup.target);
		}
	}

	private static void Validate(Label id, IModuleConfig def, ModuleExpansions expansions)
	{
		foreach (ModuleExpansionConfig config in expansions.configs)
		{
			ValidateTargetForModule(id, def, config.target);
		}
	}

	private static void ValidateTargetForModule(Label id, IModuleConfig def, ModuleExpansionTarget target)
	{
		if (def != null)
		{
			_ = def is ManufactureModuleConfig;
		}
		if (def != null)
		{
			_ = def is ConsumerModuleConfig;
		}
	}

	private static void Validate(IModuleConfig cfg, EntityConfig template)
	{
		_ = Game.serv.globals.settings.tags.allTags;
		TagList tags = cfg.Common.tags;
		if (tags != null)
		{
			foreach (Label item in tags)
			{
				_ = item;
			}
		}
		TagList skills = cfg.Common.skills;
		if (skills != null)
		{
			foreach (Label item2 in skills)
			{
				_ = item2;
			}
		}
		if (cfg is ManufactureModuleConfig manufactureModuleConfig)
		{
			foreach (Recipe recipe in manufactureModuleConfig.recipes)
			{
				Validate(recipe.refill, expected: true);
				Validate(recipe.consume, expected: false);
				Validate(recipe.produce, expected: true);
				Validate(recipe.selloff, expected: false);
			}
		}
		ConsumerModuleConfig consumerModuleConfig = cfg as ConsumerModuleConfig;
		if (consumerModuleConfig != null && consumerModuleConfig.sink == null)
		{
			_ = template.ident.IsChild;
		}
		if (consumerModuleConfig != null && consumerModuleConfig.sink != null)
		{
			ConsumerRecipe sink = consumerModuleConfig.sink;
			Validate(sink.AllRefill, expected: true);
			Validate(sink.AllConsume, expected: false);
		}
		ModulePurchaseCost modulePurchaseCost = cfg.Common?.purchase;
		if (modulePurchaseCost == null || modulePurchaseCost.consume == null)
		{
			return;
		}
		foreach (ResourceAndQty item3 in modulePurchaseCost.consume)
		{
			_ = item3;
		}
	}

	private static void CheckSign(object element, Label id, Fixnum value, bool expected)
	{
		if (!(expected ? (value >= 0) : (value <= 0)))
		{
			string text = (expected ? "positive or zero" : "negative or zero");
			Logger.Warning($"Module problem in {element}: {id} {value} should be {text}");
		}
	}

	private static void Validate(List<RefillElement> list, bool expected)
	{
		if (list == null)
		{
			return;
		}
		foreach (RefillElement item in list)
		{
			CheckSign(item, item.id, item.get, expected);
		}
	}

	private static void Validate(List<ResourceAndQty> list, bool expected)
	{
		if (list == null)
		{
			return;
		}
		foreach (ResourceAndQty item in list)
		{
			CheckSign(item, item.id, item.qty, expected);
		}
	}

	private static void Validate(List<SellOffElement> list, bool expected)
	{
		if (list == null)
		{
			return;
		}
		foreach (SellOffElement item in list)
		{
			CheckSign(item, item.id, item.sell, expected);
		}
	}
}

using System;
using System.Collections.Generic;
using Game.Session.Sim.Modules;

namespace Game.Session.Entities;

public sealed class DefModuleConfig : BaseConfig
{
	public IModuleConfig def;

	public ModuleExpansions expansions;

	public List<ModuleLevelupConfig> levelups;

	public override List<Type> RequiresConfigs => null;

	public override BaseComponent CreateComponent(EntityComponents ec)
	{
		return ec.defmodule = new DefModuleComponent();
	}

	public override BaseData MoveOrCreateData(EntityData target, EntityData source)
	{
		return null;
	}

	public override void VerifyAfterLoading(EntityConfig config)
	{
		base.VerifyAfterLoading(config);
		ModulesValidationUtil.Validate(config);
	}
}

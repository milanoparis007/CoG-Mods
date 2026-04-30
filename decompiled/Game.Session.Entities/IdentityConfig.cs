using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Session.Entities;

public sealed class IdentityConfig : BaseConfig
{
	public Label template;

	public Label parent;

	public TagList tags;

	public string modid;

	public override List<Type> RequiresConfigs => null;

	public bool IsChild
	{
		get
		{
			if (parent.IsSet)
			{
				return !template.String.EndsWith("-base");
			}
			return false;
		}
	}

	public bool IsModTemplate => modid != null;

	public override BaseComponent CreateComponent(EntityComponents ec)
	{
		return ec.ident = new IdentityComponent();
	}

	public override BaseData MoveOrCreateData(EntityData target, EntityData source)
	{
		IdentityData obj = source?.ident ?? new IdentityData();
		IdentityData result = obj;
		target.ident = obj;
		return result;
	}
}

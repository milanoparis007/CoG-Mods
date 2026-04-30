using System;
using System.Collections.Generic;

namespace Game.Session.Entities;

public sealed class SelectionConfig : BaseConfig
{
	public enum OnFocus
	{
		None,
		Avatar,
		ShowInfo
	}

	public enum OnAction
	{
		None,
		Avatar,
		Building,
		Car,
		CarAmbient,
		Corner
	}

	public OnFocus onfocus;

	public OnAction onaction;

	public override List<Type> RequiresConfigs => null;

	public override BaseComponent CreateComponent(EntityComponents ec)
	{
		return ec.selection = new SelectionComponent();
	}

	public override BaseData MoveOrCreateData(EntityData target, EntityData source)
	{
		return null;
	}
}

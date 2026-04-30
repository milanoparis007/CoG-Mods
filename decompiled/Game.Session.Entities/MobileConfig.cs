using System;
using System.Collections.Generic;
using Game.Services;
using SomaSim.Util;

namespace Game.Session.Entities;

public class MobileConfig : BaseConfig
{
	public AvatarType type;

	public string locname;

	public string locdesc;

	public string uiSprite;

	public float velocityTilesPerSecond = 1f;

	public float navOffsetTiles;

	public Fixnum basePurchasePrice;

	public bool usesGain;

	public float movementGain;

	public float movementMax;

	public float rotationGain;

	public float rotationMax;

	public override List<Type> RequiresConfigs => new List<Type> { typeof(ModelConfig) };

	public bool ShowPick
	{
		get
		{
			if (type != AvatarType.Car)
			{
				return type == AvatarType.Truck;
			}
			return true;
		}
	}

	public bool IsPeepVehicleType
	{
		get
		{
			if (type != AvatarType.Car)
			{
				return type == AvatarType.Truck;
			}
			return true;
		}
	}

	public bool IsCar => type == AvatarType.Car;

	public bool IsTruck => type == AvatarType.Truck;

	public override BaseComponent CreateComponent(EntityComponents ec)
	{
		return ec.mobile = new MobileComponent();
	}

	public override BaseData MoveOrCreateData(EntityData target, EntityData source)
	{
		MobileData obj = source?.mobile ?? new MobileData();
		MobileData result = obj;
		target.mobile = obj;
		return result;
	}

	public string GetName()
	{
		return Loc.Get(locname);
	}

	public string GetDesc()
	{
		return Loc.Get(locdesc);
	}
}

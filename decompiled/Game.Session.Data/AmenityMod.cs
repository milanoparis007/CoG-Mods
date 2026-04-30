using System;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Sim.Modules;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class AmenityMod : TagListMod
{
	public Fixnum perAmenityDelta = 0;

	public Fixnum perAmenityMultiplier = 1;

	public override ModQueryElement QueryMustProvide => ModQueryElement.Target;

	protected override Fixnum DoEvaluate(ModQuery query, Fixnum source)
	{
		GamblingModule gambling = query.FindBuildingForTarget().components.modules.gambling;
		if (gambling == null)
		{
			return source;
		}
		int num = 0;
		foreach (Label item in @if)
		{
			num += NumContainsConstructedById(gambling.data.amenities, item);
		}
		Fixnum fixnum = perAmenityDelta * num;
		Fixnum fixnum2 = new Fixnum((float)Math.Pow((float)perAmenityMultiplier, num));
		return source * fixnum2 + fixnum;
	}

	protected override void Validate(ModQuery query)
	{
		GamblingSettings gambling = Game.serv.globals.settings.gambling;
		foreach (Label item in @if)
		{
			if (gambling.FindAmenityById(item) == null)
			{
				Label label = item;
				Logger.Warning("Amenity mod: unrecognized amenity id " + label.ToString());
			}
		}
	}

	private int NumContainsConstructedById(List<AmenityData> dataList, Label amenityId)
	{
		int num = 0;
		foreach (AmenityData data in dataList)
		{
			if (data.defID == amenityId && data.IsEnabled(Game.ctx.clock.Now))
			{
				num++;
			}
		}
		return num;
	}

	public override string Explain(ModQuery query, Fixnum delta)
	{
		return Loc.Get("mod.check-amenities", "delta", AbstractModifier.FormatDelta(delta));
	}
}

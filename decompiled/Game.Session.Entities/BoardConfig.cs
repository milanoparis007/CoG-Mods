using System;
using System.Collections.Generic;
using Game.Core;
using Game.Session.Data;

namespace Game.Session.Entities;

public class BoardConfig : BaseConfig
{
	public bool canBeStretchedToSize;

	public bool ignoredBySetPieces;

	public WorldSize lotsize = new WorldSize(1f, 1f);

	public WorldPos collisionOffset = new WorldPos(0f, 0f);

	public float colliderHeight;

	public bool discoverable;

	public VisitRequirementList reqsToUseAsUpgrade;

	public override List<Type> RequiresConfigs => null;

	public override BaseComponent CreateComponent(EntityComponents ec)
	{
		return ec.board = new BoardComponent();
	}

	public override BaseData MoveOrCreateData(EntityData target, EntityData source)
	{
		BoardData obj = source?.board ?? new BoardData();
		BoardData result = obj;
		target.board = obj;
		return result;
	}

	public static void GetFootprintTestPoints(WorldSize lotSize, GridTransform tr, float p, List<WorldPos> outPoints, bool includeInnerPoints)
	{
		outPoints.Clear();
		if (includeInnerPoints)
		{
			outPoints.Add(tr.pos);
			if (lotSize.width >= 3f)
			{
				AddTileCenters();
			}
		}
		WorldPos footprintCorner = GetFootprintCorner(lotSize, tr, -1, -1, p);
		WorldPos footprintCorner2 = GetFootprintCorner(lotSize, tr, 1, -1, p);
		WorldPos footprintCorner3 = GetFootprintCorner(lotSize, tr, 1, 1, p);
		WorldPos footprintCorner4 = GetFootprintCorner(lotSize, tr, -1, 1, p);
		outPoints.Add(footprintCorner);
		outPoints.Add(footprintCorner2);
		outPoints.Add(footprintCorner3);
		outPoints.Add(footprintCorner4);
		void AddTileCenters()
		{
			float dx = (0f - (lotSize.width - 1f)) / 2f;
			WorldPos worldPos = tr.pos.Increment(dx, 0f);
			for (int i = 0; (float)i < lotSize.width; i++)
			{
				outPoints.Add(worldPos.Increment(i, 0f));
			}
		}
	}

	private static WorldPos GetFootprintCorner(WorldSize lotSize, GridTransform tr, int left, int bottom, float p = 1f)
	{
		WorldPos half = lotSize.Half;
		float x = (float)left * half.x;
		float y = (float)bottom * half.y;
		WorldPos worldPos = new WorldPos(x, y).Rotate(tr.deg);
		if (p != 1f)
		{
			worldPos *= p;
		}
		return tr.pos + worldPos;
	}

	public static void GetFootprintPoints(WorldSize lotSize, GridTransform tr, float p, List<WorldPos> outPoints)
	{
		WorldPos half = lotSize.Half;
		outPoints.Clear();
		for (float num = 0f - half.x; num <= half.x + 0.01f; num += 1f)
		{
			for (float num2 = 0f - half.y; num2 <= half.y + 0.01f; num2 += 1f)
			{
				WorldPos worldPos = new WorldPos(num, num2).Rotate(tr.deg);
				if (p != 1f)
				{
					worldPos *= p;
				}
				outPoints.Add(tr.pos + worldPos);
			}
		}
	}

	public static bool IsPointInsideFootprint(WorldSize lotSize, GridTransform tr, WorldPos pos)
	{
		WorldPos pos2 = tr.pos;
		float deg = tr.deg;
		if ((pos - pos2).MagnitudeSquared > lotSize.RadiusSquared)
		{
			return false;
		}
		WorldPos half = lotSize.Half;
		WorldPos worldPos = pos2 + new WorldPos(0f - half.x, 0f - half.y).Rotate(deg);
		WorldPos worldPos2 = pos2 + new WorldPos(half.x, 0f - half.y).Rotate(deg);
		WorldPos worldPos3 = pos2 + new WorldPos(half.x, half.y).Rotate(deg);
		WorldPos worldPos4 = worldPos2 - worldPos;
		WorldPos worldPos5 = worldPos3 - worldPos2;
		WorldPos b = pos - worldPos;
		WorldPos b2 = pos - worldPos2;
		float num = WorldPos.Dot(worldPos4, b);
		float num2 = WorldPos.Dot(worldPos4, worldPos4);
		float num3 = WorldPos.Dot(worldPos5, b2);
		float num4 = WorldPos.Dot(worldPos5, worldPos5);
		if (0f <= num && num <= num2 && 0f <= num3)
		{
			return num3 <= num4;
		}
		return false;
	}
}

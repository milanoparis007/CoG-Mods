using Game.Core;

namespace Game.Session.Entities;

public sealed class BoardData : BaseData
{
	public PlayerFlags known;

	public LotBeadHandle bead;

	public WorldPos worldpos;

	public float deg;

	public bool IsSet => !worldpos.IsZero;

	public GridTransform Transform => new GridTransform(worldpos, deg);

	public void SetPos(WorldPos pos, float deg)
	{
		worldpos = pos;
		this.deg = deg;
	}

	public void ClearPos()
	{
		SetPos(WorldPos.Zero, 0f);
	}
}

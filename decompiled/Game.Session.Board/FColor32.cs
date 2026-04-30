using UnityEngine;

namespace Game.Session.Board;

public struct FColor32
{
	public byte r;

	public byte g;

	public byte b;

	public byte a;

	public static implicit operator Color32(FColor32 input)
	{
		return new Color32(input.r, input.g, input.b, input.a);
	}

	public static implicit operator Color(FColor32 input)
	{
		return new Color32(input.r, input.g, input.b, input.a);
	}
}

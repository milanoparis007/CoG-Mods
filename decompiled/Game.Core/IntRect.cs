using UnityEngine;

namespace Game.Core;

public struct IntRect
{
	public int x;

	public int y;

	public int width;

	public int height;

	public IntRect(int x, int y, int width, int height)
	{
		this.x = x;
		this.y = y;
		this.width = width;
		this.height = height;
	}

	public bool Contains(int xpos, int ypos)
	{
		return new Rect(x, y, width, height).Contains(new Vector2(xpos, ypos));
	}
}

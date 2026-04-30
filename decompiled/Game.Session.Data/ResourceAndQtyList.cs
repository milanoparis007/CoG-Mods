using System.Collections.Generic;
using Game.Core;
using SomaSim.Util;

namespace Game.Session.Data;

public sealed class ResourceAndQtyList
{
	public const int DEFAULT_CAPACITY = 4;

	public List<ResourceAndQty> data;

	public ResourceAndQtyList(int capacity)
	{
		data = new List<ResourceAndQty>(capacity);
	}

	public ResourceAndQtyList()
		: this(4)
	{
	}

	public void Clear()
	{
		data.Clear();
	}

	public int FindIndex(Label id)
	{
		int i = 0;
		for (int count = data.Count; i < count; i++)
		{
			if (data[i].id == id)
			{
				return i;
			}
		}
		return -1;
	}

	public void Increment(ResourceAndQty resAndQty)
	{
		Increment(resAndQty.id, resAndQty.qty);
	}

	public void Increment(Label id, Fixnum delta, bool removeEmpties = false)
	{
		int num = FindIndex(id);
		if (num < 0)
		{
			data.Add(new ResourceAndQty(id, delta));
			num = data.Count - 1;
		}
		else
		{
			data[num] = new ResourceAndQty(id, data[num].qty + delta);
		}
		if (removeEmpties && data[num].qty == 0)
		{
			data.RemoveAt(num);
		}
	}

	public Fixnum Get(Label id)
	{
		int num = FindIndex(id);
		if (num >= 0)
		{
			return data[num].qty;
		}
		return new Fixnum(0);
	}

	public bool HasAnything()
	{
		if (data == null)
		{
			return false;
		}
		int i = 0;
		for (int count = data.Count; i < count; i++)
		{
			if (data[i].qty.IsNotZero)
			{
				return true;
			}
		}
		return false;
	}
}

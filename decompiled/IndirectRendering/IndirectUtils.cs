using System.Collections.Generic;
using UnityEngine;

namespace IndirectRendering;

public static class IndirectUtils
{
	public static ComputeBuffer ReleaseBuffer(ref ComputeBuffer buffer)
	{
		if (buffer != null)
		{
			buffer.Release();
		}
		return null;
	}

	public static T RandomRetrieveRemove<T>(this List<T> list)
	{
		int index = Random.Range(0, list.Count);
		T result = list[index];
		list.RemoveAt(index);
		return result;
	}
}

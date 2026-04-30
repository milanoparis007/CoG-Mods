using System;
using System.Collections.Generic;
using UnityEngine;

namespace AwesomeCharts;

[Serializable]
public class ReusableObjectsPool
{
	private Transform parent;

	private GameObject objectPrefab;

	private List<UnityEngine.Object> objectsPool;

	private string defaultObjectPrefabPath;

	private int poolSize;

	private bool sizeDirty;

	private bool allDirty;

	private ViewCreator viewCreator = new ViewCreator();

	public GameObject ObjectPrefab
	{
		get
		{
			return objectPrefab;
		}
		set
		{
			if (objectPrefab != value)
			{
				objectPrefab = value;
				allDirty = true;
			}
		}
	}

	public string DefaultObjectPrefabPath
	{
		get
		{
			return defaultObjectPrefabPath;
		}
		set
		{
			if (defaultObjectPrefabPath != value)
			{
				defaultObjectPrefabPath = value;
				allDirty = true;
			}
		}
	}

	public int PoolSize
	{
		get
		{
			return poolSize;
		}
		set
		{
			if (poolSize != value)
			{
				poolSize = value;
				sizeDirty = true;
			}
		}
	}

	public Transform Parent
	{
		get
		{
			return parent;
		}
		set
		{
			if (parent != value)
			{
				parent = value;
				allDirty = true;
			}
		}
	}

	public ReusableObjectsPool()
	{
	}

	public ReusableObjectsPool(Transform parent)
	{
		this.parent = parent;
	}

	public void Update()
	{
		if (objectsPool == null)
		{
			objectsPool = new List<UnityEngine.Object>();
		}
		_ = objectsPool.Count;
		for (int num = (allDirty ? objectsPool.Count : (objectsPool.Count - poolSize)); num > 0; num--)
		{
			UnityEngine.Object obj = objectsPool[objectsPool.Count - 1];
			UnityEngine.Object.DestroyImmediate(obj);
			objectsPool.Remove(obj);
		}
		UnityEngine.Object obj2 = ((objectPrefab != null) ? objectPrefab : Resources.Load(DefaultObjectPrefabPath));
		for (int num2 = ((!(obj2 == null)) ? (poolSize - objectsPool.Count) : 0); num2 > 0; num2--)
		{
			UnityEngine.Object item = viewCreator.InstantiateWithPrefab(obj2, parent);
			objectsPool.Add(item);
		}
		allDirty = false;
		sizeDirty = false;
	}

	public GameObject GetReusableObject(int index)
	{
		if (index < 0 || index >= objectsPool.Count)
		{
			throw new IndexOutOfRangeException();
		}
		return objectsPool[index] as GameObject;
	}

	public bool IsDirty()
	{
		if (!sizeDirty)
		{
			return allDirty;
		}
		return true;
	}
}

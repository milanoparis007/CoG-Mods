using System;
using System.Collections.Generic;
using Game.Core;
using Game.Session.Entities;
using IndirectRendering;
using UnityEngine;

namespace Game.Session.Assets;

public sealed class ModelProxy
{
	public enum Status
	{
		Uninitialized,
		Hidden,
		Queued,
		Ready
	}

	public Entity entity;

	public string prefabName;

	public Vector3 position;

	public Quaternion rotation;

	public Status status;

	public DynamicMeshHandle dynamicHandle;

	private List<ModelSubProxy> submodels = new List<ModelSubProxy>();

	public bool IsHidden => status == Status.Hidden;

	public bool IsQueued => status == Status.Queued;

	public bool IsShowing => submodels.Count > 0;

	public void SetPositionAndRotation(Vector3 position, Quaternion rotation)
	{
		this.position = position;
		this.rotation = rotation;
		RefreshModelPositionAndRotation();
	}

	private void RefreshModelPositionAndRotation()
	{
		foreach (ModelSubProxy submodel in submodels)
		{
			Vector3 pos = position;
			Vector3 vector = rotation * (submodel.slotData.offset.AsVector3XZ + submodel.slotData.plusminus.AsVector3XZ);
			pos += vector;
			if (entity.config.model.indirectInstancing && !Game.ctx.models.IsIndirectEnabled)
			{
				WorldPos half = entity.config.board.lotsize.Half;
				WorldSize worldSize = entity.config.model.customIndirectOffset * entity.config.board.lotsize.height;
				pos -= rotation * (half.AsVector3XZ + worldSize.AsVector3XZ);
			}
			if (submodel.model != null)
			{
				submodel.model.transform.position = pos;
				submodel.model.transform.rotation = rotation;
			}
			if (submodel.indirectHandle != null)
			{
				IndirectRenderer.UpdateTransform(submodel.indirectHandle, pos, rotation.eulerAngles.y);
			}
		}
	}

	public ModelSubProxy GetSubProxy(int index)
	{
		return submodels[index];
	}

	public List<ModelSubProxy> GetAllSubProxiesUnsafe()
	{
		return submodels;
	}

	public void PushSubproxy(ModelSubProxy subproxy)
	{
		submodels.Add(subproxy);
	}

	public void Teardown()
	{
		int count = submodels.Count;
		for (int i = 0; i < count; i++)
		{
			ModelSubProxy modelSubProxy = submodels[i];
			if (modelSubProxy.model != null)
			{
				UnityEngine.Object.Destroy(modelSubProxy.model);
			}
			modelSubProxy.model = null;
			if (modelSubProxy.dynamicHandle != null)
			{
				modelSubProxy.dynamicHandle.Remove();
			}
			modelSubProxy.dynamicHandle = null;
			if (modelSubProxy.indirectHandle != null)
			{
				IndirectRenderer.RemoveInstance(modelSubProxy.indirectHandle);
			}
			modelSubProxy.indirectHandle = null;
		}
	}

	public void DoForeach(Action<ModelSubProxy> action)
	{
		foreach (ModelSubProxy submodel in submodels)
		{
			action(submodel);
		}
	}

	public void SetColor(Color c)
	{
		for (int i = 0; i < submodels.Count; i++)
		{
			ModelSubProxy modelSubProxy = submodels[i];
			if (modelSubProxy.indirectHandle != null)
			{
				IndirectRenderer.UpdateOverlayColor(modelSubProxy.indirectHandle, c);
			}
		}
	}
}

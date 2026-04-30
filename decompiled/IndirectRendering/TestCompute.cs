using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Services;
using Game.Session.Assets;
using SomaSim.Util;
using UnityEngine;

namespace IndirectRendering;

public class TestCompute : MonoBehaviour
{
	public enum TestState
	{
		NotStarted,
		Working,
		Done
	}

	[SerializeField]
	private bool useRandom;

	[SerializeField]
	private List<GameObject> prefabs;

	public TestState State { get; private set; }

	public bool PassedCSTests { get; private set; }

	private void Start()
	{
		State = TestState.NotStarted;
	}

	private void Update()
	{
		if (State == TestState.NotStarted)
		{
			Logger.LogAlways("Starting CS test");
			State = TestState.Working;
			StartCoroutine(CheckIfInstancedIndirectValid(delegate(bool isValid)
			{
				PassedCSTests = isValid;
				State = TestState.Done;
				Logger.LogAlways("Completed CS test: " + PassedCSTests);
			}));
		}
	}

	private IEnumerator CheckIfInstancedIndirectValid(Action<bool> onComplete)
	{
		IndirectRenderer.PreGameSetup();
		InitTestData();
		yield return null;
		Vector3 startCamPosition = Camera.main.transform.position;
		Camera.main.transform.position = new Vector3(0f, 0f, 0f);
		yield return null;
		yield return null;
		bool firstTest = IndirectRenderer.TestBuffers();
		yield return null;
		LODDefinitionValues lODDefinitionValues = IndirectRenderer.GetLODDefinitionValues();
		Camera.main.transform.position = new Vector3(0f, 0f, 0f - (lODDefinitionValues.lod1 + 1f));
		yield return null;
		yield return null;
		bool flag = IndirectRenderer.TestBuffers();
		Camera.main.transform.position = startCamPosition;
		IndirectRenderer.PostGameDestroy();
		bool obj = firstTest && flag;
		onComplete(obj);
	}

	private void InitTestData()
	{
		Xorshift xorshift = new Xorshift();
		uint seed = ((!useRandom) ? 1u : ((uint)DateTime.Now.Second));
		xorshift.Init(seed);
		for (int i = 0; i < prefabs.Count; i++)
		{
			GameObject prefab = prefabs[i];
			VertexPaletteData vertexPaletteData = new VertexPaletteData();
			vertexPaletteData.GenerateNewValues(xorshift);
			int num = i;
			PerInstanceRuntimeData data = new PerInstanceRuntimeData
			{
				position = new Vector3(5f * (float)i, 0f, 0f),
				rotation = 0f,
				overlayColor = ColorConstants.OVERLAY_DEFAULT,
				offset = Vector3.zero,
				_paletteData = vertexPaletteData,
				springHueShift = 1f,
				fallHueShift = 0f,
				fogHideShift = 0f,
				snowThreshold = 1f,
				entityId = num,
				nodeId = num
			};
			IndirectRenderer.AddInstance(EntityID.FromID((uint)num), prefab, data);
		}
	}

	[ContextMenu("AutoPopulate")]
	private void Populate()
	{
		prefabs.Clear();
		GameObject[] array = Resources.LoadAll<GameObject>("Buildings");
		foreach (GameObject gameObject in array)
		{
			string text = gameObject.name.ToLower();
			if (text.StartsWith("bldg") && !text.Contains("legacy"))
			{
				prefabs.Add(gameObject);
			}
		}
	}
}

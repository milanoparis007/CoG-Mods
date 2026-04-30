using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.Services.Maps;
using UnityEngine;

namespace Game.Session.Board;

public class FogOfWarManager : AbstractSessionManager, IAnimatingManager, ISessionManager
{
	public bool DEBUG_PLANE_RENDERER;

	public const string FOG_OF_WAR_TEX_PARAM = "_FogOfWarTex";

	public const string FOG_OF_WAR_COLOR = "_FogOfWarColor";

	private Vector2Int RESOLUTION = new Vector2Int(1024, 1024);

	private Texture2D _texture;

	private MeshRenderer debugPlaneRenderer;

	private Color[] _colorBuffer;

	private bool _isDirty;

	public override void OnInitializeStarted()
	{
		Color white = Color.white;
		_texture = new Texture2D(RESOLUTION.x, RESOLUTION.y);
		_colorBuffer = new Color[RESOLUTION.x * RESOLUTION.y];
		for (int i = 0; i < RESOLUTION.y; i++)
		{
			for (int j = 0; j < RESOLUTION.x; j++)
			{
				int num = i * RESOLUTION.x + j;
				_colorBuffer[num] = white;
			}
		}
		_texture.SetPixels(_colorBuffer);
		Shader.SetGlobalTexture("_FogOfWarTex", _texture);
		if (DEBUG_PLANE_RENDERER)
		{
			IntSize mapSize = Game.ctx.session.mapconfig.map.mapSize;
			GameObject gameObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
			gameObject.transform.position = new Vector3(mapSize.width / 2, 0.05f, mapSize.height / 2);
			gameObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
			gameObject.transform.localScale = new Vector3(mapSize.width, mapSize.height, 1f);
			gameObject.name = "TEST FogOfWar Plane";
			debugPlaneRenderer = gameObject.GetComponent<MeshRenderer>();
			debugPlaneRenderer.material = new Material(Shader.Find("Unlit/Transparent"))
			{
				mainTexture = _texture
			};
		}
	}

	public override void OnReleased()
	{
		if (debugPlaneRenderer != null)
		{
			Object.Destroy(debugPlaneRenderer.gameObject);
			debugPlaneRenderer = null;
		}
		_colorBuffer = null;
	}

	public IEnumerator RefreshEntireMap()
	{
		PlayerID pid = Game.ctx.players.Human.PID;
		List<Node> allNodesUnsafe = Game.ctx.board.nodes.GetAllNodesUnsafe();
		foreach (Node item in allNodesUnsafe)
		{
			if (item.known.Get(pid))
			{
				RevealNodeRegion(item, isFirstCall: true);
				yield return null;
			}
		}
	}

	public void RevealEntireMap()
	{
		Color clear = Color.clear;
		int i = 0;
		for (int num = _colorBuffer.Length; i < num; i++)
		{
			_colorBuffer[i] = clear;
		}
		_isDirty = true;
	}

	public void RevealNodeRegion(Node node, bool isFirstCall)
	{
		if (!node.IsValid)
		{
			return;
		}
		IntSize mapSize = Game.ctx.session.mapconfig.map.mapSize;
		Vector2 vector = new Vector2((float)mapSize.width / (float)RESOLUTION.x, (float)mapSize.height / (float)RESOLUTION.y);
		_ = Color.white;
		Color clear = Color.clear;
		MapNodesConfig nodesConfigByID = Game.ctx.board.MapConfig.GetNodesConfigByID(node.cfg);
		int num = nodesConfigByID.nodeSpacing.width;
		int num2 = nodesConfigByID.nodeSpacing.height;
		_ = node.pos.AsVector3XZ;
		Vector3 vector2 = Quaternion.Euler(0f, node.deg, 0f) * Vector3.right;
		Vector3 vector3 = Quaternion.Euler(0f, node.deg, 0f) * -Vector3.forward;
		PlayerID pID = Game.ctx.players.Human.PID;
		if (isFirstCall)
		{
			Game.ctx.board.nodes.VisitNeighborhoodBFS(node, 100, delegate(Node testNode)
			{
				RevealNodeRegion(testNode, isFirstCall: false);
			}, delegate(Node testNode)
			{
				float magnitude = (testNode.pos - node.pos).Magnitude;
				return !testNode.HasRoad && magnitude < 20f;
			});
			NodeEdgeID[] edges = node.edges;
			foreach (NodeEdgeID id in edges)
			{
				NodeEdge edge = Game.ctx.board.nodes.GetEdge(id);
				if (edge == null)
				{
					continue;
				}
				Node node2 = node;
				NodeID otherNodeID = edge.GetOtherNodeID(node2.id);
				if (otherNodeID.IsValid)
				{
					Node node3 = Game.ctx.board.nodes.GetNode(otherNodeID);
					if (node3.known.Get(pID) && node2.cfg.Index != node3.cfg.Index)
					{
						MapNodesConfig nodesConfigByID2 = Game.ctx.board.MapConfig.GetNodesConfigByID(node3.cfg);
						WorldPos worldPos = node3.pos - node.pos;
						num = Mathf.CeilToInt(worldPos.Magnitude) + Mathf.Max(num, nodesConfigByID2.nodeSpacing.width);
						num2 = Mathf.Max(num2, nodesConfigByID2.nodeSpacing.height);
						_ = WorldPos.Lerp(node3.pos, node.pos, 0.5f).AsVector3XZ;
						vector2 = worldPos.Normalized.AsVector3XZ;
						vector3 = Quaternion.Euler(0f, 90f, 0f) * vector2;
					}
				}
			}
		}
		float num4 = (float)num / 2f;
		float num5 = (float)num2 / 2f;
		Vector3 vector4 = node.pos.AsVector3XZ - vector2 * num4 - vector3 * num5;
		int x = RESOLUTION.x;
		int y = RESOLUTION.y;
		float x2 = vector.x;
		float y2 = vector.y;
		Color color = clear;
		for (float num6 = 0f; num6 < (float)num2; num6 += y2)
		{
			Vector3 vector5 = vector4 + vector3 * num6;
			for (float num7 = 0f; num7 < (float)num; num7 += x2)
			{
				Vector3 vector6 = vector5 + vector2 * num7;
				int num8 = (int)(vector6.x / x2);
				int num9 = (int)(vector6.z / y2);
				for (int num10 = -1; num10 < 2; num10++)
				{
					for (int num11 = -1; num11 < 2; num11++)
					{
						int num12 = num8 + num11;
						int num13 = num9 + num10;
						if (num12 >= 0 && num13 >= 0 && num12 < x && num13 < y)
						{
							int num14 = num13 * x + num12;
							_colorBuffer[num14] = color;
						}
					}
				}
			}
		}
		_isDirty = true;
	}

	public void UpdateAnimations(GameAnimUpdate anim)
	{
		if (_isDirty)
		{
			_isDirty = false;
			_texture.SetPixels(_colorBuffer);
			_texture.Apply();
			Shader.SetGlobalTexture("_FogOfWarTex", _texture);
		}
	}
}

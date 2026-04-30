using UnityEngine;

public class WaterPreRender : MonoBehaviour
{
	[SerializeField]
	private Camera _refCam;

	[SerializeField]
	private Shader _waterMetaShader;

	[SerializeField]
	private float _renderDepth;

	[Header("Temporary")]
	private Camera _camera;

	private int _cullingLayerMask;

	private RenderTexture _renderTexture;

	private Material _depthMaterial;

	private void Awake()
	{
		_camera = GetComponent<Camera>();
		_renderTexture = new RenderTexture(Screen.width, Screen.height, 24);
		_cullingLayerMask = 1 << LayerMask.NameToLayer("Oil");
	}

	private void Update()
	{
		_camera.CopyFrom(_refCam);
		_camera.renderingPath = RenderingPath.Forward;
		_camera.clearFlags = CameraClearFlags.Color;
		_camera.backgroundColor = Color.black;
		_camera.depthTextureMode = DepthTextureMode.Depth;
		_camera.depth = _renderDepth;
		_camera.cullingMask = _cullingLayerMask;
		_camera.forceIntoRenderTexture = true;
		_camera.targetTexture = _renderTexture;
		_camera.SetReplacementShader(_waterMetaShader, "WaterMeta");
	}

	private void LateUpdate()
	{
		Shader.SetGlobalTexture("_WaterMetaTexture", _renderTexture);
	}
}

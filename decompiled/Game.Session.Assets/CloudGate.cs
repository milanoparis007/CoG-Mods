using Game.Core;
using SomaSim.Util;
using UnityEngine;

namespace Game.Session.Assets;

public class CloudGate
{
	public const string CLOUDS_CITY_SIZE = "_CloudsCitySize";

	public const string CLOUDS_OPACITY = "_CloudsOpacity";

	public const int CLOUDS_HEIGHT = 10;

	public GameObject container;

	private bool _enabled;

	private bool _on;

	public void Initialize(GameObject container)
	{
		IntSize mapSize = Game.ctx.session.mapconfig.map.mapSize;
		IntSize intSize = new IntSize(mapSize.width * 2, mapSize.height * 2);
		this.container = container;
		container.transform.localScale = new Vector3(intSize.width, intSize.height, 1f);
		container.transform.localPosition = new Vector3(mapSize.width / 2, 10f, mapSize.height / 2);
		_enabled = Game.serv.saveload.prefs.game.weatherEnabled;
		Toggle(isOn: false);
		Shader.SetGlobalFloat("_CloudsOpacity", 0f);
		Shader.SetGlobalVector("_CloudsCitySize", new Vector4(intSize.width, intSize.height, 0f, 0f));
		Game.serv.camera.OnCameraZoomPercentage.Add(OnCameraZoom);
	}

	public void Release()
	{
		Game.serv.camera.OnCameraZoomPercentage.Remove(OnCameraZoom);
		Toggle(isOn: false);
		container = null;
	}

	private void Toggle(bool isOn)
	{
		_on = isOn && _enabled;
		container.SetActive(isOn);
		Shader.SetGlobalFloat("_CloudsOpacity", 0f);
	}

	private void OnCameraZoom(float zoomPercentage)
	{
		FloatRange showCloudsZoomRange = Game.serv.globals.settings.general.territory.showCloudsZoomRange;
		bool flag = zoomPercentage >= showCloudsZoomRange.from;
		if (!_on && !flag)
		{
			Toggle(isOn: true);
		}
		else if (_on && flag)
		{
			float value = MathUtil.Clamp(MathUtil.Uninterpolate(zoomPercentage, showCloudsZoomRange.from, showCloudsZoomRange.to), 0f, 1f);
			Shader.SetGlobalFloat("_CloudsOpacity", value);
		}
	}
}

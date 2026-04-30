namespace Game.Services;

public class UIReference
{
	public const string GAME_UI_SCENE_NAME = "GameUI";

	public const string SESSION_UI_SCENE_NAME = "SessionUI";

	public const string MOVING_UI_SCENE_NAME = "MovingUI";

	public UIScene Scene { get; private set; }

	public string Path { get; private set; }

	public UIType Type { get; private set; }

	public UIReference(string path, UIType type, UIScene scene)
	{
		Scene = scene;
		Path = path;
		Type = type;
	}

	public string GetSceneName()
	{
		return GetSceneName(Scene);
	}

	public static bool IsUISceneName(string name)
	{
		if (!(name == "GameUI") && !(name == "SessionUI"))
		{
			return name == "MovingUI";
		}
		return true;
	}

	public static string GetSceneName(UIScene scene)
	{
		return scene switch
		{
			UIScene.GameUI => "GameUI", 
			UIScene.SessionUI => "SessionUI", 
			UIScene.MovingUI => "MovingUI", 
			_ => "GameUI", 
		};
	}
}

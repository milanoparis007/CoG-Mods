namespace Game.Services;

public struct CameraTween
{
	public static readonly CameraTween DEFAULT_ZOOM_TWEEN = new CameraTween
	{
		timeSeconds = 0.1f,
		easing = LeanTweenType.easeOutQuad,
		interruptAllTweens = true
	};

	public static readonly CameraTween SLOW_ZOOM_TWEEN = new CameraTween
	{
		timeSeconds = 1f,
		easing = LeanTweenType.easeOutQuad
	};

	public static readonly CameraTween VERY_SLOW_ZOOM_TWEEN = new CameraTween
	{
		timeSeconds = 3f,
		easing = LeanTweenType.easeOutQuad
	};

	public static readonly CameraTween DEFAULT_FOCUS_TWEEN = new CameraTween
	{
		timeSeconds = 0.3f,
		easing = LeanTweenType.easeInOutCubic,
		interruptAllTweens = true
	};

	public static readonly CameraTween SLOW_FOCUS_TWEEN = new CameraTween
	{
		timeSeconds = 0.7f,
		easing = LeanTweenType.easeOutQuad,
		interruptAllTweens = true
	};

	public static readonly CameraTween SLOW_FOCUS_TWEEN_NOINT = new CameraTween
	{
		timeSeconds = 0.7f,
		easing = LeanTweenType.easeOutQuad
	};

	public float timeSeconds;

	public LeanTweenType easing;

	public bool interruptAllTweens;

	public bool IsSet
	{
		get
		{
			if (!(timeSeconds > 0f))
			{
				return interruptAllTweens;
			}
			return true;
		}
	}

	public static CameraTween AtStartup(float seconds, LeanTweenType easing = LeanTweenType.easeInOutQuad)
	{
		return new CameraTween
		{
			timeSeconds = seconds,
			easing = easing
		};
	}
}

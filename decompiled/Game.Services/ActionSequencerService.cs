using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SomaSim.Util;
using UnityEngine;

namespace Game.Services;

public sealed class ActionSequencerService : AbstractService
{
	public class CoroutineRunner : MonoBehaviour
	{
	}

	private CoroutineRunner _runner;

	private Queue<Action> _actions;

	private IEnumerator _coroutine;

	private bool _idle;

	private readonly float EMPTY_QUEUE_DELAY_SECONDS = 5f;

	public int UnityThreadID { get; private set; }

	public bool IsRunningOnUnityThread()
	{
		return Thread.CurrentThread.ManagedThreadId == UnityThreadID;
	}

	public override void OnCreated()
	{
		_runner = Game.instance.gameObject.AddComponent<CoroutineRunner>();
		_actions = new Queue<Action>();
		_coroutine = StartRunning();
		UnityThreadID = Thread.CurrentThread.ManagedThreadId;
	}

	public override void OnInitialized()
	{
		_runner.StartCoroutine(_coroutine);
	}

	public override void OnReleased()
	{
		StopAll();
	}

	public override void OnDestroyed()
	{
		UnityEngine.Object.Destroy(_runner);
		_coroutine = null;
		_actions = null;
		_runner = null;
	}

	public void StopAll()
	{
		_actions.Clear();
		_runner.StopAllCoroutines();
	}

	public void Add(Action action)
	{
		_actions.Enqueue(action);
		if (_idle)
		{
			_runner.StopCoroutine(_coroutine);
			_runner.StartCoroutine(_coroutine);
		}
	}

	private IEnumerator StartRunning()
	{
		while (true)
		{
			if (_actions.Count > 0)
			{
				_idle = false;
				_actions.Dequeue()();
				yield return null;
			}
			else
			{
				_idle = true;
				yield return new WaitForSeconds(EMPTY_QUEUE_DELAY_SECONDS);
			}
		}
	}

	public void CallNextFrame(Action action)
	{
		_runner.StartCoroutine(MakeNextFrameCoroutine(action));
	}

	private IEnumerator MakeNextFrameCoroutine(Action action)
	{
		yield return null;
		action();
	}

	public Coroutine StartCoroutine(IEnumerator coroutine)
	{
		return _runner.StartCoroutine(coroutine);
	}

	public void StopCoroutine(IEnumerator coroutine)
	{
		_runner.StopCoroutine(coroutine);
	}

	public CoroutineTask StartCoroutineTask(IEnumerator coroutine, Action<CoroutineTask> onFinished = null)
	{
		return _runner.StartCoroutineTask(coroutine, onFinished);
	}

	public Coroutine StartThreadedProducerConsumer<T>(Func<T> producer, Action<T> consumer, string debugname = null)
	{
		return StartCoroutine(MakeThreadWrapper(producer, consumer, debugname ?? string.Empty));
	}

	private IEnumerator MakeThreadWrapper<T>(Func<T> producer, Action<T> consumer, string name)
	{
		T result = default(T);
		Action action = delegate
		{
			try
			{
				result = producer();
			}
			catch (Exception ex)
			{
				Logger.Warning("Error in threaded producer " + name + "\n" + ex);
			}
		};
		Task t = Task.Run(action);
		while (!t.IsCompleted)
		{
			yield return null;
		}
		consumer(result);
	}
}

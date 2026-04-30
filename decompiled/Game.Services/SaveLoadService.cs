using System;
using System.Collections;
using System.Text;
using Game.Platform;
using Game.Services.Filesystem;
using SomaSim.SION;
using SomaSim.Util;

namespace Game.Services;

public class SaveLoadService : AbstractService
{
	private const string PREFS_FILE = "settings.txt";

	private const string PROGRESS_FILE = "gameplay.txt";

	public Listeners<string> OnSavedPrefs;

	private bool _isPrefsLoadingDone;

	public PreferencesFile prefs { get; private set; }

	public GameplayProgressFile progress { get; private set; }

	public ParallelBatchingSave batcher { get; private set; }

	public override bool IsLoadingDone => _isPrefsLoadingDone;

	public override void OnCreated()
	{
		prefs = new PreferencesFile();
		progress = new GameplayProgressFile();
		OnSavedPrefs = new Listeners<string>();
		batcher = new ParallelBatchingSave();
		batcher.Initialize();
	}

	public override void OnDestroyed()
	{
		batcher.Release();
		batcher = null;
		OnSavedPrefs = null;
		progress = null;
		prefs = null;
	}

	public override void OnStartLoading()
	{
		if (!Game.platform.LoadPrefsAfterEngagement)
		{
			LoadPrefsOnStartup();
		}
		else
		{
			_isPrefsLoadingDone = true;
		}
	}

	public void LoadPrefsOnStartup()
	{
		Game.serv.sequencer.StartCoroutine(PrefsLoadRoutine());
	}

	private IEnumerator LoadPrefsRoutine<T>(string prefsFile, Action<T> onSuccess)
	{
		PlatformLoadPrefsRequest loadRequest = new PlatformLoadPrefsRequest
		{
			name = prefsFile
		};
		byte[] preferenceBytes = null;
		IEnumerator loadRoutine = Game.platform.LoadPrefs(loadRequest, delegate(byte[] bytes)
		{
			preferenceBytes = bytes;
		}, delegate(kLoadFileResult failureReason)
		{
			Logger.Warning($"Failed to load prefs: {failureReason}");
		});
		while (loadRoutine.MoveNext())
		{
			yield return null;
		}
		try
		{
			if (preferenceBytes != null)
			{
				string text = Encoding.UTF8.GetString(preferenceBytes);
				object source = SION.Parse(text);
				T val = Game.serv.serializer.instance.Deserialize<T>(source);
				if (val != null)
				{
					onSuccess(val);
				}
				else
				{
					Logger.LogAlways("Cannot parse string " + text);
				}
			}
		}
		catch (Exception ex)
		{
			Logger.Error("Exception while loading prefs file " + prefsFile + ": " + ex.Message);
		}
	}

	private IEnumerator SavePrefsRoutine<T>(string prefsFile, T obj)
	{
		PlatformSavePrefsRequest saveRequest = null;
		bool flag = false;
		try
		{
			string s = SION.Print(Game.serv.serializer.instance.Serialize(obj, specifyValueTypes: false), compressed: false);
			byte[] bytes = Encoding.UTF8.GetBytes(s);
			saveRequest = new PlatformSavePrefsRequest
			{
				name = prefsFile,
				data = bytes
			};
		}
		catch (Exception ex)
		{
			Logger.LogAlways("Exception while saving prefs " + prefsFile + ": " + ex.Message);
			flag = true;
		}
		if (!flag)
		{
			IEnumerator saveRoutine = Game.platform.SavePrefs(saveRequest, delegate
			{
				Logger.LogAlways("Save " + prefsFile + " success!");
				OnSavedPrefs.Invoke(prefsFile);
			}, delegate(kSaveFileResult failureReason)
			{
				Logger.Error($"Failed to save prefs: {failureReason}");
			});
			while (saveRoutine.MoveNext())
			{
				yield return null;
			}
		}
	}

	private IEnumerator PrefsLoadRoutine()
	{
		IEnumerator loadPrefsRoutine = LoadPrefsRoutine("settings.txt", delegate(PreferencesFile loaded)
		{
			prefs = loaded;
		});
		while (loadPrefsRoutine.MoveNext())
		{
			yield return null;
		}
		IEnumerator loadProgressRoutine = LoadPrefsRoutine("gameplay.txt", delegate(GameplayProgressFile loaded)
		{
			progress = loaded;
		});
		while (loadProgressRoutine.MoveNext())
		{
			yield return null;
		}
		progress.stats.session++;
		Game.serv.camera.VerifyPrefsFile();
		IEnumerator savePrefsRoutine = SavePrefsRoutine("settings.txt", prefs);
		while (savePrefsRoutine.MoveNext())
		{
			yield return null;
		}
		IEnumerator saveProgressRoutine = SavePrefsRoutine("gameplay.txt", progress);
		while (saveProgressRoutine.MoveNext())
		{
			yield return null;
		}
		_isPrefsLoadingDone = true;
	}

	public void SavePrefs()
	{
		Game.serv.sequencer.StartCoroutine(SavePrefsRoutine("settings.txt", prefs));
	}

	public void SaveProgress()
	{
		Game.serv.sequencer.StartCoroutine(SavePrefsRoutine("gameplay.txt", progress));
	}

	public IEnumerator SaveGame(SaveFileMetadata meta, SaveFileContents file)
	{
		byte[] texture = null;
		using (new BlockStopwatch("SaveLoadService CREATING SCREENSHOT"))
		{
			try
			{
				texture = Game.serv.camera.TakeScreenshotForSavefile();
			}
			catch (Exception ex)
			{
				Logger.Error("Exception taking screenshot: " + ex.Message);
			}
		}
		PlatformSaveSlotRequest saveRequest = new PlatformSaveSlotRequest
		{
			guid = meta.slotId,
			metadata = meta,
			savedata = file,
			texture = texture
		};
		yield return null;
		bool? success = null;
		using (new BlockStopwatch("SaveLoadService SAVEGAME"))
		{
			yield return Game.platform.SaveGame(saveRequest, delegate
			{
				Logger.LogAlways("Save: success!");
				success = true;
			}, delegate(kSaveFileResult failureReason)
			{
				Logger.Error($"Save: failure {failureReason}");
				success = false;
			});
			while (!success.HasValue)
			{
				yield return null;
			}
		}
	}

	public IEnumerator LoadGame(IPlatformSaveSlotDescriptor slot, SaveFileContents results)
	{
		bool? success = null;
		byte[] loadedBytes = null;
		using (new BlockStopwatch("READING FROM DISK"))
		{
			yield return Game.platform.LoadGame(slot, delegate(byte[] bytes)
			{
				loadedBytes = bytes;
				success = true;
			}, delegate(kLoadFileResult failureReason)
			{
				Logger.Error($"Error loading slot: {failureReason}");
				success = false;
			});
			while (!success.HasValue)
			{
				yield return null;
			}
		}
		using (new BlockStopwatch("PARSING BYTES"))
		{
			SaveFileContents saveFileContents = SaveFileContents.FromBytes(loadedBytes);
			results.data = saveFileContents.data;
		}
	}

	public IEnumerator DeleteGame(IPlatformSaveSlotDescriptor slot, Action onSuccess, Action<kDeleteFileResult> onFailure)
	{
		bool? success = null;
		using (new BlockStopwatch("READING FROM DISK"))
		{
			yield return Game.platform.DeleteGame(slot, delegate
			{
				success = true;
				onSuccess();
			}, delegate(kDeleteFileResult failureReason)
			{
				Logger.Error($"Error loading slot: {failureReason}");
				onFailure(failureReason);
				success = false;
			});
			while (!success.HasValue)
			{
				yield return null;
			}
		}
	}
}

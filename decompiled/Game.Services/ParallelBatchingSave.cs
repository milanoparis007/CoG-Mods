using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SomaSim.SION;

namespace Game.Services;

public sealed class ParallelBatchingSave
{
	public sealed class ResultsCollector<K> : ConcurrentDictionary<K, Hashtable>
	{
		internal ArrayList ValuesToArrayList()
		{
			return new ArrayList((from e in this
				orderby e.Key descending
				select e.Value).ToList());
		}
	}

	public abstract class JobCollection<K, T>
	{
		public sealed class SaveJob
		{
			public int start;

			public T[] sources;

			public Serializer ser;

			public void DoWork(JobCollection<K, T> mgr)
			{
				int num = start + sources.Length - 1;
				SaveLoadUtils.LogThread($"Saving {mgr.DebugName} {start}..{num}");
				Stopwatch stopwatch = Stopwatch.StartNew();
				int i = 0;
				for (int num2 = sources.Length; i < num2; i++)
				{
					T entry = sources[i];
					K index = mgr.GetIndex(entry);
					mgr.collector[index] = mgr.Serialize(entry, ser);
				}
				stopwatch.Stop();
				SaveLoadUtils.LogThread($"Saving {mgr.DebugName} {start}..{num} took {stopwatch.ElapsedMilliseconds} ms");
			}
		}

		public List<SaveJob> jobs;

		public ResultsCollector<K> collector;

		public abstract string DebugName { get; }

		public abstract K GetIndex(T entry);

		public abstract Hashtable Serialize(T entry, Serializer ser);

		public void RunAllJobs()
		{
			Stopwatch stopwatch = Stopwatch.StartNew();
			Parallel.ForEach(jobs, delegate(SaveJob job)
			{
				job.DoWork(this);
			});
			SaveLoadUtils.LogThread($"Saving {DebugName} (all) took {stopwatch.ElapsedMilliseconds} ms");
		}
	}

	public void Initialize()
	{
	}

	public void Release()
	{
	}

	public JOBS MakeParallelJobs<JOBS, K, T>(List<T> entries, int countPerJob) where JOBS : JobCollection<K, T>, new()
	{
		JOBS val = new JOBS
		{
			collector = new ResultsCollector<K>(),
			jobs = new List<JobCollection<K, T>.SaveJob>()
		};
		int i = 0;
		for (int count = entries.Count; i < count; i += countPerJob)
		{
			int num = Math.Min(countPerJob, count - i);
			T[] array = new T[num];
			entries.CopyTo(i, array, 0, num);
			val.jobs.Add(new JobCollection<K, T>.SaveJob
			{
				sources = array,
				start = i,
				ser = Game.serv.serializer.CloneInstance()
			});
		}
		return val;
	}
}

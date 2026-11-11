using System.Collections.Generic;
using System.Linq;

using Lumina.Excel.Sheets;

namespace HuntBuddy;

public class HuntRecommendationService {
	private readonly IReadOnlyDictionary<uint, uint> capByLevel;
	private readonly ClassJob[] classJobs;
	private readonly Dictionary<uint, HuntRecommendation> cache = new();
	private List<HuntJobLevel>? jobLevels;
	private uint currentJobId;

	public HuntRecommendationService() {
		this.capByLevel = Service.DataManager.GetExcelSheet<MobHuntRewardCap>()!
			.Where(row => row.RowId != 0)
			.ToDictionary(row => row.RowId, row => row.ExpCap);
		this.classJobs = Service.DataManager.GetExcelSheet<ClassJob>()!
			.Where(job => job is { JobIndex: > 0, JobType: >= 1 and <= 5, IsLimitedJob: false })
			.ToArray();
	}

	public void Refresh() {
		if (!Service.PlayerState.IsLoaded) {
			if (this.jobLevels != null) {
				this.jobLevels = null;
				this.currentJobId = 0;
				this.cache.Clear();
			}
			return;
		}

		uint currentJobId = Service.PlayerState.ClassJob.RowId;
		List<HuntJobLevel> levels = this.classJobs
			.Select(job => new HuntJobLevel(job, Service.PlayerState.GetClassJobLevel(job)))
			.Where(entry => entry.Level > 0)
			.ToList();
		if (this.currentJobId == currentJobId && this.jobLevels != null && SameLevels(this.jobLevels, levels)) {
			return;
		}

		this.currentJobId = currentJobId;
		this.jobLevels = levels;
		this.cache.Clear();
	}

	public HuntRecommendation Get(uint expReward) {
		if (this.jobLevels == null) {
			this.Refresh();
		}

		if (!this.cache.TryGetValue(expReward, out HuntRecommendation? recommendation)) {
			recommendation = HuntRecommendationCalculator.Create(expReward, this.capByLevel, this.jobLevels, this.currentJobId);
			this.cache[expReward] = recommendation;
		}
		return recommendation;
	}

	private static bool SameLevels(IReadOnlyCollection<HuntJobLevel> previous, IReadOnlyCollection<HuntJobLevel> current) {
		return previous.Count == current.Count && current.All(entry =>
			previous.Any(other => other.Job.RowId == entry.Job.RowId && other.Level == entry.Level));
	}
}

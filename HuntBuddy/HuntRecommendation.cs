using System.Collections.Generic;
using System.Linq;

using Lumina.Excel.Sheets;

namespace HuntBuddy;

public sealed record HuntJobLevel(ClassJob Job, int Level);

public enum HuntRecommendationKind {
	DpsNormal,
	DpsAboveThreshold,
	TankHealerNormal,
	TankHealerAboveThreshold,
	NotApplicable,
	DataUnavailable,
	NoValidThreshold,
	PlayerDataMissing,
	NoJobs,
	AllJobsMaxLevel,
}

public sealed record HuntRecommendation(uint? ThresholdLevel, HuntJobLevel? SelectedJob, HuntRecommendationKind Kind) {
	public bool IsAbnormal => this.SelectedJob != null && this.Kind != HuntRecommendationKind.DpsNormal;
}

public static class HuntRecommendationCalculator {
	public static HuntRecommendation Create(
		uint expReward,
		IReadOnlyDictionary<uint, uint>? capByLevel,
		IReadOnlyList<HuntJobLevel>? jobLevels,
		uint currentJobId) {
		if (expReward == 0) {
			return new HuntRecommendation(null, null, HuntRecommendationKind.NotApplicable);
		}

		if (capByLevel == null || capByLevel.Count == 0) {
			return new HuntRecommendation(null, null, HuntRecommendationKind.DataUnavailable);
		}

		uint? thresholdLevel = null;
		foreach (KeyValuePair<uint, uint> row in capByLevel.OrderBy(row => row.Key)) {
			if (row.Value <= expReward) {
				thresholdLevel = row.Key;
			}
		}

		if (thresholdLevel == null) {
			return new HuntRecommendation(null, null, HuntRecommendationKind.NoValidThreshold);
		}

		if (jobLevels == null) {
			return new HuntRecommendation(thresholdLevel, null, HuntRecommendationKind.PlayerDataMissing);
		}

		uint maxCombatLevel = capByLevel.Keys.Max();
		List<HuntJobLevel> combatJobs = jobLevels
			.Where(entry => entry is { Level: > 0, Job.JobIndex: > 0, Job.JobType: >= 1 and <= 5, Job.IsLimitedJob: false })
			.ToList();
		List<HuntJobLevel> dpsJobs = combatJobs.Where(entry => entry.Job.JobType is 3 or 4 or 5).ToList();
		List<HuntJobLevel> tankHealerJobs = combatJobs.Where(entry => entry.Job.JobType is 1 or 2).ToList();

		HuntGroupSelection? selection = SelectFromGroup(dpsJobs, thresholdLevel.Value, currentJobId, maxCombatLevel);
		if (selection != null) {
			return new HuntRecommendation(
				thresholdLevel,
				selection.SelectedJob,
				selection.IsAboveThreshold ? HuntRecommendationKind.DpsAboveThreshold : HuntRecommendationKind.DpsNormal);
		}

		selection = SelectFromGroup(tankHealerJobs, thresholdLevel.Value, currentJobId, maxCombatLevel);
		if (selection != null) {
			return new HuntRecommendation(
				thresholdLevel,
				selection.SelectedJob,
				selection.IsAboveThreshold ? HuntRecommendationKind.TankHealerAboveThreshold : HuntRecommendationKind.TankHealerNormal);
		}

		return combatJobs.Count == 0
			? new HuntRecommendation(thresholdLevel, null, HuntRecommendationKind.NoJobs)
			: new HuntRecommendation(thresholdLevel, null, HuntRecommendationKind.AllJobsMaxLevel);
	}

	private static HuntGroupSelection? SelectFromGroup(
		IReadOnlyCollection<HuntJobLevel> jobs,
		uint thresholdLevel,
		uint currentJobId,
		uint maxCombatLevel) {
		if (jobs.Count == 0 || jobs.All(entry => entry.Level >= maxCombatLevel)) {
			return null;
		}

		List<HuntJobLevel> eligible = jobs.Where(entry => entry.Level <= thresholdLevel).ToList();
		if (eligible.Count != 0) {
			int highestLevel = eligible.Max(entry => entry.Level);
			return new HuntGroupSelection(
				SelectWithStableTieBreak(eligible.Where(entry => entry.Level == highestLevel).ToList(), currentJobId),
				false);
		}

		int lowestLevel = jobs.Min(entry => entry.Level);
		return new HuntGroupSelection(
			SelectWithStableTieBreak(jobs.Where(entry => entry.Level == lowestLevel).ToList(), currentJobId),
			true);
	}

	private static HuntJobLevel SelectWithStableTieBreak(IReadOnlyCollection<HuntJobLevel> candidates, uint currentJobId) {
		HuntJobLevel? currentJob = candidates.FirstOrDefault(entry => entry.Job.RowId == currentJobId);
		return currentJob ?? candidates.OrderBy(entry => entry.Job.RowId).First();
	}

	private sealed record HuntGroupSelection(HuntJobLevel SelectedJob, bool IsAboveThreshold);
}

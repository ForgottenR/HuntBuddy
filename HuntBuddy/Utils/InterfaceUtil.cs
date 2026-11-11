using System.Collections.Generic;
using System.Linq;
using System.Numerics;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Textures;

namespace HuntBuddy.Utils;

/// <summary>
/// Interface utilities class.
/// </summary>
public static class InterfaceUtil {
	/// <summary>
	/// Draws hunt icons from game images.
	/// </summary>
	/// <param name="mobHuntEntry"><see cref="MobHuntEntry"/> containing relevant information.</param>
	public static void DrawHuntIcon(MobHuntEntry mobHuntEntry) {
		Vector2 cursorPos = ImGui.GetCursorScreenPos();
		Vector2 imageSize = mobHuntEntry.ExpansionId < 3 ? new Vector2(192f, 128f) : new Vector2(210f);
		imageSize *= ImGui.GetIO().FontGlobalScale * Plugin.Instance.Configuration.IconScale;

		ImGui.InvisibleButton("canvas", imageSize);

		ImDrawListPtr drawList = ImGui.GetWindowDrawList();
		if (mobHuntEntry is { ExpansionId: 4, IsEliteMark: false }) // Endwalker uses circle for non elite mobs
		{
			drawList.AddCircleFilled(
				cursorPos + (imageSize / 2f),
				imageSize.X / 2f,
				Plugin.Instance.Configuration.IconBackgroundColourU32);
		}
		else {
			drawList.AddRectFilled(
				cursorPos,
				cursorPos + imageSize,
				Plugin.Instance.Configuration.IconBackgroundColourU32);
		}

		drawList.AddImage(LoadIcon(mobHuntEntry.Icon).Handle, cursorPos, cursorPos + imageSize);
	}

	public static string GetBillLabel(MobHuntEntry mobHuntEntry) {
		return (BillEnum)mobHuntEntry.BillNumber switch {
			BillEnum.ArrRank1 => "2.0 初级",
			BillEnum.HwRank1 => "3.0 初级",
			BillEnum.HwRank2 => "3.0 中级",
			BillEnum.HwRank3 => "3.0 高级",
			BillEnum.SbRank1 => "4.0 初级",
			BillEnum.SbRank2 => "4.0 中级",
			BillEnum.SbRank3 => "4.0 高级",
			BillEnum.ShbRank1 => "5.0 初级",
			BillEnum.ShbRank2 => "5.0 中级",
			BillEnum.ShbRank3 => "5.0 高级",
			BillEnum.EwRank1 => "6.0 初级",
			BillEnum.EwRank2 => "6.0 中级",
			BillEnum.EwRank3 => "6.0 高级",
			BillEnum.DtRank1 => "7.0 初级",
			BillEnum.DtRank2 => "7.0 中级",
			BillEnum.DtRank3 => "7.0 高级",
			BillEnum.ArrElite => "2.0 精英",
			BillEnum.HwElite => "3.0 精英",
			BillEnum.SbElite => "4.0 精英",
			BillEnum.ShbElite => "5.0 精英",
			BillEnum.EwElite => "6.0 精英",
			BillEnum.DtElite => "7.0 精英",
			_ => "未知狩猎令",
		};
	}

	public static void DrawHuntReward(MobHuntEntry mobHuntEntry) {
		if (mobHuntEntry.ExpReward == 0) {
			ImGui.Text("无经验奖励");
			return;
		}

		HuntRecommendation recommendation = Plugin.Instance.RecommendationService.Get(mobHuntEntry.ExpReward);
		ImGui.Text(recommendation.ThresholdLevel is uint thresholdLevel
			? $"{mobHuntEntry.ExpReward:N0} EXP · ≤{thresholdLevel}级"
			: "无法确定经验截止等级");
	}

	public static void DrawHuntRecommendation(IEnumerable<MobHuntEntry> mobHuntEntries) {
		List<MobHuntEntry> entries = mobHuntEntries.ToList();
		uint expReward = entries.Where(entry => entry.ExpReward > 0).Select(entry => entry.ExpReward).DefaultIfEmpty(0u).Min();
		HuntRecommendation recommendation = Plugin.Instance.RecommendationService.Get(expReward);
		string text = FormatRecommendation(recommendation, entries.Count > 1);
		if (recommendation.IsAbnormal) {
			ImGui.TextColored(new Vector4(1f, 0.65f, 0.2f, 1f), text);
		}
		else {
			ImGui.Text(text);
		}
	}

	private static string FormatRecommendation(HuntRecommendation recommendation, bool useMinimum) {
		string prefix = useMinimum ? "多令取最低 · " : "";
		if (recommendation.SelectedJob == null) {
			return prefix + (recommendation.Kind switch {
				HuntRecommendationKind.NotApplicable => "无经验推荐",
				HuntRecommendationKind.DataUnavailable => "狩猎经验数据不可用",
				HuntRecommendationKind.NoValidThreshold => "无法确定经验截止等级",
				HuntRecommendationKind.PlayerDataMissing => "职业数据不可用",
				HuntRecommendationKind.NoJobs => "无可推荐职业",
				HuntRecommendationKind.AllJobsMaxLevel => "全部战斗职业已满级",
				_ => "无推荐",
			});
		}

		HuntJobLevel selectedJob = recommendation.SelectedJob!;
		string jobName = selectedJob.Job.Name.ToString();
		return prefix + (recommendation.Kind switch {
			HuntRecommendationKind.DpsNormal =>
				$"≤{recommendation.ThresholdLevel}级 · 推荐{jobName} {selectedJob.Level}级",
			HuntRecommendationKind.DpsAboveThreshold =>
				$"异常：无≤{recommendation.ThresholdLevel}级 DPS；推荐{jobName} {selectedJob.Level}级（最低级 DPS 兜底）",
			HuntRecommendationKind.TankHealerNormal =>
				$"异常：DPS 均已满级；推荐{jobName} {selectedJob.Level}级（T/奶）",
			HuntRecommendationKind.TankHealerAboveThreshold =>
				$"异常：DPS 已满级且无≤{recommendation.ThresholdLevel}级 T/奶；推荐{jobName} {selectedJob.Level}级（T/奶兜底）",
			_ => "无推荐",
		});
	}

	/// <summary>
	/// Returns a IDalamudTextureWrap for an icon.
	/// Will request a Hi-Res version and fallback to normal resolution otherwise.
	/// </summary>
	/// <param name="id">Icon ID</param>
	/// <returns>IDalamudTextureWrap for the icon, or an empty wrap if the icon is not found.</returns>
	private static IDalamudTextureWrap LoadIcon(uint id) {
		if (Service.TextureProvider.TryGetFromGameIcon(new GameIconLookup {
			IconId = id,
			HiRes = true,
		}, out var texture)) {
			return texture.GetWrapOrEmpty();
		}
		return Service.TextureProvider.GetFromGameIcon(new GameIconLookup {
			IconId = id,
			HiRes = false,
		}).GetWrapOrEmpty();
	}

	/// <summary>
	/// Renders a button with an icon.
	/// </summary>
	/// <param name="icon">Desired <see cref="FontAwesomeIcon"/> to be rendered.</param>
	/// <param name="id">Button ID.</param>
	/// <returns>True if pressed.</returns>
	public static bool IconButton(FontAwesomeIcon icon, string? id) {
		ImGui.PushFont(UiBuilder.IconFont);

		string? text = icon.ToIconString();

		if (id != null) {
			text += $"##{id}";
		}

		bool result = ImGui.Button(text);

		ImGui.PopFont();

		return result;
	}

	/// <summary>
	/// Renders a button with an icon.
	/// </summary>
	/// <param name="icon">Desired <see cref="FontAwesomeIcon"/> to be rendered.</param>
	/// <returns>True if pressed.</returns>
	public static bool IconButton(FontAwesomeIcon icon) => IconButton(icon, null);

	/// <summary>
	/// Draws horizontally-centered text in an ImGui window.
	/// </summary>
	/// <param name="text">The text to draw. Should be a single line.</param>
	// I hate centering text in ImGui
	public static void DrawCenteredText(string text) {
		float width = ImGui.CalcTextSize(text).X;
		float space = ImGui.GetContentRegionAvail().X;
		ImGui.SetCursorPosX((space / 2) - (width / 2));
		ImGui.Text(text);
	}

	/// <summary>
	/// Draw a tooltip with simple text content, using the specified text wrapping position.
	/// </summary>
	/// <param name="maxWidth">The textwrap position for the tooltip text.</param>
	/// <param name="text">The text of the tooltip.</param>
	public static void DrawWrappedTooltip(float maxWidth, string text) {
		ImGui.BeginTooltip();
		ImGui.PushTextWrapPos(maxWidth);
		ImGui.Text(text);
		ImGui.PopTextWrapPos();
		ImGui.EndTooltip();
	}
}

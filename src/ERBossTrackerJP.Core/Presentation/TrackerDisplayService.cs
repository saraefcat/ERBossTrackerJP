using ERBossTrackerJP.Core.Models;

namespace ERBossTrackerJP.Core.Presentation;

public sealed class TrackerDisplayService
{
    public TrackerDisplayModel Create(
        TrackerSnapshot snapshot,
        BossListFilter filter)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(filter);
        ValidateFilter(filter);

        string searchText = filter.SearchText?.Trim() ?? string.Empty;
        var bosses = new List<BossListItem>(snapshot.Bosses.Count);

        foreach (BossProgress progress in snapshot.Bosses)
        {
            if (!MatchesCompletion(progress, filter.Completion) ||
                filter.RegionId is not null &&
                !string.Equals(
                    progress.Boss.RegionId,
                    filter.RegionId,
                    StringComparison.Ordinal) ||
                filter.Content.HasValue && progress.Boss.Content != filter.Content.Value)
            {
                continue;
            }

            string name = GetText(
                filter.Language,
                progress.Boss.NameJa,
                progress.Boss.NameEn);

            if (searchText.Length > 0 &&
                !name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bosses.Add(new BossListItem(
                progress.Boss.Id,
                progress.Boss.FlagId,
                progress.IsDefeated,
                name,
                progress.Boss.RegionId,
                GetText(
                    filter.Language,
                    progress.Boss.RegionJa,
                    progress.Boss.RegionEn),
                GetText(
                    filter.Language,
                    progress.Boss.LocationJa,
                    progress.Boss.LocationEn),
                progress.Boss.Content,
                progress.Boss.SortOrder));
        }

        RegionListItem[] regions = snapshot.Regions
            .Select(region => new RegionListItem(
                region.RegionId,
                GetText(filter.Language, region.RegionJa, region.RegionEn),
                region.Defeated,
                region.Total))
            .ToArray();

        return new TrackerDisplayModel(snapshot, bosses, regions);
    }

    private static bool MatchesCompletion(
        BossProgress progress,
        BossCompletionFilter completion) => completion switch
        {
            BossCompletionFilter.All => true,
            BossCompletionFilter.Defeated => progress.IsDefeated,
            BossCompletionFilter.Undefeated => !progress.IsDefeated,
            _ => throw new ArgumentOutOfRangeException(nameof(completion), completion, null),
        };

    private static string GetText(
        DisplayLanguage language,
        string japanese,
        string english) => language switch
        {
            DisplayLanguage.Japanese => japanese,
            DisplayLanguage.English => english,
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
        };

    private static void ValidateFilter(BossListFilter filter)
    {
        if (!Enum.IsDefined(filter.Language))
        {
            throw new ArgumentOutOfRangeException(
                nameof(filter),
                filter.Language,
                "The display language is not supported.");
        }

        if (!Enum.IsDefined(filter.Completion))
        {
            throw new ArgumentOutOfRangeException(
                nameof(filter),
                filter.Completion,
                "The completion filter is not supported.");
        }

        if (filter.Content.HasValue && !Enum.IsDefined(filter.Content.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(filter),
                filter.Content,
                "The game content filter is not supported.");
        }

        if (filter.RegionId is not null &&
            string.IsNullOrWhiteSpace(filter.RegionId))
        {
            throw new ArgumentException(
                "Region ID must be null or non-blank.",
                nameof(filter));
        }
    }
}

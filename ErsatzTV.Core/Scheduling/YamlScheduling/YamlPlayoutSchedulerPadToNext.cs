using ErsatzTV.Core.Domain;
using ErsatzTV.Core.Interfaces.Scheduling;

namespace ErsatzTV.Core.Scheduling.YamlScheduling;

public class YamlPlayoutSchedulerPadToNext : YamlPlayoutSchedulerDuration
{
    public static DateTimeOffset Schedule(
        Playout playout,
        DateTimeOffset currentTime,
        int guideGroup,
        YamlPlayoutPadToNextInstruction padToNext,
        IMediaCollectionEnumerator enumerator,
        Option<IMediaCollectionEnumerator> fallbackEnumerator)
    {
        int currentMinute = currentTime.Minute;

        int targetMinute = (currentMinute + padToNext.PadToNext - 1) / padToNext.PadToNext * padToNext.PadToNext;

        DateTimeOffset almostTargetTime =
            currentTime - TimeSpan.FromMinutes(currentMinute) + TimeSpan.FromMinutes(targetMinute);

        var targetTime = new DateTimeOffset(
            almostTargetTime.Year,
            almostTargetTime.Month,
            almostTargetTime.Day,
            almostTargetTime.Hour,
            almostTargetTime.Minute,
            0,
            almostTargetTime.Offset);

        // ensure filler works for content less than one minute
        if (targetTime <= currentTime)
            targetTime = targetTime.AddMinutes(padToNext.PadToNext);

        return Schedule(
            playout,
            currentTime,
            targetTime,
            padToNext.DiscardAttempts,
            padToNext.Trim,
            GetFillerKind(padToNext),
            guideGroup,
            enumerator,
            fallbackEnumerator);
    }
}

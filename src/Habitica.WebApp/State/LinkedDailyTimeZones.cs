using Habitica.Domain.Workspace;
using System.Globalization;
namespace Habitica.WebApp.State;

public static class LinkedDailyTimeZones
{
    public static string Label(string zone)
    {
        if (DailyProjectionSchedule.FixedOffset(zone) is not {} west) return "Retained unsupported zone";
        return $"UTC{(west > 0 ? "−" : "+")}{(Math.Abs(west) / 60).ToString("00", CultureInfo.InvariantCulture)}:00";
    }
    public static bool AccountSupported(DailyAccountSchedule account) => account.DayStart == 0
        && DailyProjectionSchedule.FixedTimeZoneIds.Any(zone => DailyProjectionSchedule.FixedOffset(zone) == account.TimezoneOffset);
    public static string? Failure(string zone, DailyAccountSchedule? account)
    {
        if (account is null) return null;
        if (!AccountSupported(account)) return "Linked Dailies require a supported whole-hour UTC offset and a midnight Habitica day start. Current account settings are unsupported; the draft is preserved.";
        if (DailyProjectionSchedule.FixedOffset(zone) != account.TimezoneOffset) return "Choose the fixed UTC offset matching the observed Habitica account. The draft has not been changed automatically.";
        return null;
    }
}

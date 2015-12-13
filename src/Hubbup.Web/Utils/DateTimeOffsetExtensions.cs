using System;

namespace Hubbup.Web.Utils
{
    public static class DateTimeOffsetExtensions
    {
        private static readonly TimeZoneInfo PacificTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

        public static DateTimeOffset ToPacificTime(this DateTimeOffset utcDateTime)
        {
            return TimeZoneInfo.ConvertTime(utcDateTime, PacificTimeZone);
        }

        public static string ToTimeAgo(this DateTimeOffset date)
        {
            var localDate = date.ToPacificTime();
            var localNow = DateTimeOffset.UtcNow.ToPacificTime();

            var daysAgo = (int)Math.Floor((localNow - localDate).TotalDays);
            if (daysAgo == 0)
            {
                var hoursAgo = (int)Math.Floor((localNow - localDate).TotalHours);
                if (hoursAgo == 0)
                {
                    var minutesAgo = (int)Math.Floor((localNow - localDate).TotalMinutes);
                    if (minutesAgo == 0)
                    {
                        return "just now!";
                    }
                    else if (minutesAgo == 1)
                    {
                        return "1 minute ago";
                    }
                    else
                    {
                        return string.Format("{0} minutes ago", minutesAgo);
                    }
                }
                else if (hoursAgo == 1)
                {
                    return "1 hour ago";
                }
                else
                {
                    return string.Format("{0} hours ago", hoursAgo);
                }
            }
            else if (daysAgo == 1)
            {
                return "1 day ago";
            }
            else
            {
                return string.Format("{0} days ago", daysAgo);
            }
        }
    }
}

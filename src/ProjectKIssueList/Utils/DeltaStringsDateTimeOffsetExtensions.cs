using System;

namespace ProjectKIssueList.Utils
{
    public static class DeltaStringsDateTimeOffsetExtensions
    {
        public static string ToDaysAgo(this DateTimeOffset date)
        {
            var daysAgo = (int)Math.Floor((DateTimeOffset.UtcNow - date).TotalDays);
            if (daysAgo == 1)
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

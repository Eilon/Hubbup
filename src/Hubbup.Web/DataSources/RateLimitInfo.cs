using System;

namespace Hubbup.Web.DataSources
{
    public class RateLimitInfo
    {
        public int Limit { get; set; }
        public int Remaining { get; set; }
        public int Cost { get; set; }
        public DateTime ResetAt { get; set; }

        public static RateLimitInfo Add(RateLimitInfo current, RateLimitInfo next) => new RateLimitInfo()
        {
            Limit = next.Limit,
            Remaining = next.Remaining,
            Cost = current.Cost + next.Cost,
            ResetAt = next.ResetAt
        };
    }
}

namespace Hubbup.Web.DataSources
{
    public class SearchResults<T>
    {
        public RateLimitInfo RateLimit { get; set; }

        public T Search { get; set; }

        public SearchResults()
        {

        }

        public SearchResults(T search, RateLimitInfo rateLimit)
        {
            Search = search;
            RateLimit = rateLimit;
        }
    }
}

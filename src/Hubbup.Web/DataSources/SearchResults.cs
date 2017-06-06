namespace Hubbup.Web.DataSources
{
    public class SearchResults<T>
    {
        public RateLimitInfo RateLimit { get; set; }

        public int Pages { get; set; }

        public T Search { get; set; }

        public SearchResults()
        {

        }

        public SearchResults(T search, RateLimitInfo rateLimit, int pages)
        {
            Search = search;
            RateLimit = rateLimit;
            Pages = pages;
        }
    }
}

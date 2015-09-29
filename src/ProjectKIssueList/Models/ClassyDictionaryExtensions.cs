using System;
using System.Collections.Generic;

namespace ProjectKIssueList.Models
{
    public static class ClassyDictionaryExtensions
    {
        // Submitted this proposed API to CLR here: https://github.com/dotnet/corefx/issues/3482
        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException(nameof(dictionary));
            }
            TValue value;
            dictionary.TryGetValue(key, out value);
            return value;
        }

        public static TValue GetValueOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue)
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException(nameof(dictionary));
            }
            TValue value;
            return dictionary.TryGetValue(key, out value) ? value : defaultValue;
        }
    }
}

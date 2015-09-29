using System;
using System.Collections.Generic;

namespace ProjectKIssueList.Models
{
    public static class ClassyDictionaryExtensions
    {
        // Submitted this issue to CLR here: https://github.com/dotnet/corefx/issues/3482
        public static TValue GetValueNoThrow<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
            where TValue : class
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException(nameof(dictionary));
            }
            TValue value;
            dictionary.TryGetValue(key, out value);
            return value;
        }
    }
}

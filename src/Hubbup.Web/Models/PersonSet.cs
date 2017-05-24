using System;
using System.Collections.Generic;

namespace Hubbup.Web.Models
{
    public class PersonSet
    {
        public static readonly PersonSet Empty = new PersonSet(Array.Empty<string>());

        public IReadOnlyList<string> People { get; }

        public PersonSet(IReadOnlyList<string> people)
        {
            People = people;
        }
    }
}

using System.Collections.Generic;

namespace ProjectKIssueList.Models
{
    public class StaticPersonSetProvider : IPersonSetProvider
    {
        private static readonly Dictionary<string, PersonSet> PersonSetList = new Dictionary<string, PersonSet>
        {
            {
                "Eilon",
                new PersonSet {
                    People = new string[]
                    {
                        "ajaybhargavb",
                        "dougbu",
                        "Eilon",
                        "kichalla",
                        "kirthik",
                        "NTaylorMullen",
                        "pranavkm",
                        "rynowak",
                    }
                }
            },
            {
                "Murat",
                new PersonSet {
                    People = new string[]
                    {
                        "anurse",
                        "BrennanConroy",
                        "cesarbs",
                        "davidfowl",
                        "halter73",
                        "JunTaoLuo",
                        "loudej",
                        "lodejard",
                        "moozzyk",
                        "muratg",
                        "Tratcher",
                        "troydai",
                        "victorhurdugaci",
                    }
                }
            },
            {
                "Diego",
                new PersonSet {
                    People = new string[]
                    {
                        "ajcvickers",
                        "AndriySvyryd",
                        "anpete",
                        "bricelam",
                        "divega",
                        "HaoK",
                        "lajones",
                        "maumar",
                        "mikary",
                        "natemcmaster",
                        "rowanmiller",
                        "smitpatel",
                    }
                }
            },
        };

        public PersonSet GetPersonSet(string personSetName)
        {
            return PersonSetList.GetValueOrDefault(personSetName);
        }
    }
}

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
            {
                "perfers",
                new PersonSet {
                    People = new string[]
                    {
                        "davidfowl",
                        "halter73",
                        "loudej",
                        "lodejard",
                        "rynowak",
                        "Tratcher",
                    }
                }
            },
            {
                "Docs",
                new PersonSet {
                    People = new string[]
                    {
                        "ardalis",
                        "danroth27",
                        "Erikre",
                        "Rick-Anderson",
                        "TomArcher",
                    }
                }
            },
            {
                "NuGet",
                new PersonSet {
                    People = new string[]
                    {
                        "danliu",
                        "deepakaravindr",
                        "emgarten",
                        "feiling",
                        "johntaylor",
                        "MeniZalzman",
                        "RanjiniM",
                        "xavierdecoster",
                        "yishaigalatzer",
                        "zhili1208",
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

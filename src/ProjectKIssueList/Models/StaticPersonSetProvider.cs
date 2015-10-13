using System.Collections.Generic;
using ProjectKIssueList.Utils;

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
                        "ryanbrandenburg",
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
                        "pakrym",
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
            {
                "corefx",
                new PersonSet {
                    People = new string[]
                    {
                        "adityamandaleeka",
                        "agocke",
                        "ahsonkhan",
                        "AlekseyTs",
                        "AlexGhiondea",
                        "alexperovich",
                        "AlfredoMS",
                        "AnthonyDGreen",
                        "AtsushiKan",
                        "bartonjs",
                        "bleroy",
                        "brthor",
                        "ChadNedzlek",
                        "chcosta",
                        "Chrisboh",
                        "CIPop",
                        "crummel",
                        "dagood",
                        "davidsh",
                        "davkean",
                        "davmason",
                        "dleapon",
                        "dsplaisted",
                        "eerhardt",
                        "ellismg",
                        "ericstj",
                        "FiveTimesTheFun",
                        "gafter",
                        "ianhays",
                        "jaredpar",
                        "JeremyKuhne",
                        "jhendrixMSFT",
                        "Jinhuafei",
                        "joperezr",
                        "josguil",
                        "joshfree",
                        "kapilash",
                        "karelz",
                        "khdang",
                        "kkurni",
                        "krwq",
                        "KrzysztofCwalina",
                        "Maoni0",
                        "maririos",
                        "markwilkie",
                        "MattGal",
                        "MattWhilden",
                        "mellinoe",
                        "mmitche",
                        "msftqingye",
                        "naamunds",
                        "nadiatk",
                        "nguerrera",
                        "pallavit",
                        "Petermarcu",
                        "piotrpMSFT",
                        "Priya91",
                        "rajansingh10",
                        "ramarag",
                        "richlander",
                        "saurabh500",
                        "SGuyGe",
                        "shmao",
                        "shrutigarg",
                        "SidharthNabar",
                        "sokket",
                        "stephentoub",
                        "steveharter",
                        "tarekgh",
                        "terrajobst",
                        "theoy",
                        "tmat",
                        "vancem",
                        "venkat-raman251",
                        "vijaykota",
                        "VSadov",
                        "weshaggard",
                        "YoungGah",
                        "zhenlan",
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

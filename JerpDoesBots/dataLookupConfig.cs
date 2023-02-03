using System.Collections.Generic;

namespace JerpDoesBots
{

    class dataLookupConfigCatalog
    {
        public string code { get; set; }
        public string displayName { get; set; }
        public string outputStringMatch { get; set; }
        public string outputStringBelow { get; set; }
        public string outputStringAbove { get; set; }
        public string outputStringBetween { get; set; }
        public bool isNumeric { get; set; }
        public int numericIndexOffset { get; set; }
        public List<float> numericEntries { get; set; }
        public Dictionary<string, string> entries { get; set; }

        public dataLookupConfigCatalog()
        {
            isNumeric = false;
            entries = new Dictionary<string, string>();
            numericEntries = new List<float>();
            numericIndexOffset = 0;
        }
    }

    class dataLookupConfig
    {
        public Dictionary<string, dataLookupConfigCatalog> entries { get; set; }

        public dataLookupConfig()
        {
            entries = new Dictionary<string, dataLookupConfigCatalog>();
        }
    }
}

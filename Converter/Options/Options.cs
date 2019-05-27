using System.Collections.Generic;

namespace Converter.Options
{
    public class Options
    {
        public enum IndexDecision
        {
            Hash = 0,
            Range = 1,
            ExtendedPropery = 2
        }
        public bool CopyData { get; set; } = true;
        public IndexDecision UseHashIndexes { get; set; } = IndexDecision.Hash;
/*
        public bool DropOnDestination { get; set; } = false;
*/
        public List<string> Schemas { get; set; } = new List<string>();
        public List<string> Tables { get; set; } = new List<string>();
    }
}

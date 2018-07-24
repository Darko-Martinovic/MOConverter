

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
        public bool DropOnDestination { get; set; } = false;
        public string SchemaContains { get; set; } = string.Empty;
        public string TableContains { get; set; } = string.Empty;
    }
}

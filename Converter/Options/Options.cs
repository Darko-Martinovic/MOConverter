
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


        private bool mCopyData = true;
        public bool CopyData
        {
            get { return mCopyData; }
            set { mCopyData = value; }
        }


        private IndexDecision mUseHashIndexes = IndexDecision.Hash;
        public IndexDecision UseHashIndexes
        {
            get { return mUseHashIndexes; }
            set { mUseHashIndexes = value; }
        }


        private bool mDropOnDestination = false;
        public bool DropOnDestination
        {
            get { return mDropOnDestination; }
            set { mDropOnDestination = value; }
        }


        private string mSchemaContains = string.Empty;
        public string SchemaContains
        {
            get { return mSchemaContains; }
            set { mSchemaContains = value; }
        }

        private string mTableContains = string.Empty;
        public string TableContains
        {
            get { return mTableContains; }
            set { mTableContains = value; }
        }



    }
}

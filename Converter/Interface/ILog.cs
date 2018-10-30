namespace Converter.Interface
{
    public interface ILog
    {
        void Log(string text, string description);
        void LogWarErr(string text, string description);
        void SetValue(int value);
        void SetMaxValue(int value);
        void SetOverall(string text);
        
        int CurrentItem
        {
            get;
            set;
        }
        int Counter
        {
            get;
            set;
        }


    }
}

using JRPG.Core;

namespace JRPG.Save
{
    public interface ISaveable
    {
        string SaveKey { get; }
        SaveDataBase CaptureState();
        void RestoreState(SaveDataBase state);
    }
}

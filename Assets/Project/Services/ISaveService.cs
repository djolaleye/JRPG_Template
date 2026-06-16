namespace JRPG.Services
{
    public interface ISaveService
    {
        bool CanSave();
        bool Save(int slot);
        bool Load(int slot);
    }
}

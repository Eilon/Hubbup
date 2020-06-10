namespace Hubbup.MikLabelModel
{
    public interface IMikLabelerPathProvider
    {
        (string issuePath, string prPath) GetModelPath();
    }
}

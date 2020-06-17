namespace Hubbup.MikLabelModel
{
    public class MikLabelerStringPathProvider : IMikLabelerPathProvider
    {
        private readonly string _path;
        private readonly string _prPath;

        public MikLabelerStringPathProvider(string issuePath, string prPath)
        {
            _path = issuePath;
            _prPath = prPath;
        }

        (string issuePath, string prPath) IMikLabelerPathProvider.GetModelPath()
        {
            return (_path, _prPath);
        }
    }
}

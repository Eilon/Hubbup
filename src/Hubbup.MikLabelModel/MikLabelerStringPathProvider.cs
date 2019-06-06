namespace Hubbup.MikLabelModel
{
    public class MikLabelerStringPathProvider : IMikLabelerPathProvider
    {
        private readonly string _path;

        public MikLabelerStringPathProvider(string path)
        {
            _path = path;
        }

        string IMikLabelerPathProvider.GetModelPath()
        {
            return _path;
        }
    }
}

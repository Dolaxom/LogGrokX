namespace LogGrokX
{
    public interface IThreadGroupedItem
    {
        bool HasSameThread(IThreadGroupedItem? other, int threadFieldIndex);
    }
}
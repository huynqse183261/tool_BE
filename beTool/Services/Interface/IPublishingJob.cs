namespace Services.Interface
{
    public interface IPublishingJob
    {
        Task ExecuteAsync(int postId);
    }
}
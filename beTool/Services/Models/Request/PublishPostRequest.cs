namespace Services.Models.Request
{
    public class PublishPostRequest
    {
        /// <summary>
        /// Final caption after user review. If empty, the service uses the caption saved in the post.
        /// </summary>
        public string? Caption { get; set; }
    }
}

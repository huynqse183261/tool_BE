namespace Services.Models.AI
{
    public class AiClassificationResult
    {
        public string? ContentType { get; set; }
        public string? MoodType { get; set; }
        public string? MainSubject { get; set; }
        public string? Environment { get; set; }
    }
}

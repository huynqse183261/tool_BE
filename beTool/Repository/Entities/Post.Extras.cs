// NOTE: This file extends the EF Core Power Tools generated Post entity.
// Keep changes here to avoid losing them when regenerating.
#nullable disable
namespace Repositories.Entities;

public partial class Post
{
    // Columns exist in the DB schema (see DB.sql) but may not be present in the generated file.
    public string AiTitle { get; set; }
    public string Hashtags { get; set; }
    public string ContentType { get; set; }
    public string SceneType { get; set; }
    public string MoodType { get; set; }

    public bool? EditedByUser { get; set; }
    public int? GenerationVersion { get; set; }
    public string FacebookPostId { get; set; }
}

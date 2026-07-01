using Services.Models.Enums;

namespace Services.Interface
{
    public interface IPromptTemplateService
    {
        string GetTemplate(ContentType type);
    }
}

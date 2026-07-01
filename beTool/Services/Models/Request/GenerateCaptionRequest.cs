using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Models.Request
{
    public class GenerateCaptionRequest
    {
        // ContentType: quyết định STRUCTURE bài viết (ProductShowcase/Storytelling/EventRecap)
        public string? ContentType { get; set; }

        // SceneType: quyết định SCENE STYLE / atmosphere / visual mood (IndustrialGarage/JapaneseStreet/...)
        // (rename từ templateType để tránh hiểu nhầm)
        public string? SceneType { get; set; }


        // User inputs are intentionally NOT used for caption generation.
        // Keep fields for backward compatibility.
        public string? Sku { get; set; }
        public string? ProductName { get; set; }
        public string? Scale { get; set; }
        public string? EventName { get; set; }
        public string? EventDate { get; set; }

        public string EffectiveContentType =>
            !string.IsNullOrWhiteSpace(ContentType) ? ContentType! : "";

        public string EffectiveSceneType =>
            !string.IsNullOrWhiteSpace(SceneType) ? SceneType! : "";

    }
}

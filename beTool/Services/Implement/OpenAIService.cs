using Microsoft.Extensions.Configuration;
using Services.Interface;
using Services.Models.AI;
using Services.Models.Enums;
using Services.Models.Request;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Services.Implement
{
    /// <summary>
    /// Flow: Images -> Detect ContentType -> Enum Mapping -> Prompt Template -> Generate Caption
    /// </summary>
    public class OpenAIService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly IPromptTemplateService _promptTemplateService;

        // Forbidden vocabulary (expanded per brand rules)
        private static readonly string[] ForbiddenVocabulary = new[]
        {
            "món đồ chơi", "tuyệt vời", "độc đáo", "tác phẩm nghệ thuật", "lý tưởng cho",
            "sản phẩm chất lượng", "hoàn hảo cho", "đẳng cấp", "ấn tượng mạnh",
            "mô hình độc đáo", "chi tiết sống động"
        };

        // Preferred vocabulary (expanded per brand rules)
        private static readonly string[] PreferredVocabulary = new[]
        {
            "industrial garage", "workshop", "display setup", "background", "collector shelf",
            "atmosphere", "cinematic", "cinematic lighting", "JDM", "street build", "góc display"
        };

        public OpenAIService(HttpClient httpClient, IConfiguration configuration, IPromptTemplateService promptTemplateService)
        {
            _httpClient = httpClient;
            _promptTemplateService = promptTemplateService;

            var apiKey = configuration["OpenAI:ApiKey"];
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
        }

        public async Task<(string caption, string analysis, string contentType, string tone, string? title, List<string>? hashtags)>
            GenerateBrandAwareCaptionAsync(List<string> imageUrls, string templateType, string? brandVoice, GenerateCaptionRequest request)
        {
            var imageParts = BuildImageContentParts(imageUrls);

            // NOTE: templateType is ContentType (structure) selected by FE.
            // Scene style will be injected via request.SceneType.
            var mappedType = MapToContentTypeEnum(templateType);

            var template = _promptTemplateService.GetTemplate(mappedType);

            // IMPORTANT: captions are generated ONLY from images.
            // Scene style is injected via request.SceneType.
            var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var sceneType = request.EffectiveSceneType;
            var sceneStyleKeywords = BuildSceneStyleKeywords(sceneType);

            // Populate vars from request fields + AI extraction (must run before creative generation)
            var extractedVars = await ExtractTemplateVariablesAsync(mappedType, imageParts, request);
            foreach (var kv in extractedVars)
                vars[kv.Key] = kv.Value;

            // Ask AI to produce simple creative JSON (HOOK/BODY/TITLE etc.); backend will render final caption.
            var creative = await GenerateCreativeJsonAsync(
                mappedType,
                brandVoice,
                template,
                imageParts,
                vars,
                toneHint: null,
                sceneStyle: sceneStyleKeywords
            );





            // Backend render final caption deterministically
            var finalCaption = RenderFinalCaption(mappedType, vars, creative);

            // Sanitize and validate output
            finalCaption = SanitizeForbiddenVocabulary(finalCaption);
            if (!ValidateCaptionOutput(finalCaption, mappedType, out var errors))
            {
                throw new InvalidOperationException("Caption validation failed: " + string.Join("; ", errors));
            }

            // Build analysis: include variables and creative JSON to allow regenerate
            var analysisObj = new { contentType = mappedType.ToString(), variables = vars, creative = creative };
            var analysis = JsonSerializer.Serialize(analysisObj);

            // tone/title/hashtags come from creative if present
            var tone = creative.TryGetValue("MOOD", out var m) ? m : "general";
            var title = creative.TryGetValue("TITLE", out var t) ? t : null;
            List<string>? hashtags = null;
            if (creative.TryGetValue("HASHTAGS", out var hs) && !string.IsNullOrWhiteSpace(hs))
            {
                hashtags = new List<string>(hs.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            }

            return (finalCaption, analysis, mappedType.ToString(), tone, title, hashtags);
        }

        public async Task<string> RegenerateCaptionAsync(string previousAnalysis, List<string> imageUrls, string? newPrompt)
        {
            // Expect previousAnalysis to contain serialized variables and template type.
            Dictionary<string, string>? variables = null;
            ContentType? templateType = null;
            Dictionary<string, string>? creative = null;

            try
            {
                using var doc = JsonDocument.Parse(previousAnalysis);
                var root = doc.RootElement;

                // variables
                if (root.TryGetProperty("variables", out var vars) && vars.ValueKind == JsonValueKind.Object)
                {
                    variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var prop in vars.EnumerateObject())
                        variables[prop.Name] = prop.Value.GetString() ?? "";
                }

                // creative (previous HOOK/BODY/TITLE)
                if (root.TryGetProperty("creative", out var cr) && cr.ValueKind == JsonValueKind.Object)
                {
                    creative = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var prop in cr.EnumerateObject())
                        creative[prop.Name] = prop.Value.GetString() ?? "";
                }

                // contentType
                if (root.TryGetProperty("contentType", out var ct) && ct.ValueKind == JsonValueKind.String)
                {
                    if (Enum.TryParse(ct.GetString(), true, out ContentType parsed)) templateType = parsed;
                }
            }
            catch
            {
                // ignore parse errors
            }

            if (variables == null || variables.Count == 0 || templateType == null)
            {
                throw new InvalidOperationException("Regenerate requires previous analysis that includes 'variables' and 'contentType'. Provide SKU/PRODUCT_NAME in the previousAnalysis or via frontend/DB.");
            }

            if (!ValidateTemplateVariables(templateType.Value, variables, out var missing))
            {
                throw new InvalidOperationException($"Cannot regenerate: missing variables {string.Join(',', missing)}. Do not let AI invent SKU/PRODUCT_NAME.");
            }

            // We will prompt AI to rewrite only creative fields (HOOK/BODY/TITLE/MOOD) and not format or CTA.
            var imageParts = BuildImageContentParts(imageUrls);
            var template = _promptTemplateService.GetTemplate(templateType.Value);
            var newCreative = await GenerateCreativeJsonAsync(templateType.Value, null, template, imageParts, variables, creative, newPrompt);

            // Merge creative results into existing creative dict
            if (creative == null) creative = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in newCreative)
                creative[kv.Key] = kv.Value;

            // Render final caption locally
            var finalCaption = RenderFinalCaption(templateType.Value, variables, creative);
            finalCaption = SanitizeForbiddenVocabulary(finalCaption);

            if (!ValidateCaptionOutput(finalCaption, templateType.Value, out var errors))
            {
                throw new InvalidOperationException("Caption validation failed: " + string.Join("; ", errors));
            }

            return finalCaption;
        }
        private async Task<Dictionary<string, string>> ExtractTemplateVariablesAsync(ContentType type, object[] imageParts, GenerateCaptionRequest request)
        {
            var userProvided = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(request.Sku)) userProvided["SKU"] = request.Sku!;
            if (!string.IsNullOrWhiteSpace(request.ProductName)) userProvided["PRODUCT_NAME"] = request.ProductName!;
            if (!string.IsNullOrWhiteSpace(request.Scale)) userProvided["SCALE"] = request.Scale!;
            if (!string.IsNullOrWhiteSpace(request.EventName)) userProvided["EVENT_NAME"] = request.EventName!;
            if (!string.IsNullOrWhiteSpace(request.EventDate)) userProvided["DATE"] = request.EventDate!;

            var required = type switch
            {
                ContentType.ProductShowcase => new[] { "PRODUCT_NAME", "SKU", "SCALE" },
                ContentType.Storytelling => new[] { "PRODUCT_NAME", "SCALE" },
                ContentType.EventRecap => new[] { "EVENT_NAME", "DATE" },
                _ => Array.Empty<string>()
            };

            var missingFields = required
                .Where(f => !userProvided.TryGetValue(f, out var v) || string.IsNullOrWhiteSpace(v))
                .ToList();

            if (missingFields.Count == 0)
                return userProvided;

            var fieldList = string.Join(", ", missingFields.Select(f => $"\"{f}\""));
            var extractorBuilder = new StringBuilder();
            extractorBuilder.AppendLine($"Extract ONLY these fields from the image: {fieldList}");
            extractorBuilder.AppendLine("Return JSON only. If a field is not clearly visible, return empty string. Do NOT invent values.");
            extractorBuilder.AppendLine("{");
            extractorBuilder.AppendLine(string.Join(",\n", missingFields.Select(f => $"  \"{f}\": \"...\"")));
            extractorBuilder.AppendLine("}");
            var extractor = extractorBuilder.ToString();

            var content = new List<object> { new { type = "text", text = extractor } };
            content.AddRange(imageParts);

            var requestBody = new
            {
                model = "gpt-5.1",
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new { role = "user", content = content.ToArray() }
                },
                max_completion_tokens = 200,
                temperature = 0.1
            };

            var response = await SendRequestAsync(requestBody);
            var text = CleanupText(ExtractAssistantContent(response));

            try
            {
                var aiExtracted = JsonSerializer.Deserialize<Dictionary<string, string>>(text, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new Dictionary<string, string>();

                foreach (var kv in aiExtracted)
                {
                    if (!string.IsNullOrWhiteSpace(kv.Value) && !userProvided.ContainsKey(kv.Key))
                        userProvided[kv.Key] = kv.Value;
                }
            }
            catch
            {
                // Keep user-provided values; validation will report remaining missing fields.
            }

            return userProvided;
        }

        // New: Ask AI for simple creative JSON (no formatting, no CTA, no signature)
        private async Task<Dictionary<string, string>> GenerateCreativeJsonAsync(

    ContentType type,
    string? brandVoice,
    string template,
    object[] imageParts,
    Dictionary<string, string> variables,
    Dictionary<string, string>? existingCreative = null,
    string? toneHint = null,
    string? sceneStyle = null)
        {
            // SYSTEM PROMPT
            var systemPrompt = BuildGre911BrandBrainSystemPrompt(
            brandVoice,
            template
            );
            Console.WriteLine($"CONTENT TYPE = {type}");
            // USER PROMPT
            var userSb = new StringBuilder();

            // IMPORTANT:
            // Template is now the SINGLE SOURCE OF TRUTH.
            // Do NOT hardcode BODY / HOOK / TITLE schemas anymore.
            userSb.AppendLine(template);

            // Scene style injection
            if (!string.IsNullOrWhiteSpace(sceneStyle))
            {
                userSb.AppendLine();
                userSb.AppendLine("SCENE STYLE:");
                userSb.AppendLine(sceneStyle);
            }

            // Optional tone adjustment
            if (!string.IsNullOrWhiteSpace(toneHint))
            {
                userSb.AppendLine();
                userSb.AppendLine("TONE ADJUSTMENT:");
                userSb.AppendLine(toneHint);
            }

            // Variables from request
            if (variables.Any())
            {
                userSb.AppendLine();
                userSb.AppendLine("KNOWN VARIABLES:");
                foreach (var kv in variables)
                {
                    userSb.AppendLine($"{kv.Key} = {kv.Value}");
                }
            }

            // Existing creative for regeneration
            if (existingCreative != null && existingCreative.Any())
            {
                userSb.AppendLine();
                userSb.AppendLine("EXISTING CREATIVE:");
                foreach (var kv in existingCreative)
                {
                    userSb.AppendLine($"{kv.Key} = {kv.Value}");
                }
            }

            // FINAL STRICT RULE
            userSb.AppendLine();
            userSb.AppendLine("IMPORTANT:");
            userSb.AppendLine("- Return ONLY valid JSON.");
            userSb.AppendLine("- Do NOT wrap JSON in markdown.");
            userSb.AppendLine("- Do NOT explain.");
            userSb.AppendLine("- Do NOT add fields that are not defined in the template.");
            userSb.AppendLine("- Do NOT generate product metadata unless the template explicitly asks for it.");
            userSb.AppendLine("- Follow the template structure exactly.");
            userSb.AppendLine("- Missing fields must be returned as empty string.");
            switch (type)
            {
                case ContentType.ProductShowcase:
                    userSb.AppendLine();
                    userSb.AppendLine("PRODUCT SHOWCASE RULES:");
                    userSb.AppendLine("- Focus on display environment and collector value.");
                    userSb.AppendLine("- No storytelling narrative.");
                    break;

                case ContentType.Storytelling:
                    userSb.AppendLine();
                    userSb.AppendLine("STORYTELLING RULES:");
                    userSb.AppendLine("- Narrative only.");
                    userSb.AppendLine("- No product metadata.");
                    userSb.AppendLine("- No SKU.");
                    userSb.AppendLine("- No CTA.");
                    userSb.AppendLine("- No price.");
                    userSb.AppendLine("- No product information block.");
                    userSb.AppendLine("- Do NOT generate lines beginning with:");
                    userSb.AppendLine("  📦");
                    userSb.AppendLine("  🏭");
                    userSb.AppendLine("  📏");
                    userSb.AppendLine("  📩");
                    break;

                case ContentType.EventRecap:
                    userSb.AppendLine();
                    userSb.AppendLine("EVENT RECAP RULES:");
                    userSb.AppendLine("- Focus on community moments.");
                    userSb.AppendLine("- Focus on collectors, conversations and shared passion.");
                    userSb.AppendLine("- Mention people, displays, activities and interactions when visible.");
                    userSb.AppendLine("- Event recap must feel complete and meaningful.");
                    userSb.AppendLine("- Never leave recap sections empty.");
                    userSb.AppendLine("- OPENING_LINE must contain at least 1 sentence.");
                    userSb.AppendLine("- EVENT_DESCRIPTION must contain 2-4 sentences.");
                    userSb.AppendLine("- COMMUNITY_MOMENT must contain 1-3 sentences.");
                    userSb.AppendLine("- Total recap should usually be between 80 and 150 words.");
                    userSb.AppendLine("- No product metadata.");
                    userSb.AppendLine("- No SKU.");
                    userSb.AppendLine("- No CTA.");
                    userSb.AppendLine("- Do NOT write like a product showcase.");
                    userSb.AppendLine("- Do NOT write like a storytelling post.");
                    userSb.AppendLine("- Write like a real Facebook event recap.");
                    break;
            }

            // MULTIMODAL CONTENT
            var generationContent = new List<object>
{
    new
    {
        type = "text",
        text = userSb.ToString()
    }
};

            generationContent.AddRange(imageParts);

            // OPENAI REQUEST
            var requestBody = new
            {
                model = "gpt-5.1",
                response_format = new
                {
                    type = "json_object"
                },
                messages = new object[]
                {
        new
        {
            role = "system",
            content = systemPrompt
        },
        new
        {
            role = "user",
            content = generationContent.ToArray()
        }
                },
                max_completion_tokens = 700,
                temperature = 0.7
            };

            // SEND REQUEST
            var response = await SendRequestAsync(requestBody);

            // RAW AI RESPONSE
            var text = CleanupText(
                ExtractAssistantContent(response)
            );

            Console.WriteLine("========== RAW AI RESPONSE ==========");
            Console.WriteLine(text);
            Console.WriteLine("=====================================");

            // PARSE JSON
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(
                 text,
                 new JsonSerializerOptions
                 {
                     PropertyNameCaseInsensitive = true
                 }) ?? new Dictionary<string, string>();

                dict = FilterCreativeByContentType(type, dict);

                return dict;
            }
            catch (Exception ex)
            {
                Console.WriteLine("JSON PARSE ERROR:");
                Console.WriteLine(ex.Message);

                return new Dictionary<string, string>();
            }


        }
        private static Dictionary<string, string> FilterCreativeByContentType(
            ContentType type,
            Dictionary<string, string> creative)
        {
            var allowed = type switch
            {
                ContentType.ProductShowcase => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TITLE",
            "SHORT_ENVIRONMENT_DESCRIPTION",
            "COLLECTOR_DESCRIPTION",
            "HASHTAGS"
        },

                ContentType.Storytelling => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "TITLE",
            "OPENING_VISUAL",
            "SMALL_REALISTIC_MOMENT",
            "EMOTIONAL_LINE",
            "SCALE_LINE",
            "HASHTAGS"
        },

                ContentType.EventRecap => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "OPENING_LINE",
            "EVENT_DESCRIPTION",
            "COMMUNITY_MOMENT",
            "HASHTAGS"
        },

                _ => new HashSet<string>()
            };

            return creative
                .Where(x => allowed.Contains(x.Key))
                .ToDictionary(x => x.Key, x => x.Value,
                    StringComparer.OrdinalIgnoreCase);
        }

        private static ContentType MapToContentTypeEnum(string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType)) return ContentType.Storytelling;

            if (Enum.TryParse(contentType.Trim(), ignoreCase: true, out ContentType parsed))
                return parsed;

            var normalized = contentType.Trim().Replace("_", "");
            if (Enum.TryParse(normalized, ignoreCase: true, out parsed))
                return parsed;

            return ContentType.Storytelling;
        }

        private static string BuildGre911BrandBrainSystemPrompt(string? brandVoice, string template)
        {
            var brain = """
You are the official content writer for GRE•911 - a premium diecast, diorama, and miniature culture brand in Vietnam.

Write like a real collector page. Avoid ecommerce/corporate tone and exaggerated marketing.
Prefer cinematic atmosphere, collector realism, subtle emotional tone.

Rules:
- ALWAYS write in Vietnamese.
- NEVER mention AI.
- Avoid generic AI marketing language.

Preferred vocabulary (use naturally when appropriate):
- workshop, industrial garage, display setup, collector shelf, atmosphere, cinematic lighting, JDM, street build, background, góc display

Forbidden phrasing (do NOT use):
- món đồ chơi, tuyệt vời, độc đáo, tác phẩm nghệ thuật, lý tưởng cho, sản phẩm chất lượng, hoàn hảo cho, đẳng cấp, ấn tượng mạnh

Return valid JSON only. No markdown. No extra keys beyond what the user prompt requests.
""";

            var brand = string.IsNullOrWhiteSpace(brandVoice)
                ? "Brand voice: Making Things Small. Creating the Culture."
                : brandVoice;

            return $"{brain}\n\n{brand}\n\nTEMPLATE (follow this style strictly):\n{template}";
        }

        private static object[] BuildImageContentParts(List<string> imageUrls)
        {
            var limitedUrls = imageUrls.Take(4).ToList();
            var parts = new List<object>();
            foreach (var url in imageUrls)
            {
                parts.Add(new { type = "image_url", image_url = new { url } });
            }
            return parts.ToArray();
        }

        private static string ExtractAssistantContent(string response)
        {
            using var doc = JsonDocument.Parse(response);
            return doc.RootElement.GetProperty("choices")[0]
                .GetProperty("message").GetProperty("content")
                .GetString() ?? "";
        }

        private static string BuildSceneStyleKeywords(string? sceneType)
        {
            if (string.IsNullOrWhiteSpace(sceneType)) return "";
            if (!Enum.TryParse(sceneType, ignoreCase: true, out SceneType st))
                return "";

            return st switch
            {
                SceneType.IndustrialGarage => "industrial garage, workshop, display setup, background, atmosphere, cinematic lighting",
                SceneType.JapaneseStreet => "Japanese street, urban night, street build, background, display setup, atmosphere",
                SceneType.GasStation => "gas station, neon lights, cinematic lighting, street build, background, atmosphere",
                SceneType.Warehouse => "warehouse, industrial, cinematic lighting, collector shelf, background, atmosphere",
                SceneType.UrbanNight => "urban night, street build, cinematic, neon, background, atmosphere",
                _ => ""
            };
        }

        private static string CleanupText(string text)

        {
            if (string.IsNullOrWhiteSpace(text)) return "{}";
            return text.Replace("```json", "").Replace("```", "").Trim();
        }

        private static string SanitizeForbiddenVocabulary(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input ?? "";

            var output = input;

            // Remove forbidden phrases (word-boundary, case-insensitive)
            foreach (var bad in ForbiddenVocabulary)
            {
                output = Regex.Replace(output, $@"\b{Regex.Escape(bad)}\b", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }

            // Optionally encourage preferred words by suggesting replacements (light approach)
            foreach (var pref in PreferredVocabulary)
            {
                // no-op; just ensure term exists in domain vocabulary; do not force insertion here
            }

            // Normalize newlines consistently, preserve line breaks
            output = output.Replace("\r\n", "\n").Replace("\r", "\n");

            // Collapse multiple spaces/tabs but preserve line breaks
            output = Regex.Replace(output, @"[ \t]{2,}", " ");

            // Collapse excessive blank lines to at most two consecutive newlines
            output = Regex.Replace(output, @"\n{3,}", "\n\n");

            // Trim each line and overall (preserve internal newlines)
            var lines = output.Split('\n');
            for (int i = 0; i < lines.Length; i++)
                lines[i] = lines[i].Trim();
            output = string.Join("\n", lines).Trim();

            return output;
        }

        private static bool ValidateTemplateVariables(ContentType type, Dictionary<string, string> vars, out List<string> missing)
        {
            missing = new List<string>();
            var required = type switch
            {
                ContentType.ProductShowcase => new[] { "PRODUCT_NAME", "SKU" },
                ContentType.Storytelling => new[] { "PRODUCT_NAME" },
                ContentType.EventRecap => new[] { "EVENT_NAME", "DATE" },
                _ => Array.Empty<string>()
            };

            foreach (var r in required)
            {
                if (!vars.TryGetValue(r, out var v) || string.IsNullOrWhiteSpace(v))
                    missing.Add(r);
            }

            return missing.Count == 0;
        }

        // Changed signature to accept ContentType so CTA enforcement is type-aware
        private static bool ValidateCaptionOutput(string caption, ContentType type, out List<string> errors)
        {
            errors = new List<string>();
            if (type == ContentType.Storytelling)
            {
                if (caption.Contains("📦") ||
                    caption.Contains("🏭") ||
                    caption.Contains("📏") ||
                    caption.Contains("📩") ||
                    caption.Contains("Mã sản phẩm") ||
                    caption.Contains("Inbox"))
                {
                    errors.Add("Storytelling contains ProductShowcase metadata.");
                }
            }
            if (string.IsNullOrWhiteSpace(caption))
            {
                errors.Add("Caption is empty.");
                return false;
            }

            // must contain at least one newline
            if (!caption.Contains("\n"))
                errors.Add("Caption must contain a line break.");

            // too short
            if (caption.Length < 60)
                errors.Add("Caption is too short.");

            var bodyText = caption;

            // ignore metadata
            bodyText = Regex.Replace(bodyText, @"📦.*|🏭.*|📏.*|#.*", "");

            var words = Regex.Split(bodyText, @"\s+")
                .Where(w => Regex.IsMatch(w, @"\p{L}|\p{N}"))
                .Count();

            if (words < 10)
                errors.Add("Caption too short overall body.");

            // signature detection: expect '#GRE911' or 'GRE•911' or 'GRE911'
            if (!(caption.Contains("#GRE911") || caption.Contains("GRE•911") || caption.Contains("GRE911")))
                errors.Add("Missing signature (e.g. #GRE911 or GRE•911).");

            // duplicate word detection (adjacent duplicates)
            if (HasDuplicateAdjacentWords(caption))
                errors.Add("Caption contains duplicate adjacent words.");

            return errors.Count == 0;
        }

     private static bool HasDuplicateAdjacentWords(string input)
{
    var words = Regex.Matches(
            input.ToLowerInvariant(),
            @"[\p{L}\p{N}]+")
        .Select(m => m.Value)
        .ToList();

    for (int i = 0; i < words.Count - 1; i++)
    {
        if (words[i].Length < 3)
            continue;

        if (words[i] == words[i + 1])
        {
            Console.WriteLine($"DUPLICATE WORD FOUND: {words[i]}");
            return true;
        }
    }

    return false;
}
        // Guard function to strip leaked metadata from AI output
        private static string StripLeakedMetadata(string aiText)
        {
            if (string.IsNullOrWhiteSpace(aiText)) return aiText;

            // Strip any metadata/CTA lines AI may have leaked into creative fields
            var leakPatterns = new[]
            {
                @"📦.*?(?=\n|$)",
                @"🏭.*?(?=\n|$)",
                @"📏.*?(?=\n|$)",
                @"📩.*?(?=\n|$)",
                @"Mã sản phẩm.*?(?=\n|$)",
                @"Tên sản phẩm.*?(?=\n|$)",
                @"Tỷ lệ.*?(?=\n|$)",
                @"Inbox GRE.*?(?=\n|$)",
                @"Making Things Small.*?(?=\n|$)"
            };

            var output = aiText;
            foreach (var pattern in leakPatterns)
                output = Regex.Replace(output, pattern, "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

            // Clean up extra newlines created by stripping
            output = Regex.Replace(output, @"\n{2,}", "\n");
            return output.Trim();
        }

        // Backend renders final caption deterministically using pieces returned by AI and guaranteed variables
        private static string RenderFinalCaption(ContentType type, Dictionary<string, string> vars, Dictionary<string, string> creative)
        {
            return type switch
            {
                ContentType.ProductShowcase => RenderProductShowcase(vars, creative),
                ContentType.Storytelling => RenderStorytelling(vars, creative),
                ContentType.EventRecap => RenderEventRecap(vars, creative),
                _ => RenderProductShowcase(vars, creative)
            };
        }

        // ProductShowcase: header cinematic -> body -> metadata block -> CTA -> tagline -> hashtags
        private static string RenderProductShowcase(
 Dictionary<string, string> vars,
 Dictionary<string, string> creative)
        {
            string environment = StripLeakedMetadata(
            creative.TryGetValue("SHORT_ENVIRONMENT_DESCRIPTION", out var env)
            ? env.Trim()
            : "");

            string collector = StripLeakedMetadata(
                creative.TryGetValue("COLLECTOR_DESCRIPTION", out var col)
                    ? col.Trim()
                    : "");

            string hashtags = creative.TryGetValue("HASHTAGS", out var ht)
                ? ht.Trim()
                : "#GRE911";

            string productName = vars.TryGetValue("PRODUCT_NAME", out var pn)
                ? pn
                : "Unknown Product";

            string sku = vars.TryGetValue("SKU", out var s)
                ? s
                : "GRE911";

            string scale = vars.TryGetValue("SCALE", out var sc)
                ? sc
                : "1:64";

            var sb = new StringBuilder();

            sb.AppendLine($"🔥{productName.ToUpper()} _ {sku} 🔥");

            if (!string.IsNullOrWhiteSpace(environment))
            {
                sb.AppendLine();
                sb.AppendLine(environment);
            }

            if (!string.IsNullOrWhiteSpace(collector))
            {
                sb.AppendLine();
                sb.AppendLine(collector);
            }

            sb.AppendLine();
            sb.AppendLine($"📦 Mã sản phẩm: {sku}");
            sb.AppendLine($"🏭 Tên sản phẩm: {productName}");
            sb.AppendLine($"📏 Tỷ lệ: {scale}");

            sb.AppendLine();
            sb.AppendLine($"🌐 Website: https://garage911vn.com/");

            sb.AppendLine();
            sb.AppendLine("📩 Inbox GRE•911 hoặc truy cập website để nhận báo giá và thông tin chi tiết.");

            sb.AppendLine();
            sb.AppendLine("Making Things Small. Creating the Culture.");

            sb.AppendLine();
            sb.AppendLine(hashtags);

            return sb.ToString().Trim();


        }

        private static string RenderStorytelling(
        Dictionary<string, string> vars,
        Dictionary<string, string> creative)
        {
            string title = StripLeakedMetadata(
            creative.TryGetValue("TITLE", out var t)
            ? t.Trim()
            : "");


            string opening = StripLeakedMetadata(
                creative.TryGetValue("OPENING_VISUAL", out var ov)
                    ? ov.Trim()
                    : "");

            string realisticMoment = StripLeakedMetadata(
                creative.TryGetValue("SMALL_REALISTIC_MOMENT", out var rm)
                    ? rm.Trim()
                    : "");

            string emotional = StripLeakedMetadata(
                creative.TryGetValue("EMOTIONAL_LINE", out var em)
                    ? em.Trim()
                    : "");

            string scaleLine = StripLeakedMetadata(
                creative.TryGetValue("SCALE_LINE", out var sl)
                    ? sl.Trim()
                    : "1:64 — nhưng vẫn giữ được nhịp sống rất thật.");

            string hashtags = creative.TryGetValue("HASHTAGS", out var ht)
                ? ht.Trim()
                : "#GRE911";

            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(title))
            {
                sb.AppendLine(title.ToUpper());
            }

            if (!string.IsNullOrWhiteSpace(opening))
            {
                sb.AppendLine();
                sb.AppendLine(opening);
            }

            if (!string.IsNullOrWhiteSpace(realisticMoment))
            {
                sb.AppendLine();
                sb.AppendLine(realisticMoment);
            }

            if (!string.IsNullOrWhiteSpace(emotional))
            {
                sb.AppendLine();
                sb.AppendLine(emotional);
            }

            sb.AppendLine();
            sb.AppendLine(scaleLine);

            sb.AppendLine();
            sb.AppendLine("Making Things Small. Creating the Culture.");

            sb.AppendLine();
            sb.AppendLine(hashtags);

            return sb.ToString().Trim();


        }

        private static string RenderEventRecap(
        Dictionary<string, string> vars,
        Dictionary<string, string> creative)
        {
            string opening = StripLeakedMetadata(
            creative.TryGetValue("OPENING_LINE", out var op)
            ? op.Trim()
            : "");


            string eventDescription = StripLeakedMetadata(
                creative.TryGetValue("EVENT_DESCRIPTION", out var ed)
                    ? ed.Trim()
                    : "");

            string communityMoment = StripLeakedMetadata(
                creative.TryGetValue("COMMUNITY_MOMENT", out var cm)
                    ? cm.Trim()
                    : "");

            string hashtags = creative.TryGetValue("HASHTAGS", out var ht)
                ? ht.Trim()
                : "#GRE911";

            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(opening))
            {
                sb.AppendLine(opening);
            }

            if (!string.IsNullOrWhiteSpace(eventDescription))
            {
                sb.AppendLine();
                sb.AppendLine(eventDescription);
            }

            if (!string.IsNullOrWhiteSpace(communityMoment))
            {
                sb.AppendLine();
                sb.AppendLine(communityMoment);
            }

            sb.AppendLine();
            sb.AppendLine("📸 Cùng nhìn lại một vài khoảnh khắc đáng nhớ của buổi offline qua loạt ảnh dưới đây nhé!");

            sb.AppendLine();
            sb.AppendLine(hashtags);

            return sb.ToString().Trim();


        }


        private async Task<string> SendRequestAsync(object requestBody)
        {
            var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            for (int retry = 0; retry < 5; retry++)
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                    return responseBody;

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var delay = (int)Math.Pow(2, retry) * 1000;
                    await Task.Delay(delay);
                    continue;
                }

                throw new Exception($"OpenAI Error: {responseBody}");
            }

            throw new Exception("OpenAI rate limit exceeded.");
        }
    }
}
using Amazon.Lambda.Core;
using System.Text.Json;
using LinkPreviewCore;


// Required for Lambda JSON
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace LinkPreviewLambda;

public class Function
{
    public async Task<object> FunctionHandler(AppSyncEvent? request, ILambdaContext context)
    {
        try
        {

            context.Logger.LogInformation($"Got request: {JsonSerializer.Serialize(request)}");
        
        // 1. Extract arguments
        var url = request?.Arguments["url"]?.ToString();
        
        if (string.IsNullOrEmpty(url))
        {
            context.Logger.LogError("No URL provided");
            return null;
        }
        

            var preview = await PreviewService.fetchPreview(url);

            return new
            {
                url = preview.Url,
                title = preview.Title,
                description = preview.Description,
                image = preview.Image,
                siteName = preview.SiteName
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex.Message);

            return new
            {
                url = request?.Arguments["url"]??"",
                title = (string?)null,
                description = (string?)null,
                image = (string?)null,
                siteName = (string?)null
            };
        }
    }
}

public class AppSyncEvent
{
    public Dictionary<string, object> Arguments { get; set; }
    
}

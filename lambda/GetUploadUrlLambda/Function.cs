using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace GetUploadUrlLambda;

public class Function
{
    private readonly IAmazonS3 _s3 = new AmazonS3Client();
   
    public async Task<object> FunctionHandler(AppSyncEvent? request, ILambdaContext context)
    {
        // Log incoming request
        context.Logger.LogInformation($"Request received: {System.Text.Json.JsonSerializer.Serialize(request)}");
        
        var fileName = request?.Arguments["fileName"]?.ToString();
        var contentType = request?.Arguments["contentType"]?.ToString();
        
        context.Logger.LogInformation($"fileName: {fileName}, contentType: {contentType}");
        
        // Check environment variable
        var bucketName = Environment.GetEnvironmentVariable("BUCKET_NAME");
        context.Logger.LogInformation($"BUCKET_NAME: {bucketName}");
        
        if (string.IsNullOrEmpty(bucketName))
        {
            context.Logger.LogError("BUCKET_NAME environment variable is missing!");
            throw new Exception("BUCKET_NAME not configured");
        }
        
        if (string.IsNullOrEmpty(fileName))
        {
            context.Logger.LogError("fileName is missing!");
            throw new Exception("fileName is required");
        }
        
        if (string.IsNullOrEmpty(contentType))
        {
            context.Logger.LogError("contentType is missing!");
            throw new Exception("contentType is required");
        }

        var key = $"chat-media/{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid()}-{fileName}";
        context.Logger.LogInformation($"Generated key: {key}");

        // Generate upload URL
        var uploadRequest = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = key,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.AddMinutes(5),
            ContentType = contentType
        };
        var uploadUrlRequest = _s3.GetPreSignedURL(uploadRequest);
        context.Logger.LogInformation($"Generated uploadUrl: {uploadUrlRequest}");

        // Generate view URL
        var viewRequest = new GetPreSignedUrlRequest
        {
            BucketName = bucketName,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.AddHours(1)
        };
        var viewUrl = _s3.GetPreSignedURL(viewRequest);
        context.Logger.LogInformation($"Generated viewUrl: {viewUrl}");

        // Check if URLs were generated
        if (string.IsNullOrEmpty(uploadUrlRequest) || string.IsNullOrEmpty(viewUrl))
        {
            context.Logger.LogError("Failed to generate URLs");
            throw new Exception("Failed to generate pre-signed URLs");
        }

        return new 
        {
            uploadUrl = uploadUrlRequest,
            fileUrl = viewUrl  
        };
    }
}

public class AppSyncEvent
{
    public Dictionary<string, object> Arguments { get; set; } = new();
}
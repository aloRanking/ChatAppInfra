using Amazon.Lambda.Core;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Amazon.S3;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace ImageModerationLambda;

public class Function
{
    
    private readonly AmazonRekognitionClient _rekognitionClient;
    private readonly AmazonS3Client _s3;

     public Function()
    {
        
        _rekognitionClient = new AmazonRekognitionClient(region:Amazon.RegionEndpoint.EUWest1);
        _s3 = new AmazonS3Client();
    }

    public async Task<Object> FunctionHandler(AppSyncEvent request, ILambdaContext context)
    {
        try
        {
            var bucket = Environment.GetEnvironmentVariable("BUCKET_NAME");
            var encodedKey = request.Arguments["key"]?.ToString();

            
            var key = Uri.UnescapeDataString(encodedKey);
        
        context.Logger.LogInformation($"Original key: {encodedKey}");
        context.Logger.LogInformation($"Decoded key: {key}");
        context.Logger.LogInformation($"Bucket: {bucket}");
    
    var response = await _s3.GetObjectAsync(bucket, key);
    
        using var ms = new MemoryStream();
        await response.ResponseStream.CopyToAsync(ms);
    
        var detectRequest = new DetectModerationLabelsRequest
        {
            Image = new Image
            {
                Bytes = ms
            },
            MinConfidence = 70
        };
    
        var moderation = await _rekognitionClient.DetectModerationLabelsAsync(detectRequest);
    
        return new
        {
            isSafe = moderation.ModerationLabels.Count == 0,
            reason = moderation.ModerationLabels.FirstOrDefault()?.Name
        };
        }
        catch (Exception ex)
        {
            
            context.Logger.LogError($"Rekognition error: {ex.Message}");
            return new
            {
                isSafe = false,
                reason = $"Moderation failed: {ex.Message}"
            };
        }
       
    }
}

public class AppSyncEvent
{
    public Dictionary<string, object> Arguments { get; set; } = new();
}

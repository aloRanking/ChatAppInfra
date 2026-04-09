using Amazon.CDK;
using Amazon.CDK.AWS.AppSync;
using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.S3.Deployment;
using System.IO;
using Constructs;
using Amazon.CDK.AWS.CloudFront;
using Amazon.CDK.AWS.CloudFront.Origins;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.SES.Actions;
using Amazon.CDK.AWS.Events.Targets;
using System.Collections.Generic;
using Amazon.CDK.AWS.SecretsManager;

namespace ChatAppInfra
{
    public class ChatAppInfraStack : Stack
    {
        public string EnvironmentName { get; }

        internal ChatAppInfraStack(Construct scope, string id, string environmentName, IStackProps props = null) : base(scope, id, props)
        {
            EnvironmentName = environmentName;


            if (EnvironmentName == "prod")
            {
                this.TerminationProtection = true;
            }
            var isProd = EnvironmentName == "prod";

            // var appSyncServiceRole = new Role(this, "AppSyncServiceRole", new RoleProps
            // {
            //     RoleName = $"chatapp-appsync-role-{EnvironmentName}",
            //     AssumedBy = new ServicePrincipal("appsync.amazonaws.com"),
            //     Description = "Allows AppSync resolvers to access backend resources",


            // });

            // appSyncServiceRole.ApplyRemovalPolicy(isProd
            //         ? RemovalPolicy.RETAIN
            //         : RemovalPolicy.DESTROY);




        //     var frontendBucket = new Bucket(this, "ChatAppFrontendBucket", new BucketProps
        //     {
        //         BucketName = $"chatapp-frontend-{EnvironmentName}",

        //         // Enable static website hosting
        //         WebsiteIndexDocument = "index.html",
        //         WebsiteErrorDocument = "index.html",
        //         BlockPublicAccess = BlockPublicAccess.BLOCK_ACLS_ONLY,
        //         // Public access for demo environment
        //         PublicReadAccess = true,

        //         RemovalPolicy = isProd
        // ? RemovalPolicy.RETAIN
        // : RemovalPolicy.DESTROY,

        //         AutoDeleteObjects = !isProd
        //     });

            var newFrontendBucket = new Bucket(this, "FrontendBucketCloudFront", new BucketProps
            {
                BucketName = $"chatapp-frontend-{EnvironmentName}-{Account}-{Region}",

                // Enable static website hosting
                WebsiteIndexDocument = "index.html",
                WebsiteErrorDocument = "index.html",
                BlockPublicAccess = BlockPublicAccess.BLOCK_ACLS_ONLY,
                // Public access for demo environment
                PublicReadAccess = true,

                RemovalPolicy = isProd
        ? RemovalPolicy.RETAIN
        : RemovalPolicy.DESTROY,

                AutoDeleteObjects = !isProd
            });

            var distribution = new Distribution(this, "FrontendDistribution", new DistributionProps
            {
                // Default behavior for all paths
                DefaultBehavior = new BehaviorOptions
                {
                    // Use S3 REST API endpoint (supports HTTPS)
                    Origin = new S3StaticWebsiteOrigin(newFrontendBucket),

                    // Redirect HTTP to HTTPS
                    ViewerProtocolPolicy = ViewerProtocolPolicy.REDIRECT_TO_HTTPS,

                    // Optional: Cache settings
                    //CachePolicy = CachePolicy.CACHING_OPTIMIZED,


                },

                // Handle SPA routing - serve index.html for 404s
                ErrorResponses = new[]
                {
        new ErrorResponse
        {
            HttpStatus = 404,
            ResponseHttpStatus = 200,
            ResponsePagePath = "/index.html"
        }
    },

                // Default root object
                DefaultRootObject = "index.html",

                // Optional: Enable logging
                EnableLogging = false,

                // Optional: Add custom domain (if you have one)
                // DomainNames = new[] { "app.yourdomain.com" },
                // Certificate = certificate,

                // Price class (cheaper options available)
                PriceClass = PriceClass.PRICE_CLASS_100
            });


            var cloudFrontUrl = $"https://{distribution.DistributionDomainName}";
            var googleClientSecret = SecretValue.SecretsManager("google-oauth-secret", new SecretsManagerSecretOptions
{
    JsonField = "clientSecret"  // Extract only the secret part
});

var googleClientId = SecretValue.SecretsManager("google-oauth-secret", new SecretsManagerSecretOptions
{
    JsonField = "clientId"
});

            var userPool = new UserPool(this, "ChatUserPool", new UserPoolProps
            {
                UserPoolName = $"chatapp-users-{EnvironmentName}",

                SignInAliases = new SignInAliases
                {
                    Username = true
                },

                SelfSignUpEnabled = true,

                // Enable email verification
                AutoVerify = new AutoVerifiedAttrs
                {
                    Email = true
                },

                UserVerification = new UserVerificationConfig
                {
                    EmailSubject = "Verify your ChatApp account",
                    EmailBody = "Your verification code is {####}"
                },

                // Needed for email-based recovery
                AccountRecovery = AccountRecovery.EMAIL_ONLY,

                PasswordPolicy = new PasswordPolicy
                {
                    MinLength = 8,
                    RequireDigits = true,
                    RequireLowercase = true,
                    RequireUppercase = true,
                    RequireSymbols = false
                },

                RemovalPolicy = isProd
          ? RemovalPolicy.RETAIN
          : RemovalPolicy.DESTROY
            });


            var googleProvider = new UserPoolIdentityProviderGoogle(this, "Google", new UserPoolIdentityProviderGoogleProps
            {
                UserPool = userPool,

    ClientId = "444380488435-0bothioqg10ik4q4febhnbkt1k0pskhg.apps.googleusercontent.com",
    ClientSecretValue = SecretValue.UnsafePlainText(""),

                Scopes = new[]
                {
        "profile",
        "email",
        "openid"
    },

                AttributeMapping = new AttributeMapping
                {
                    Email = ProviderAttribute.GOOGLE_EMAIL,
                    GivenName = ProviderAttribute.GOOGLE_GIVEN_NAME,
                    FamilyName = ProviderAttribute.GOOGLE_FAMILY_NAME
                }
            });



            var cognitoDomain = userPool.AddDomain("ChatDomain", new UserPoolDomainOptions
            {
                CognitoDomain = new CognitoDomainOptions
                {
                    DomainPrefix = $"chatapp-{EnvironmentName}"
                }
            });

            var userPoolClient = userPool.AddClient("ChatAppClient", new UserPoolClientOptions
            {
                UserPoolClientName = $"chatapp-client-{EnvironmentName}",

                AuthFlows = new AuthFlow
                {
                    UserPassword = true,
                    UserSrp = true
                },

                GenerateSecret = false,

                SupportedIdentityProviders = new[]
    {
        UserPoolClientIdentityProvider.COGNITO,
        UserPoolClientIdentityProvider.GOOGLE
    },

                OAuth = new OAuthSettings
                {
                    Flows = new OAuthFlows
                    {
                        AuthorizationCodeGrant = true
                    },

                    Scopes = new[]
        {
            OAuthScope.OPENID,
            OAuthScope.EMAIL,
            OAuthScope.PROFILE
        },

                    CallbackUrls = new[]
        {
            "http://localhost:4200",
            "http://localhost:4200/",
            cloudFrontUrl,
            $"{cloudFrontUrl}/",
        },

                    LogoutUrls = new[]
        {
            "http://localhost:4200",
            cloudFrontUrl,
            $"{cloudFrontUrl}/",
        }
                }
            });

            userPoolClient.Node.AddDependency(googleProvider);

            var api = new GraphqlApi(this, "ChatApi", new GraphqlApiProps
            {
                Name = $"chatapp-api-{EnvironmentName}",

                Definition = Definition.FromFile("schema.graphql"),

                AuthorizationConfig = new AuthorizationConfig
                {
                    DefaultAuthorization = new AuthorizationMode
                    {
                        AuthorizationType = AuthorizationType.USER_POOL,
                        UserPoolConfig = new UserPoolConfig
                        {
                            UserPool = userPool
                        }
                    }
                },

                XrayEnabled = true
            });
            var envJs = $@"
window.__env = {{
  region: '{this.Region}',
  graphqlEndpoint: '{api.GraphqlUrl}',
  userPoolId: '{userPool.UserPoolId}',
  userPoolClientId: '{userPoolClient.UserPoolClientId}',
  cognitoDomain: '{cognitoDomain.DomainName}.auth.{this.Region}.amazoncognito.com',
  redirectSignIn: '{cloudFrontUrl}',
  redirectSignOut: '{cloudFrontUrl}'
}};
";




            var chatTable = new Table(this, "ChatTable", new TableProps
            {
                TableName = $"ChatTable-{EnvironmentName}",
                PartitionKey = new Attribute
                {
                    Name = "PK",
                    Type = AttributeType.STRING
                },
                SortKey = new Attribute
                {
                    Name = "SK",
                    Type = AttributeType.STRING
                },
                BillingMode = BillingMode.PAY_PER_REQUEST,
                RemovalPolicy = isProd ? RemovalPolicy.RETAIN : RemovalPolicy.DESTROY,
                PointInTimeRecoverySpecification = new PointInTimeRecoverySpecification
                {
                    PointInTimeRecoveryEnabled = isProd
                },
            });

            chatTable.AddGlobalSecondaryIndex(new GlobalSecondaryIndexProps
            {
                IndexName = "EntityType-createdAt-index",
                PartitionKey = new Attribute
                {
                    Name = "EntityType",
                    Type = AttributeType.STRING
                },
                SortKey = new Attribute
                {
                    Name = "createdAt",
                    Type = AttributeType.STRING
                },
                ProjectionType = ProjectionType.ALL
            });


            var chatDs = api.AddDynamoDbDataSource(
                "ChatDataSource",
                chatTable,
                    new DataSourceOptions
                    {

                        Description = "Data source for chat rooms and messages",
                        Name = $"ChatDataSource-{EnvironmentName}"
                    }
            );

            chatDs.CreateResolver("CreateRoomResolver", new BaseResolverProps
            {
                TypeName = "Mutation",
                FieldName = "createRoom",

                RequestMappingTemplate = MappingTemplate.FromString(@"
{
  ""version"": ""2018-05-29"",
  ""operation"": ""PutItem"",
  ""key"": {
    ""PK"": $util.dynamodb.toDynamoDBJson(""ROOM#$util.autoId()""),
    ""SK"": $util.dynamodb.toDynamoDBJson(""META"")
  },
  ""attributeValues"": {
    ""EntityType"": { ""S"": ""Room"" },
    ""name"": $util.dynamodb.toDynamoDBJson($ctx.args.name),
    ""createdAt"": $util.dynamodb.toDynamoDBJson($util.time.nowISO8601())
  }
}
"),

                ResponseMappingTemplate = MappingTemplate.FromString(@"
#set($item = $ctx.result)
{
  ""roomId"": ""$item.PK.replace('ROOM#','')"",
  ""name"": ""$item.name"",
  ""createdAt"": ""$item.createdAt""
}
")
            });

            chatDs.CreateResolver("ListRoomsResolver", new BaseResolverProps
            {
                TypeName = "Query",
                FieldName = "listRooms",
                RequestMappingTemplate = MappingTemplate.FromString(@"
{
  ""version"": ""2018-05-29"",
  ""operation"": ""Query"",
  ""index"": ""EntityType-createdAt-index"",
  ""query"": {
    ""expression"": ""EntityType = :entityType"",
    ""expressionValues"": {
      "":entityType"": $util.dynamodb.toDynamoDBJson(""Room"")
    }
  },
  ""scanIndexForward"": false
}
"),
                ResponseMappingTemplate = MappingTemplate.FromString(@"
#set($items = $ctx.result.items)
[
#foreach($item in $items)
  {
    ""roomId"": ""$item.PK.replace('ROOM#','')"",
    ""name"": ""$item.name"",
    ""createdAt"": ""$item.createdAt""
  }#if($foreach.hasNext),#end
#end
]
")
            });


            chatDs.CreateResolver("GetRoomResolver", new BaseResolverProps
            {
                TypeName = "Query",
                FieldName = "getRoom",
                RequestMappingTemplate = MappingTemplate.FromString(@"
{
  ""version"": ""2018-05-29"",
  ""operation"": ""GetItem"",
  ""key"": {
    ""PK"": $util.dynamodb.toDynamoDBJson(""ROOM#$ctx.args.roomId""),
    ""SK"": $util.dynamodb.toDynamoDBJson(""META"")
  }
}
"),
                ResponseMappingTemplate = MappingTemplate.FromString(@"
#if($ctx.result)
  {
    ""roomId"": ""$ctx.result.PK.replace('ROOM#','')"",
    ""name"": ""$ctx.result.name"",
    ""createdAt"": ""$ctx.result.createdAt""
  }
#else
  $util.error(""Room not found"")
#end
")
            });

            chatDs.CreateResolver("SendMessageResolver", new BaseResolverProps
            {
                TypeName = "Mutation",
                FieldName = "sendMessage",
                RequestMappingTemplate = MappingTemplate.FromString(@"
#set($messageId = $util.autoId())
#set($timestamp = $util.time.nowISO8601())
{
  ""version"": ""2018-05-29"",
  ""operation"": ""PutItem"",
  ""key"": {
    ""PK"": $util.dynamodb.toDynamoDBJson(""ROOM#$ctx.args.roomId""),
    ""SK"": $util.dynamodb.toDynamoDBJson(""MSG#$timestamp#$messageId"")
  },
  ""attributeValues"": {
    ""EntityType"": { ""S"": ""Message"" },
    ""messageId"": { ""S"": ""$messageId"" },
    ""roomId"": $util.dynamodb.toDynamoDBJson($ctx.args.roomId),
    ""senderUserId"": $util.dynamodb.toDynamoDBJson($ctx.identity.sub),
    ""senderUsername"": $util.dynamodb.toDynamoDBJson($ctx.identity.username),
    ""content"": $util.dynamodb.toDynamoDBJson($ctx.args.content),
    ""createdAt"": { ""S"": ""$timestamp"" }
    #if($ctx.args.linkPreview)
    ,
    ""linkPreview"": $util.dynamodb.toDynamoDBJson($ctx.args.linkPreview)
    #end
    #if($ctx.args.mediaUrl)
    ,
    ""mediaUrl"": $util.dynamodb.toDynamoDBJson($ctx.args.mediaUrl)
    #end
    #if($ctx.args.mediaType)
    ,
    ""mediaType"": $util.dynamodb.toDynamoDBJson($ctx.args.mediaType)
    #end
    #if($ctx.args.messageType)
    ,
    ""messageType"": $util.dynamodb.toDynamoDBJson($ctx.args.messageType)
    #end
  }
}
"),
                ResponseMappingTemplate = MappingTemplate.FromString(@"
{
  ""messageId"": ""$ctx.result.messageId"",
  ""roomId"": ""$ctx.result.roomId"",
  ""senderUserId"": ""$ctx.result.senderUserId"",
  ""senderUsername"": ""$ctx.result.senderUsername"",
  ""content"": ""$ctx.result.content"",
  ""createdAt"": ""$ctx.result.createdAt"",
  ""linkPreview"": $util.toJson($ctx.result.linkPreview),
  ""mediaUrl"": ""$ctx.result.mediaUrl"",
  ""mediaType"": ""$ctx.result.mediaType"",
  ""messageType"": ""$ctx.result.messageType""
}
")

            });

            chatDs.CreateResolver("GetMessagesResolver", new BaseResolverProps
            {
                TypeName = "Query",
                FieldName = "getMessages",
                RequestMappingTemplate = MappingTemplate.FromString(@"
{
  ""version"": ""2018-05-29"",
  ""operation"": ""Query"",
  ""query"": {
    ""expression"": ""PK = :pk AND begins_with(SK, :sk)"",
    ""expressionValues"": {
      "":pk"": $util.dynamodb.toDynamoDBJson(""ROOM#$ctx.args.roomId""),
      "":sk"": $util.dynamodb.toDynamoDBJson(""MSG#"")
    }
  }
}
"),
                ResponseMappingTemplate = MappingTemplate.FromString(@"
#set($items = [])
#foreach($item in $ctx.result.items)
  #set($newItem = {
    ""messageId"": ""$item.messageId"",
    ""roomId"": ""$item.roomId"",
    ""senderUserId"": ""$item.senderUserId"",
    ""senderUsername"": ""$item.senderUsername"",
    ""content"": ""$item.content"",
    ""createdAt"": ""$item.createdAt"",
    ""linkPreview"": $item.linkPreview,
    ""mediaUrl"": ""$item.mediaUrl"",
    ""mediaType"": ""$item.mediaType"",
    ""messageType"": ""$item.messageType""
  })
  $util.qr($items.add($newItem))
#end

$util.toJson($items)
")
            });


            new BucketDeployment(this, "DeployAngularApp", new BucketDeploymentProps
            {
                DestinationBucket = newFrontendBucket,

                Sources = new ISource[]
     {
        Source.Asset(Path.Combine(Directory.GetCurrentDirectory(), "frontend/dist/chatapp-frontend/browser")),
        Source.Data("env.js", envJs)
     },

                Distribution = distribution,
                DistributionPaths = new[] { "/*" }
            });



            var linkPreviewLambda = new Amazon.CDK.AWS.Lambda.Function(this, "LinkPreviewLambda", new Amazon.CDK.AWS.Lambda.FunctionProps
            {
                Runtime = Runtime.DOTNET_8,
                Handler = "LinkPreviewLambda::LinkPreviewLambda.Function::FunctionHandler",
                Code = Amazon.CDK.AWS.Lambda.Code.FromAsset(Path.Combine(Directory.GetCurrentDirectory(), "lambda/LinkPreview/LinkPreviewLambda/bin/Release/net8.0/publish")),
                Timeout = Duration.Seconds(10),
                MemorySize = 512
            });


            var linkPreviewDs = api.AddLambdaDataSource("LinkPreviewDS", linkPreviewLambda);

            linkPreviewDs.CreateResolver("GetLinkPreviewResolver", new BaseResolverProps
            {
                TypeName = "Query",
                FieldName = "getLinkPreview"
            });


            //CHAT MEDIA UPLOAD

            var mediaBucket = new Bucket(this, "ChatMediaBucket", new BucketProps
            {
                BucketName = $"chatapp-mediaa-{EnvironmentName}-{Account}-{Region}",
                BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
                Encryption = BucketEncryption.S3_MANAGED,
                Cors = new[]
                {
                new CorsRule
                {
                    AllowedMethods = new[]
                    {
                        HttpMethods.PUT,
                        HttpMethods.GET
                    },
                    AllowedOrigins = new[] { "*" },
                    AllowedHeaders = new[] { "*" }
                }
            },
                        LifecycleRules = new[]
                    {
            new LifecycleRule
            {
                Expiration = Duration.Days(30)
            }
}


            });

            var uploadImageLamda = new Amazon.CDK.AWS.Lambda.Function(this, "ImageUploadLambda", new Amazon.CDK.AWS.Lambda.FunctionProps
            {
                Runtime = Runtime.DOTNET_8,
                Handler = "GetUploadUrlLambda::GetUploadUrlLambda.Function::FunctionHandler",
                Code = Amazon.CDK.AWS.Lambda.Code.FromAsset(Path.Combine(Directory.GetCurrentDirectory(), "lambda/GetUploadUrlLambda/bin/Release/net8.0/publish")),
                Timeout = Duration.Seconds(10),
                MemorySize = 512,
                Environment = new Dictionary<string, string>
                {
                    { "BUCKET_NAME", mediaBucket.BucketName }
                },


            });

            mediaBucket.GrantReadWrite(uploadImageLamda);

            var uploadImageLambdaDS = api.AddLambdaDataSource("UploadImageDataSource", uploadImageLamda);

            uploadImageLambdaDS.CreateResolver("GetUploadUrlResolver", new BaseResolverProps
            {
                TypeName = "Mutation",
                FieldName = "getUploadUrl"
            });

            //image moderation lambda
            var imageModerationLambda = new Amazon.CDK.AWS.Lambda.Function(this, "ImageModerationLambda", new Amazon.CDK.AWS.Lambda.FunctionProps
            {
                Runtime = Runtime.DOTNET_8,
                Handler = "ImageModerationLambda::ImageModerationLambda.Function::FunctionHandler",
                Code = Amazon.CDK.AWS.Lambda.Code.FromAsset( Path.Combine(Directory.GetCurrentDirectory(), "lambda/ImageModerationLambda/bin/Release/net8.0/publish")),
                Timeout = Duration.Seconds(10),
                MemorySize = 512,
                Environment = new Dictionary<string, string>
    {
        { "BUCKET_NAME", mediaBucket.BucketName },
        

    }
            });

            mediaBucket.GrantRead(imageModerationLambda);
            imageModerationLambda.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
{
    Actions = new[]
    {
        "rekognition:DetectModerationLabels",
        "s3:GetObject",
        "s3:GetObjectMetadata"
    },
    Resources = new[] { "*" }
}));

            var imageModerationLambdaDS = api.AddLambdaDataSource("ImageModerationDataSource", imageModerationLambda);

            imageModerationLambdaDS.CreateResolver("ModerateImageResolver", new BaseResolverProps
            {
                TypeName = "Mutation",
                FieldName = "moderateImage"
            });


            new CfnOutput(this, "GraphqlEndpoint", new CfnOutputProps
            {
                Value = api.GraphqlUrl
            });

            new CfnOutput(this, "UserPoolId", new CfnOutputProps
            {
                Value = userPool.UserPoolId
            });

            new CfnOutput(this, "UserPoolClientId", new CfnOutputProps
            {
                Value = userPoolClient.UserPoolClientId
            });

            new CfnOutput(this, "Region", new CfnOutputProps
            {
                Value = this.Region
            });

            new CfnOutput(this, "FrontendURL", new CfnOutputProps
            {
                Value = $"http://{newFrontendBucket.BucketWebsiteDomainName}",
                Description = "Frontend website URL"
            });

            new CfnOutput(this, "CognitoDomain", new CfnOutputProps
            {
                Value = cognitoDomain.BaseUrl(),
                Description = "Cognito Hosted UI URL"
            });

            new CfnOutput(this, "LoginUrl", new CfnOutputProps
            {
                Value = $"{cognitoDomain.BaseUrl()}/login?response_type=code&client_id={userPoolClient.UserPoolClientId}&redirect_uri=http://localhost:4200&identity_provider=Google"
            });

            new CfnOutput(this, "CloudFrontURL", new CfnOutputProps
            {
                Value = cloudFrontUrl
            });

            new CfnOutput(this, "MediaBucketName", new CfnOutputProps
            {
                Value = mediaBucket.BucketName,
                Description = "S3 bucket for media uploads"
            });





            // var messagesTable = new Table(this, "MessagesTable", new TableProps
            // {
            //     TableName = $"chatapp-messages-{EnvironmentName}",
            //     PartitionKey = new Attribute
            //     {
            //         Name = "id",
            //         Type = AttributeType.STRING
            //     },

            //     SortKey = new Attribute
            //     {
            //         Name = "createdAt",
            //         Type = AttributeType.STRING
            //     },
            //     BillingMode = BillingMode.PAY_PER_REQUEST,

            //     PointInTimeRecoverySpecification = new PointInTimeRecoverySpecification
            //     {
            //         PointInTimeRecoveryEnabled = isProd
            //     },


            //     RemovalPolicy = isProd
            //         ? RemovalPolicy.RETAIN
            //         : RemovalPolicy.DESTROY
            // });

            // var messagesDataSource = api.AddDynamoDbDataSource(
            //     "MessagesDataSource",
            //     messagesTable,
            //      new DataSourceOptions  
            //     {

            //         Description = "Data source for chat messages",
            //         Name = $"MessagesDataSource-{EnvironmentName}"
            //     }

            // );

            // messagesDataSource.CreateResolver("SendMessageResolver", new BaseResolverProps
            // {
            //     TypeName = "Mutation",
            //     FieldName = "sendMessage",

            //     RequestMappingTemplate = MappingTemplate.FromString(@"
            // {
            //   ""version"": ""2017-02-28"",
            //   ""operation"": ""PutItem"",
            //   ""key"": {
            //     ""roomId"": { ""S"": ""$ctx.args.roomId"" },
            //     ""createdAt"": { ""S"": ""$util.time.nowISO8601()"" }
            //   },
            //   ""attributeValues"": {
            //     ""id"": { ""S"": ""$util.autoId()"" },
            //     ""content"": { ""S"": ""$ctx.args.content"" },
            //     ""sender"": { ""S"": ""$ctx.identity.username"" }
            //   }
            // }
            // "),

            //     ResponseMappingTemplate = MappingTemplate.FromString("$util.toJson($ctx.result)")
            // });

            // messagesDataSource.CreateResolver("GetMessagesResolver", new BaseResolverProps
            // {
            //     TypeName = "Query",
            //     FieldName = "getMessages",

            //     RequestMappingTemplate = MappingTemplate.FromString(@"
            // {
            //   ""version"": ""2017-02-28"",
            //   ""operation"": ""Query"",
            //   ""query"": {
            //     ""expression"": ""roomId = :roomId"",
            //     ""expressionValues"": {
            //       "":roomId"": { ""S"": ""$ctx.args.roomId"" }
            //     }
            //   },
            //   ""scanIndexForward"": true
            // }
            // "),

            //     ResponseMappingTemplate = MappingTemplate.FromString("$util.toJson($ctx.result.items)")
            // });







        }


    }

}
// ChatApp-dev.GraphqlEndpoint = https://edgpzpffrvbc5ki7bwiblumuja.appsync-api.eu-north-1.amazonaws.com/graphql
// ChatApp-dev.Region = eu-north-1
// ChatApp-dev.UserPoolClientId = fobk7cij8nd4p2qjeaiomjrd3
// ChatApp-dev.UserPoolId = eu-north-1_3bejDTGNy

// ChatApp-prod.GraphqlEndpoint = https://xdeafstdijfoxouztletxj56pa.appsync-api.eu-north-1.amazonaws.com/graphql
// ChatApp-prod.Region = eu-north-1
// ChatApp-prod.UserPoolClientId = 493kr7fplsdeqoek57cb775bvv
// ChatApp-prod.UserPoolId = eu-north-1_fYi4nC96Q
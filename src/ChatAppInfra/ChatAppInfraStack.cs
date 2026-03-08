using Amazon.CDK;
using Amazon.CDK.AWS.AppSync;
using Amazon.CDK.AWS.Cognito;
using Amazon.CDK.AWS.DynamoDB;
using Amazon.CDK.AWS.IAM;
using Constructs;

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
            var userPoolClient = userPool.AddClient("ChatAppClient", new UserPoolClientOptions
            {
                UserPoolClientName = $"chatapp-client-{EnvironmentName}",

                AuthFlows = new AuthFlow
                {
                    UserPassword = true,
                    UserSrp = true
                },

                GenerateSecret = false
            });

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

            // api.GrantMutation(appSyncServiceRole);
            // api.GrantQuery(appSyncServiceRole);
            // api.GrantSubscription(appSyncServiceRole);




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
  }
}
"),
    ResponseMappingTemplate = MappingTemplate.FromString(@"

{
  ""messageId"": ""$util.autoId()"",
  ""roomId"": ""$ctx.args.roomId"",
  ""senderUserId"": ""$ctx.identity.sub"",
  ""senderUsername"": ""$ctx.identity.username"",
  ""content"": ""$ctx.args.content"",
  ""createdAt"": ""$util.time.nowISO8601()""
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
                ResponseMappingTemplate = MappingTemplate.FromString("$util.toJson($ctx.result.items)")
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
using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Constructs;

namespace ChatAppInfra.Constructs
{
    public class GitHubActionsOidcRole : Construct
    {
        public Role Role { get; }

        public GitHubActionsOidcRole(Construct scope, string id, string githubRepo, string environmentName = "dev") 
            : base(scope, id)
        {
            // Create OIDC identity provider for GitHub
            var provider = new OpenIdConnectProvider(this, "GitHubProvider", new OpenIdConnectProviderProps
            {
                Url = "https://token.actions.githubusercontent.com",
                ClientIds = new[] { "sts.amazonaws.com" },
                Thumbprints = new[] { "6938fd4d98bab03faadb97b34396831e3780aea1" }
            });

            // Create IAM role
            Role = new Role(this, "GitHubActionsRole", new RoleProps
            {
                RoleName = $"github-actions-{environmentName}",
                AssumedBy = new FederatedPrincipal(
                    provider.OpenIdConnectProviderArn,
                    new Dictionary<string, object>
                    {
                        ["StringEquals"] = new Dictionary<string, string>
                        {
                            { "token.actions.githubusercontent.com:aud", "sts.amazonaws.com" }
                        },
                        ["StringLike"] = new Dictionary<string, string>
                        {
                            { "token.actions.githubusercontent.com:sub", $"repo:{githubRepo}:*" }
                        }
                    },
                    "sts:AssumeRoleWithWebIdentity"
                ),
                MaxSessionDuration = Duration.Hours(1)
            });

            // Attach CDK deployment permissions
            Role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
            {
                Effect = Effect.ALLOW,
                Actions = new[]

                {
                    "ssm:GetParameter",
        "ssm:GetParameters",
        "ssm:GetParametersByPath",
                    "cloudformation:*",
                    "s3:*",
                    "lambda:*",
                    "dynamodb:*",
                    "appsync:*",
                    "cognito-idp:*",
                    "rekognition:*",
                    
                },
                Resources = new[] { "*" }
            }));
//             Role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
// {
//     Effect = Effect.ALLOW,
//     Actions = new[]
//     {
//         "sts:AssumeRole"
//     },
//     Resources = new[]
//     {
//         $"arn:aws:iam::{Aws.ACCOUNT_ID}:role/cdk-*"
//     }
// }));

            Role.AddToPolicy(new PolicyStatement(new PolicyStatementProps
    {
        Effect = Effect.ALLOW,
        Actions = new[] { "ssm:GetParameter" },
        Resources = new[] 
        { 
            $"arn:aws:ssm:{Aws.REGION}:{Aws.ACCOUNT_ID}:parameter/cdk-bootstrap/*"
        }
    }));

            

            
        }
    }
}
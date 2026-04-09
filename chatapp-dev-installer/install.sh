#!/bin/bash



echo "ChatApp Dev Infrastructure Installer"
echo "------------------------------------"

set -e

STACK_NAME="ChatApp-dev"
TEMPLATE="cloud-assembly/ChatApp-dev.template.json"
ASSETS="cloud-assembly/ChatApp-dev.assets.json"

echo "Checking AWS credentials..."

if ! aws sts get-caller-identity > /dev/null 2>&1; then
  echo "AWS credentials not configured."
  exit 1
fi

AWS_REGION=$(aws configure get region)

echo "Publishing CDK assets..."

npx cdk-assets publish --path $ASSETS

echo "Creating deployment bucket..."

DEPLOY_BUCKET="chatapp-cfn-deploy-$AWS_REGION"

aws s3api create-bucket \
  --bucket $DEPLOY_BUCKET \
  --region $AWS_REGION \
  --create-bucket-configuration LocationConstraint=$AWS_REGION \
  2>/dev/null || true

echo "Deploying infrastructure..."
echo "Deploying infrastructure......."

aws cloudformation deploy \
  --region $AWS_REGION \
  --template-file $TEMPLATE \
  --stack-name $STACK_NAME \
  --s3-bucket $DEPLOY_BUCKET \
  --capabilities CAPABILITY_NAMED_IAM

echo ""
echo "Retrieving outputs..."

CLOUDFRONT_URL=$(aws cloudformation describe-stacks \
  --stack-name $STACK_NAME \
  --query "Stacks[0].Outputs[?OutputKey=='CloudFrontURL'].OutputValue" \
  --output text)

echo ""
echo "------------------------------------"
echo "ChatApp deployed successfully"
echo "------------------------------------"
echo ""
echo "CloudFront URL:"
echo $CLOUDFRONT_URL
echo ""

# #!/bin/bash

# echo "ChatApp Dev Infrastructure Installer"
# echo "------------------------------------"

# set -e

# STACK_NAME="ChatApp-dev"
# TEMPLATE="cloud-assembly/ChatApp-dev.template.json"
# ASSETS="cloud-assembly/ChatApp-dev.assets.json"

# echo "Checking AWS credentials..."

# if ! aws sts get-caller-identity > /dev/null 2>&1; then
#   echo "AWS credentials not configured."
#   echo "Run 'aws configure' first."
#   exit 1
# fi

# echo "Publishing CDK assets..."

# npx cdk-assets publish --path $ASSETS

# echo "Deploying CloudFormation stack..."

# aws cloudformation deploy \
#   --template-file $TEMPLATE \
#   --stack-name $STACK_NAME \
#   --capabilities CAPABILITY_NAMED_IAM

# echo ""
# echo "Retrieving deployment outputs..."

# FRONTEND_URL=$(aws cloudformation describe-stacks \
#   --stack-name $STACK_NAME \
#   --query "Stacks[0].Outputs[?OutputKey=='CloudFrontURL'].OutputValue" \
#   --output text)

# echo ""
# echo "Deployment completed successfully."
# echo ""
# echo "Open the Chat App here:"
# echo $FRONTEND_URL



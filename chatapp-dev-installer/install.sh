#!/bin/bash
echo "ChatApp Dev Infrastructure Installer"
echo "------------------------------------"

set -e

STACK_NAME="ChatApp-dev"
TEMPLATE="templates/ChatApp-dev.template.json"

echo "Checking AWS credentials..."

if ! aws sts get-caller-identity > /dev/null 2>&1; then
  echo "AWS credentials not configured."
  echo "Run 'aws configure' or set your AWS credentials before running this installer."
  exit 1
fi

echo "AWS credentials verified."

echo "Deploying ChatApp Dev infrastructure..."

aws cloudformation deploy \
  --template-file $TEMPLATE \
  --stack-name $STACK_NAME \
  --capabilities CAPABILITY_NAMED_IAM

echo "Deployment completed successfully."
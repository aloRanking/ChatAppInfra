# ChatApp Dev Infrastructure Installer

This package installs the **ChatApp development infrastructure** in AWS using a pre-generated CloudFormation template.

The infrastructure was generated from an AWS CDK application using `cdk synth`.

## Requirements

Before running the installer, ensure the following are installed and configured:

- AWS CLI
- An AWS account
- AWS credentials configured locally

Verify your AWS credentials:

```bash
aws sts get-caller-identity
```

If this command returns your AWS account details, you are ready to deploy.

## Installation Steps

1. Unzip the installer package.

2. Navigate to the installer directory.

```bash
cd chatapp-dev-installer
```

3. Run the installer script.

```bash
./install.sh
```

The script will deploy the **ChatApp-dev** CloudFormation stack.

## What This Installer Creates

The deployment provisions the development infrastructure for ChatApp, including:

- AWS AppSync GraphQL API
- DynamoDB tables
- IAM roles and permissions
- GraphQL schema and resolvers

All resources are deployed as a CloudFormation stack named:

```
ChatApp-dev
```

## Updating the Stack

To update the infrastructure after changes, simply run:

```bash
./install.sh
```

CloudFormation will update the existing stack.

## Removing the Infrastructure

To delete the development environment:

```bash
aws cloudformation delete-stack --stack-name ChatApp-dev
```

You can monitor deletion progress in the AWS CloudFormation console.

## Notes

- This installer deploys the **development environment only**.
- Production infrastructure should be deployed using the production installer.
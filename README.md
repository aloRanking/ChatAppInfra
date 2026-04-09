# Welcome to your CDK C# project!

This is a blank project for CDK development with C#.

The `cdk.json` file tells the CDK Toolkit how to execute your app.

It uses the [.NET CLI](https://docs.microsoft.com/dotnet/articles/core/) to compile and execute your project.

## Useful commands

* `dotnet build src` compile this app
* `cdk deploy`       deploy this stack to your default AWS account/region
* `cdk diff`         compare deployed stack with current state
* `cdk synth`        emits the synthesized CloudFormation template


* `cp -R cdk.out/* chatapp-dev-installer/cloud-assembly/` copy files from cdk.out to chatapp-dev-installer/cloud-assenbly
* `chmod +x chatapp-dev-installer/install.sh` make install.sh executable
* `zip -r chatapp-dev-installer.zip chatapp-dev-installer` zip chatapp-dev-installer
* `unzip chatapp-dev-installer.zip` unzip chatapp-dev-installer
using System.Collections.Generic;
using System.Threading.Tasks;
using Pulumi;
using VnTraining.Components;

return await Deployment.RunAsync(() =>
{
    var config     = new Config();
    var namePrefix = config.Require("namePrefix");
    var cidrBlock  = config.Require("cidrBlock");
    var costCenter = config.Require("costCenter");

    var vpc = new SecureVpc("app-network", new SecureVpcArgs
    {
        CidrBlock  = cidrBlock,
        CostCenter = costCenter,
    });

    // TODO 1 — solution
    var bucket = new SecureBucket("app-storage", new SecureBucketArgs
    {
        VpcId      = vpc.VpcId,
        NamePrefix = namePrefix,
    }, new ComponentResourceOptions
    {
        Protect   = true,
        DependsOn = new InputList<Resource> { vpc },
    });

    // TODO 2 — solution
    return new Dictionary<string, object?>
    {
        ["vpcId"]      = vpc.VpcId,
        ["bucketName"] = bucket.BucketName,
        ["bucketArn"]  = bucket.BucketArn,
        ["kmsKeyArn"]  = bucket.KmsKeyArn,
    };
});

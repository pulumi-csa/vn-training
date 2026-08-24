using System.Collections.Generic;
using System.Threading.Tasks;
using Pulumi;
using VnTraining.Components;

return await Deployment.RunAsync(() =>
{
    // ── Config ────────────────────────────────────────────────────────────────
    // Stack-specific values come from Pulumi.dev.yaml.
    // The ESC environment listed under `environment:` in that file also injects
    // values here — costCenter arrives identically to any other config key even
    // though it lives in a shared ESC environment, not in this file.
    var config     = new Config();
    var namePrefix = config.Require("namePrefix");   // Pulumi.dev.yaml
    var cidrBlock  = config.Require("cidrBlock");    // Pulumi.dev.yaml
    var costCenter = config.Require("costCenter");   // injected by ESC environment

    // ── SecureVpc ─────────────────────────────────────────────────────────────
    // One call creates a VPC and a subnet. Both show up in `pulumi up` nested
    // under this component because the component sets Parent = this internally.
    //
    // vpc.VpcId is an Output<string> — a value that won't be known until AWS
    // assigns it at deploy time. We can pass it to other resources right now
    // without awaiting it; Pulumi resolves the dependency graph for us.
    var vpc = new SecureVpc("app-network", new SecureVpcArgs
    {
        CidrBlock  = cidrBlock,
        CostCenter = costCenter,
    });

        // ── SecureBucket — YOUR TURN ──────────────────────────────────────────────
    //
    // Open Components/SecureBucket.cs and read through it before you start.
    // Notice:
    //   - The three child resources it creates (KMS key, bucket, encryption config)
    //   - How Parent = this groups them under the component in the CLI output
    //   - The DependsOn inside the component — same option, different call site
    //
    // Then complete the two TODOs below.

    // TODO 1 — Instantiate SecureBucket
    //
    // Create a SecureBucket named "app-storage". Pass:
    //   VpcId      = vpc.VpcId      ← Output<string> flows straight across; no unwrapping
    //   NamePrefix = namePrefix
    //
    // Also set two resource options on this component (ComponentResourceOptions):
    //
    //   Protect = true
    //     Prevents `pulumi destroy` from deleting this resource until you explicitly
    //     remove the protection. Use it for anything painful to recreate.
    //
    //   DependsOn = new InputList<Resource> { vpc }
    //     Adds an explicit ordering edge: Pulumi won't start creating the bucket
    //     until the VPC component is fully up. VpcId already implies a dependency,
    //     but DependsOn makes the intent visible to anyone reading Program.cs.
    //

    // Stack output: exposed to `pulumi stack output` and consumable by other
    // stacks via StackReference.
    var outputs = new Dictionary<string, object?>
    {
        ["vpcId"] = vpc.VpcId,
    };

    // TODO 2 — Add bucket outputs
    //
    // Add three entries to the `outputs` dictionary so they appear in
    // `pulumi stack output` after deployment:
    //   "bucketName" = bucket.BucketName
    //   "bucketArn"  = bucket.BucketArn
    //   "kmsKeyArn"  = bucket.KmsKeyArn



    return outputs;
});

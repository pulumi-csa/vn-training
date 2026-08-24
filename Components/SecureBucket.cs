using System.Collections.Generic;
using Pulumi;
using Pulumi.Aws.Kms;
using Pulumi.Aws.S3;

namespace VnTraining.Components
{
    /// <summary>
    /// Arguments for the SecureBucket component.
    /// </summary>
    public sealed class SecureBucketArgs : ResourceArgs
    {
        /// <summary>
        /// The VPC ID from SecureVpc.VpcId.
        ///
        /// S3 isn't network-scoped the way EC2 is, but accepting an Output<string>
        /// from another component demonstrates the core pattern: one component's
        /// output becomes another component's input, and Pulumi tracks the dependency
        /// automatically. You'd use the same approach for an RDS instance, an ALB,
        /// or any resource that genuinely needs to know its network context.
        /// </summary>
        [Input("vpcId", required: true)]
        public Input<string> VpcId { get; set; } = null!;

        /// <summary>Prefix for the bucket name. Must be lowercase and URL-safe.</summary>
        [Input("namePrefix", required: true)]
        public Input<string> NamePrefix { get; set; } = null!;
    }

    /// <summary>
    /// A pre-built component that creates a KMS-encrypted S3 bucket.
    ///
    /// Three child resources live inside this component:
    ///   1. aws:kms:Key               — a dedicated Customer Managed Key (CMK)
    ///   2. aws:s3:BucketV2           — the bucket itself
    ///   3. aws:s3:BucketServerSideEncryptionConfigurationV2
    ///                                — wires the CMK to the bucket
    ///
    /// Resource options to notice inside the component:
    ///   Parent = this        groups children under the component in `pulumi up` output
    ///   DependsOn on the     makes Pulumi wait for the key before configuring
    ///   encryption config    encryption — even though the BucketId reference already
    ///                        implies ordering, the key ARN is resolved via Output,
    ///                        so the explicit edge makes the intent clear.
    ///
    /// You will set additional resource options (Protect, DependsOn) at the call site
    /// in Program.cs — that's the hands-on part of this exercise.
    /// </summary>
    public class SecureBucket : ComponentResource
    {
        /// <summary>The S3 bucket name (the AWS-assigned name, not the ARN).</summary>
        public Output<string> BucketName { get; }

        /// <summary>Full ARN of the bucket, e.g. arn:aws:s3:::VN-app-abc123.</summary>
        public Output<string> BucketArn { get; }

        /// <summary>ARN of the KMS key used to encrypt objects in this bucket.</summary>
        public Output<string> KmsKeyArn { get; }

        public SecureBucket(string name, SecureBucketArgs args, ComponentResourceOptions? opts = null)
            : base("VN:storage:SecureBucket", name, opts)
        {
            // ── 1. Customer Managed KMS Key ───────────────────────────────────
            // A dedicated CMK gives per-bucket key rotation and audit trails in
            // CloudTrail that you don't get with the AWS-managed key (aws/s3).
            var key = new Key($"{name}-key", new KeyArgs
            {
                Description            = args.NamePrefix.Apply(p => $"{p} bucket encryption key"),
                DeletionWindowInDays   = 7,   // minimum; fine for a training environment
                EnableKeyRotation      = true,
                Tags = new InputMap<string>
                {
                    ["Name"] = $"{name}-key",
                },
            }, new CustomResourceOptions { Parent = this });

            // ── 2. S3 Bucket ──────────────────────────────────────────────────
            // BucketPrefix lets AWS append a unique suffix so the name is globally
            // unique without us hard-coding a random string.
            var bucket = new BucketV2($"{name}-bucket", new BucketV2Args
            {
                BucketPrefix = args.NamePrefix.Apply(p => $"{p}-secure-"),
                ForceDestroy = true, // allows clean teardown in training; remove in prod
                Tags = new InputMap<string>
                {
                    ["Name"]       = $"{name}-bucket",
                    // Record the VPC context as a tag so the relationship is visible
                    // in the AWS console — the pattern matters even when S3 doesn't
                    // enforce a network boundary.
                    ["VpcContext"] = args.VpcId,
                },
            }, new CustomResourceOptions { Parent = this });

            // ── 3. Server-Side Encryption Configuration ───────────────────────
            // Modelled as a separate resource in the Pulumi AWS provider because
            // it maps to a separate AWS API call — the same reason CDK's
            // Bucket.encryptionKey creates its own child construct.
            var encryption = new BucketServerSideEncryptionConfigurationV2(
                $"{name}-encryption",
                new BucketServerSideEncryptionConfigurationV2Args
                {
                    Bucket = bucket.Id,
                    Rules = new[]
                    {
                        new Pulumi.Aws.S3.Inputs.BucketServerSideEncryptionConfigurationV2RuleArgs
                        {
                            ApplyServerSideEncryptionByDefault =
                                new Pulumi.Aws.S3.Inputs.BucketServerSideEncryptionConfigurationV2RuleApplyServerSideEncryptionByDefaultArgs
                                {
                                    SseAlgorithm   = "aws:kms",
                                    KmsMasterKeyId = key.Arn,
                                },
                            BucketKeyEnabled = true, // reduces KMS API call volume and cost
                        },
                    },
                },
                new CustomResourceOptions
                {
                    Parent    = this,
                    // The bucket must exist before we can configure encryption on it.
                    // The Output reference to bucket.Id already implies ordering, but
                    // the explicit DependsOn makes the intent unambiguous to readers.
                    DependsOn = new InputList<Resource> { bucket },
                }
            );

            BucketName = bucket.Id;
            BucketArn  = bucket.Arn;
            KmsKeyArn  = key.Arn;

            RegisterOutputs(new Dictionary<string, object?>
            {
                ["bucketName"] = BucketName,
                ["bucketArn"]  = BucketArn,
                ["kmsKeyArn"]  = KmsKeyArn,
            });
        }
    }
}

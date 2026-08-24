using System.Collections.Generic;
using Pulumi;
using Pulumi.Aws.Ec2;

namespace VnTraining.Components
{
    /// <summary>
    /// Arguments for the SecureVpc component.
    /// </summary>
    public sealed class SecureVpcArgs : ResourceArgs
    {
        /// <summary>CIDR block for the VPC, e.g. "10.0.0.0/16".</summary>
        [Input("cidrBlock", required: true)]
        public Input<string> CidrBlock { get; set; } = null!;

        /// <summary>
        /// A short environment/team tag applied to every child resource.
        /// Comes from the shared ESC environment so every stack uses the same value.
        /// </summary>
        [Input("costCenter", required: true)]
        public Input<string> CostCenter { get; set; } = null!;
    }

    /// <summary>
    /// A pre-built component that creates a VPC with DNS enabled and one public subnet.
    ///
    /// All child resources are parented to this component, which means:
    ///   - `pulumi up` shows them nested under "VN:net:SecureVpc" in the tree
    ///   - deleting the component deletes the children automatically
    ///
    /// Participants: you will INSTANTIATE this in Program.cs — you don't need to change it.
    /// </summary>
    public class SecureVpc : ComponentResource
    {
        /// <summary>The AWS VPC ID, available as an Output to pass to other resources.</summary>
        public Output<string> VpcId { get; }

        /// <summary>The ID of the single public subnet created inside the VPC.</summary>
        public Output<string> SubnetId { get; }

        public SecureVpc(string name, SecureVpcArgs args, ComponentResourceOptions? opts = null)
            : base("VN:net:SecureVpc", name, opts)
        {
            // Shared options: every child resource is parented to this component.
            // Parent wires the dependency graph and groups resources in the CLI output.
            var childOpts = new CustomResourceOptions { Parent = this };

            var vpc = new Vpc($"{name}-vpc", new VpcArgs
            {
                CidrBlock = args.CidrBlock,
                EnableDnsHostnames = true,
                EnableDnsSupport = true,
                Tags = args.CostCenter.Apply(cc => new Dictionary<string, string>
                {
                    ["Name"] = $"{name}-vpc",
                    ["CostCenter"] = cc,
                }),
            }, childOpts);

            // One public subnet carved from the VPC's CIDR.
            // Using the first /24 of whatever CIDR was provided.
            var subnet = new Subnet($"{name}-subnet", new SubnetArgs
            {
                VpcId = vpc.Id,
                // Derive a /24 from the VPC CIDR: replace the last two octets.
                CidrBlock = args.CidrBlock.Apply(cidr =>
                {
                    // e.g. "10.0.0.0/16" -> "10.0.1.0/24"
                    var parts = cidr.Split('.');
                    return $"{parts[0]}.{parts[1]}.1.0/24";
                }),
                MapPublicIpOnLaunch = false,
                Tags = new InputMap<string>
                {
                    ["Name"] = $"{name}-subnet",
                },
            }, childOpts);

            VpcId = vpc.Id;
            SubnetId = subnet.Id;

            RegisterOutputs(new Dictionary<string, object?>
            {
                ["vpcId"] = VpcId,
                ["subnetId"] = SubnetId,
            });
        }
    }
}

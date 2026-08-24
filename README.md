# VN Pulumi Training — Hands-On Exercise

This is the hands-on follow-up to the intro slides on the Pulumi model
(projects, stacks, ESC), resource properties vs. resource options, and
component resources.

**Both components are already written.** The exercise has two phases:

1. **Walk-through (trainer-led)** — the VPC is fully wired in `Program.cs`.
   Read through the code, ask questions, then run `pulumi up` to deploy it.
2. **Hands-on** — add a KMS-encrypted S3 bucket by instantiating the
   pre-built `SecureBucket` component. You'll set resource options at the
   call site and export the outputs.

---

## What you'll build

One stack (`dev`) with two components:

```
dev
├── VN:net:SecureVpc          "app-network"    ← walk-through
│   ├── aws:ec2:Vpc                "app-network-vpc"
│   └── aws:ec2:Subnet             "app-network-subnet"
└── VN:storage:SecureBucket   "app-storage"    ← you build this
    ├── aws:kms:Key                "app-storage-key"
    ├── aws:s3:BucketV2            "app-storage-bucket"
    └── aws:s3:BucketServerSideEncryptionConfigurationV2
                                   "app-storage-encryption"
```

---

## Prerequisites

| Tool            | Version    | Install                               |
| --------------- | ---------- | ------------------------------------- |
| Pulumi CLI      | ≥ 3.x      | https://www.pulumi.com/docs/install/  |
| .NET SDK        | ≥ 8.0      | https://dotnet.microsoft.com/download |
| AWS credentials | any method | `aws configure` or env vars           |

Log in to Pulumi Cloud before starting:

```bash
pulumi login
```

---

## Phase 1 — Walk-through (trainer-led)

### 1. Initialize the stack

```bash
cd vn-training
pulumi stack init ${yourname}-dev
```

### 2. Stand up the stack with the VPC

```bash
pulumi preview
```

Read the tree: two resources nested under the component. Then deploy:

```bash
pulumi up
```

Watch the output — child resources appear indented under `SecureVpc`. After it
finishes:

```bash
pulumi stack output
# vpcId: vpc-0abc…
```

---

## Phase 2 — Hands-on: add a SecureBucket

### 4. Read the component first

Open **`Components/SecureBucket.cs`**. Before writing any code, notice:

- Three child resources: a KMS key, an S3 bucket, and an encryption config.
  The encryption config is a separate resource because it maps to a separate
  AWS API call — the same reason CDK's `Bucket.encryptionKey` creates its own
  child construct.
- `Parent = this` on every child — same pattern as `SecureVpc`.
- `DependsOn` on the encryption config — an example of resource options
  _inside_ a component. You'll set different options _outside_ in Program.cs.
- `SecureBucketArgs` accepts `VpcId` — an `Output<string>` from `SecureVpc`.
  S3 isn't network-scoped, but passing the output demonstrates the pattern and
  Pulumi records the dependency in the graph.

### 5. Complete the two TODOs in `Program.cs`

**TODO 1** — Instantiate `SecureBucket`:

```csharp
var bucket = new SecureBucket("app-storage", new SecureBucketArgs
{
    VpcId      = vpc.VpcId,
    NamePrefix = namePrefix,
}, new ComponentResourceOptions
{
    Protect   = true,
    DependsOn = new InputList<Resource> { vpc },
});
```

Two resource options to understand:

- **`Protect = true`** — `pulumi destroy` will refuse to delete this resource
  until you explicitly remove the protection
  (`pulumi state unprotect <urn>`). Use it for anything painful to recreate.

- **`DependsOn = new InputList<Resource> { vpc }`** — an explicit ordering
  edge. `vpc.VpcId` already implies a dependency, but `DependsOn` makes the
  intent visible to anyone reading `Program.cs` — and handles cases where the
  dependency isn't expressed through an Output at all.

**TODO 2** — Add bucket outputs to the `outputs` dictionary:

```csharp
outputs["bucketName"] = bucket.BucketName;
outputs["bucketArn"]  = bucket.BucketArn;
outputs["kmsKeyArn"]  = bucket.KmsKeyArn;
```

### 6. Preview and deploy

```bash
pulumi preview
```

You should now see both components in the tree — five resources under
`SecureVpc` and `SecureBucket` respectively. Then:

```bash
pulumi up
pulumi stack output
```

### 7. Tear down

Because the bucket has `Protect = true`, destroy will fail until you remove the
protection:

```bash
# Copy the exact URN from `pulumi stack --show-urns`
pulumi state unprotect "urn:pulumi:dev::vn-training::VN:storage:SecureBucket::app-storage"
pulumi destroy
```

---

## Concepts map — tying each step back to the slides

| Step                                       | Concept                                                                    |
| ------------------------------------------ | -------------------------------------------------------------------------- |
| `Pulumi.dev.yaml` config values            | Per-stack config                                                           |
| `environment:` block in `Pulumi.dev.yaml`  | ESC as a shared config layer; `costCenter` is just another config key here |
| `new SecureVpc(…)` / `new SecureBucket(…)` | ComponentResource — instantiate, don't wire internals                      |
| `vpc.VpcId` passed to `SecureBucketArgs`   | Output flowing between resources; Pulumi tracks the dependency             |
| `Protect`, `DependsOn` on `SecureBucket`   | Resource options set at the call site, not inside the component            |
| `Parent = this` inside components          | What creates the nested tree in `pulumi up` output                         |
| `DependsOn` inside `SecureBucket.cs`       | Same option, different call site — components can set options too          |
| `pulumi stack output`                      | Stack outputs as the public API                                            |

---

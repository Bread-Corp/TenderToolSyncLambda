# 🔄 Tender Tool Data Sync Lambda

[![AWS Lambda](https://img.shields.io/badge/AWS-Lambda-orange.svg)](https://aws.amazon.com/lambda/)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![Amazon RDS](https://img.shields.io/badge/AWS-RDS-informational.svg)](https://aws.amazon.com/rds/)
[![Amazon OpenSearch](https://img.shields.io/badge/AWS-OpenSearch-blueviolet.svg)](https://aws.amazon.com/opensearch-service/)

This is a **utility microservice** for the Tender Tool project. Its sole purpose is to perform a one-time (or re-runnable) "full load" data synchronization, copying and "flattening" all tender data from our primary SQL Server (RDS) database into our Amazon OpenSearch cluster.

This function is triggered **manually** via the AWS Lambda "Test" button and is not part of the main, automated data-processing pipeline.

## 📚 Table of Contents

- [🎯 Project Purpose](#-project-purpose)
- [📋 Project History & Key Decisions](#-project-history--key-decisions)
- [🧭 Architecture & Data Flow](#-architecture--data-flow)
- [🧠 How It Works: The Sync Logic](#-how-it-works-the-sync-logic)
- [🧩 Project Structure](#-project-structure)
- [🚀 How to Run the Sync (Guide)](#-how-to-run-the-sync-guide)
- [🔒 How to Access the OpenSearch Dashboard (Guide)](#-how-to-access-the-opensearch-dashboard-guide)
- [⚙️ Networking & IAM](#️-networking--iam)
- [📦 Tech Stack](#-tech-stack)
- [🧰 Troubleshooting & Team Gotchas](#-troubleshooting--team-gotchas)
- [🔜 Next Steps](#-next-steps)

## 🎯 Project Purpose

The goal was to add advanced search capabilities to our front end. To do this, we needed to get our relational data from RDS into a dedicated search engine. This `SyncLambda` is the bridge that moves that data.

It reads from all `BaseTender`, child (`EskomTender`, `SanralTender`, etc.), `Tags`, and `SupportingDocs` tables and flattens them into a single, comprehensive `TenderSearchDocument` object in OpenSearch. This allows the front end to perform fast, powerful, full-text searches across all tender properties and tags in a single query.

## 📋 Project History & Key Decisions

This project is the result of a critical technical pivot.

1. **The Goal:** We needed to sync our RDS database with a new OpenSearch cluster to enable advanced search.
2. **Attempt #1: AWS DMS (Database Migration Service):** Our initial plan was to use AWS DMS, a "no-code" console-based service.
3. **The Roadblock:** We discovered that our Free Tier RDS instance runs **SQL Server Express Edition**. DMS requires Change Data Capture (CDC) for ongoing replication, but **CDC is not supported on SQL Server Express**. This made DMS unusable for our needs.
4. **The Pivot (This Project):** Instead of being blocked by a "black box" service, we pivoted to a developer-controlled solution. This Lambda function gives us 100% control over the data sync. We use **Entity Framework Core** (which we already know) to read from RDS and the **OpenSearch .NET Client** to perform a bulk upload. This approach is more transparent, flexible, and works perfectly with our existing database.

## 🧭 Architecture & Data Flow

The architecture for this utility is simple and manually triggered:

```
YOU (Developer)
   |
   ├─ 1. Click "Test" in AWS Lambda Console
   ↓
TenderToolSyncLambda
   │
   ├─ 1. (DELETED) Old "tenders" index in OpenSearch is deleted (for a clean sync)
   │
   ├─ 2. Connects to RDS via EF Core
   │  (Runs 1 query for BaseTenders + 5 queries for child tables)
   │
   ├─ 3. Transforms data in-memory
   │  (Loops through 538 BaseTenders, maps child data)
   │
   ├─ 4. Connects to OpenSearch Cluster
   │  (Bulk-uploads 538 "flattened" TenderSearchDocument objects)
   │
   └─ 5. Logs "SYNC PROCESS SUCCEEDED"
   |
   ↓
Amazon OpenSearch
(Now contains 538 searchable documents)
```

## 🧠 How It Works: The Sync Logic

The entire logic is contained in `SyncController.cs`. When triggered, it performs these steps:

1. **Connects** to both the `ApplicationDbContext` (RDS) and the `IOpenSearchClient`.
2. **(DELETED):** The "future-proof" logic was re-added. The function no longer deletes the index.
3. **Reads All Data:** It loads *all* data into memory for high performance:
   - `_dbContext.Tenders.Include(t => t.Tags).Include(t => t.SupportingDocs)`
   - `_dbContext.eTenders.ToDictionaryAsync()`
   - `_dbContext.EskomTenders.ToDictionaryAsync()`
   - `_dbContext.SanralTenders.ToDictionaryAsync()`
   - `_dbContext.SarsTenders.ToDictionaryAsync()`
   - `_dbContext.TransnetTenders.ToDictionaryAsync()`
4. **Transforms (Flattens):** It loops through every `BaseTender` and uses the dictionaries to efficiently find and map the child-specific data into the "master" `TenderSearchDocument` model.
5. **Bulk Upserts:** It uploads the entire list of 538 documents to the `tenders` index in OpenSearch. Because we provide the `TenderID` as the document ID, this is an **"upsert"** (create new or update existing). This makes the function safe to re-run at any time to sync new data.

## 🧩 Project Structure

```
TenderToolSyncLambda/
├── Controllers/
│   └── SyncController.cs         # The main (and only) controller. Contains all sync logic.
├── Data/
│   └── ApplicationDbContext.cs   # (Copied) EF Core context for RDS.
├── Models/
│   ├── BaseTender.cs             # (Copied) All database models...
│   ├── eTender.cs
│   ├── ...etc...
│   └── TenderSearchDocument.cs   # The NEW flattened model for OpenSearch.
├── Properties/
│   └── launchSettings.json
├── appsettings.json              # Contains ConnectionString and OpenSearch Endpoint URL.
├── LambdaEntryPoint.cs           #
├── LocalEntryPoint.cs            #
├── Startup.cs                    # Configures DI for JSON logging, DbContext, and OpenSearchClient.
└── serverless.template           # CRITICAL: Configures VPC, Subnets, Security Groups, and IAM Role.
```

## 🚀 How to Run the Sync (Guide)

This is a **one-time, manual task** to populate the database.

### Step 1: Deploy the Lambda

- Ensure `appsettings.json` has the correct RDS Connection String and OpenSearch Endpoint URL.
- Ensure `serverless.template` has the correct VPC, Subnet, and Security Group IDs.
- Right-click the project in Visual Studio → **"Publish AWS Serverless Application..."**.
- Use a new stack name (e.g., `tender-tool-sync-stack`).
- Wait for the CloudFormation stack to reach **`CREATE_COMPLETE`**.

### Step 2: Configure the Test Event

- Go to the **AWS Lambda Console** and find your new function (e.g., `tender-tool-sync-stack-AspNetCoreFunction...`).
- Click the **"Test"** tab.
- Select **"Create new event"**.
- **Event name:** `RunSync`
- **Template:** `Amazon API Gateway AWS Proxy`
- **Event JSON:** Delete everything and paste in this minimal JSON:

```json
{
  "httpMethod": "POST",
  "path": "/sync/start",
  "body": "{}"
}
```

- Click **"Save"**.

### Step 3: Run the Sync

- Make sure `RunSync` is the selected event.
- Click the orange **"Test"** button.
- **Be patient.** This function has a 10-minute timeout for a reason. It is reading and processing your entire database. It will show "Executing..." for several minutes.

### Step 4: Check the Logs

- When it finishes, check the CloudWatch Logs.
- You are looking for the final message: **`--- SYNC PROCESS SUCCEEDED ---`**.

## 🔒 How to Access the OpenSearch Dashboard (Guide)

The OpenSearch cluster is **100% private** in our VPC. You cannot access the dashboard from your browser directly. You must use our `tender-tool-bastion` host to create a secure SSH tunnel.

### Step 1: Start the Bastion Host

1. Go to the **EC2 Console**.
2. Find and **"Start"** the `tender-tool-bastion` instance.
3. Wait for it to be "Running" and copy its **Public IPv4 address**.

### Step 2: Start the SSH Tunnel

1. Open a local terminal (PowerShell, CMD, etc.).
2. Run the following `ssh` command. You must:
   - Provide the correct path to your `.pem` key file.
   - Provide the new **Public IPv4 address** of the bastion.

```bash
ssh -i "C:\path\to\your\tender-tool-bastion-key.pem" -N -L 8443:vpc-tender-tool-search-m2hyjgolvayz42ki2zjhq3atly.us-east-1.es.amazonaws.com:443 ec2-user@[YOUR_BASTION_PUBLIC_IP]
```

3. If it asks to continue connecting, type `yes` and press Enter.
4. The terminal will sit and "hang." This is correct; the tunnel is now open. **Keep this terminal running.**

### Step 3: Access the Dashboard in Your Browser

1. Open your browser (Chrome, Firefox).
2. Go to this exact URL: **`https://localhost:8443/_dashboards`**
3. Your browser will show a security warning ("Your connection is not private"). This is safe and expected.
4. Click **"Advanced"** and **"Proceed to localhost (unsafe)"**.
5. You will now see the OpenSearch login page. Log in with your master user credentials (e.g., `opensearch_admin`).

### Step 4: Verify the Data in Dev Tools

1. Once logged in, click the "hamburger" menu (☰) → **"Dev Tools"**.
2. To see if your index exists, run:

```json
GET /_cat/indices?v
```

- You should see a `tenders` index with `docs.count: 538`.

3. To see your actual data, run:

```json
GET /tenders/_search
{
  "query": { "match_all": {} }
}
```

## ⚙️ Networking & IAM

- **VPC:** The `serverless.template` configures this Lambda to run inside our VPC (`vpc-0e6df682e377ffb63`), in the private subnets (`subnet-0f4...`, `subnet-072a...`).
- **Security Group:** It uses our main `rds-all-access` security group, which gives it network access to both the RDS database and the OpenSearch cluster.
- **IAM Role:** The template creates an IAM role with two policies:
  1. `AWSLambdaVPCAccessExecutionRole`: Allows it to run in the VPC and write logs.
  2. `es:*`: A custom policy giving it full permissions on our `tender-tool-search` domain.
- **Internal OpenSearch Security:** For the Lambda to work, its IAM Role (`tender-tool-sync-stack-AspNetCoreFunctionRole-...`) **must** be mapped to the `all_access` role inside the OpenSearch Dashboard (see Step 4 of the Access Guide).

## 📦 Tech Stack

- **.NET 8** (LTS)
- **Compute**: AWS Lambda
- **Database**: Amazon RDS (SQL Server Express)
- **Search Engine**: Amazon OpenSearch Service
- **ORM**: Entity Framework Core
- **OpenSearch Client**: OpenSearch .NET Client
- **Networking**: AWS VPC, Private Subnets, Security Groups
- **Logging**: `Microsoft.Extensions.Logging.Console` (for structured JSON logging)

## 🧰 Troubleshooting & Team Gotchas

### ⚠️ CRITICAL: OpenSearch Internal Permissions

Deploying the `SyncLambda` is not enough. The Lambda's IAM role must be mapped to the `all_access` role *inside* the OpenSearch Dashboard before it can write data.

1.  Start the **EC2 Bastion Host** and connect via **SSH Tunnel**.
2.  Log in to the **OpenSearch Dashboard** (`https://localhost:8443/_dashboards`) as your `opensearch_admin`.
3.  Go to **Security** -\> **Roles** -\> **`all_access`**.
4.  Click the **"Mapped users"** tab -\> **"Manage mapping"**.
5.  In **"Backend roles,"** add the ARN of your `SyncLambda`'s role (e.g., `arn:aws:iam::...:role/tender-tool-sync-stack-AspNetCoreFunctionRole-...`).
6.  Click **"Map"**.

You must **repeat this** for the `TenderToolSearchLambda`'s role, but map it to the **`readall`** role.


<details>
<summary><strong>ERROR: Timeout during sync (10+ minutes)</strong></summary>

**Issue**: The Lambda times out before completing the sync process.

**Reason**: The function is reading and transforming your entire database (538+ records). This is a legitimate, heavy operation.

**Fix**: Ensure the Lambda timeout is set to the maximum (10 minutes) in the `serverless.template`. If it still times out, consider breaking the sync into smaller batches.

</details>

<details>
<summary><strong>ERROR: Cannot connect to RDS from Lambda</strong></summary>

**Issue**: The Lambda fails with database connection errors.

**Reason**: VPC networking misconfiguration. The Lambda cannot reach the RDS instance.

**Fix**: Verify that:
- The Lambda is deployed in the same VPC as your RDS instance
- The Lambda's subnets have route tables pointing to a NAT Gateway (for internet access)
- The Lambda's security group allows outbound traffic to the RDS security group

</details>

<details>
<summary><strong>ERROR: OpenSearch connection refused</strong></summary>

**Issue**: The Lambda cannot connect to the OpenSearch cluster.

**Reason**: Either networking or IAM permissions issue.

**Fix**: Verify that:
- The OpenSearch cluster is in the same VPC as the Lambda
- The Lambda's IAM role has the necessary `es:*` permissions
- The OpenSearch cluster's access policy allows the Lambda's IAM role

</details>

<details>
<summary><strong>ERROR: SSH tunnel connection refused</strong></summary>

**Issue**: Cannot establish SSH tunnel to access OpenSearch dashboard.

**Reason**: Either the bastion host is stopped or the SSH key/IP is incorrect.

**Fix**: 
- Ensure the `tender-tool-bastion` EC2 instance is running
- Verify you're using the correct `.pem` key file
- Check that you're using the current public IP of the bastion host
- Ensure your local firewall allows outbound SSH connections

</details>

---

> Built with love, bread, and code by **Bread Corporation** 🦆❤️💻
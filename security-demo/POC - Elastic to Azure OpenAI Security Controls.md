# POC Build Guide — Elastic ↔ Azure OpenAI Security Controls
**Customer:** Zenith Bank
**Environment:** Elastic Cloud (ECH) — OTel demo running on GCP Kubernetes
**Audience:** Zenith Bank Security Team
**Goal:** Demonstrate that Zenith Bank controls what data is ever exposed to Azure OpenAI, and that all LLM usage is observable and auditable within Elastic.

---

## What We Are Demonstrating

Three control layers that act as a gatekeeper between Zenith Bank's data and the Azure OpenAI model:

| Layer | Where It Happens | What It Does |
|---|---|---|
| **1 — Ingestion Pipeline** | Before data enters Elastic | Strips, masks, or redacts sensitive fields at source |
| **2 — Field Filtering** | Before data is sent to the LLM | Controls exactly which fields are included in the prompt |
| **3 — Obfuscation / Hashing** | At ingest time | Hashes sensitive values so the raw value never exists in Elastic |
| **+ LLM Observability** | After the LLM call | Logs every prompt, response, token count, and latency in Elastic |

---

## Prerequisites

- Elastic Cloud (ECH) deployment running — OTel demo (opentelemetry-demo) shipping data from GCP Kubernetes
- Azure OpenAI connector already configured in Kibana (per the Azure OpenAI Requirements doc)
- Kibana Dev Tools access (Management → Dev Tools)
- Superuser or equivalent role to create ingest pipelines and security roles

> **Live demo script:** All steps below have corresponding Kibana Dev Console commands ready to paste and run.
> See `POC - Kibana Dev Console Demo Script.md` for the full step-by-step script.

### Data in the Environment

The OTel demo generates realistic e-commerce transaction data across 18 microservices (frontend, checkout, payment, cart, shipping, etc.). For the security demo this maps well to banking — payment flows, order IDs, and transaction references stand in for financial transaction data.

Key fields used in the demo:
| OTel Demo Field | Banking Equivalent | Treatment |
|---|---|---|
| `Attributes.app.payment.card.number` | Card / account number | Redacted at pipeline |
| Log `body` containing card patterns | Unstructured log data with PII | Masked via regex |
| `Attributes.app.order.id` | Transaction reference | SHA-256 hashed |
| `Attributes.app.payment.transaction.id` | Payment transaction ID | SHA-256 hashed |
| `Attributes.app.cart.items.count` | Item count | Kept — not sensitive |
| `service.name`, `span.duration.us`, `http.response.status_code` | Service telemetry | Kept — safe for LLM |

---

## Layer 1 — Ingestion Pipeline: Mask Sensitive Fields Before They Enter Elastic

The goal here is to show that a field like an account number or card number can be stripped or masked **before it is indexed** — meaning it never exists in Elastic in its raw form.

> **Dev Console:** Run the `PUT _ingest/pipeline/zenith-otel-field-masking` block, then the `POST _ingest/pipeline/zenith-otel-field-masking/_simulate` block from the demo script. The simulate call shows before/after live without touching the index.

### Step 1.1 — Create an Ingest Pipeline in Kibana

1. In Kibana, go to **Management → Dev Tools**
2. Paste and run the pipeline creation block from the demo script (`PUT _ingest/pipeline/zenith-otel-field-masking`)
3. The pipeline applies these processors:

**Gsub processor** — masks credit card numbers in log message fields:
```json
{
  "gsub": {
    "field": "message",
    "pattern": "\\b(?:\\d[ -]?){13,16}\\b",
    "replacement": "[CARD-REDACTED]",
    "ignore_missing": true
  }
}
```

**Fingerprint + Remove processors** — hash the order ID, then drop the raw value:
```json
{
  "fingerprint": {
    "fields": ["Attributes.app\\.order\\.id"],
    "method": "SHA-256",
    "target_field": "Attributes.app\\.order\\.id\\.hash",
    "ignore_missing": true
  }
},
{
  "remove": {
    "field": ["Attributes.app\\.order\\.id"],
    "ignore_missing": true
  }
}
```

### Step 1.2 — Apply the Pipeline to the OTel Traces Index Template

Run from Dev Console (from demo script):
```
PUT _index_template/traces-apm-zenith
```
This sets the pipeline as the default for all incoming OTel trace data (`traces-apm-*`).

### What to Show the Security Team
- Run the **simulate** block with a document containing a fake card number (e.g. `4539 1488 0343 6467`)
- Point to the output: `[CARD-REDACTED]` in the body, SHA-256 hashes where order/transaction IDs were, raw fields gone
- Then run the `_field_caps` query to prove the raw field doesn't exist in the live index

---

## Layer 2 — Field Filtering: Control What the LLM Receives in Its Prompt

Even if a field is stored in Elastic, you control which fields are ever constructed into a prompt sent to Azure OpenAI. This is demonstrated two ways: via a Dev Console query that mimics LLM retrieval, and via the RBAC role that restricts field visibility.

> **Dev Console:** Run the `POST /_security/role/zenith-otel-analyst` block, then the field-projected `_search` and `_field_caps` queries from the demo script.

### Step 2.1 — Show LLM Context via a Projected Search

Run from Dev Console (from demo script — "Step 3a"):
- Query the `traces-apm-default` index projecting only safe fields: `@timestamp`, `service.name`, `span.name`, `span.duration.us`, `http.response.status_code`, `error.message`, `Attributes.app.cart.items.count`
- This is exactly what Elastic constructs as context before calling Azure OpenAI

### Step 2.2 — Create the Analyst RBAC Role

Run from Dev Console:
```
POST /_security/role/zenith-otel-analyst
```
The role grants read on `traces-apm-*`, `logs-apm.app.*-default`, `metrics-apm.*-default` with an explicit field allowlist — only safe telemetry fields are accessible.

### What to Show the Security Team
- Run the projected search and expand a result — show that `app.order.id`, `app.payment.card.number`, and `app.payment.transaction.id` are absent
- Then run `_field_caps` for those fields to prove they don't exist in the index at all
- The "gatekeeper" point is made in two steps: the pipeline removes the data, and the role enforces it even if anything slipped through

---

## Layer 3 — Obfuscation / Hashing: Stored Fields the LLM Can Never Reverse

The hashing is already built into the Layer 1 pipeline — `app.order.id` and `app.payment.transaction.id` are fingerprinted with SHA-256 and the originals removed. This section explains the concept and what to highlight to the security team.

### What the Simulate Output Shows

In the pipeline simulate result (from the demo script), point to:
- `Attributes.app.order.id` → **gone** from the output document
- `Attributes.app.order.id.hash` → **present** as a 64-character hex string
- Same for `app.payment.transaction.id`

### What to Show the Security Team
- The hash is deterministic — the same order ID always produces the same hash, so you can still correlate events ("how many errors involved this order?") without storing the raw ID
- The LLM receives only the hash — it cannot reverse it to the original value
- Run the `_field_caps` query for `Attributes.app.order.id` — confirm the raw field does not exist, only `Attributes.app.order.id.hash` does

---

## LLM Observability — Every Call to Azure OpenAI Is Logged in Elastic

Elastic captures full observability over every interaction with the LLM. This gives the security team a complete audit trail.

### What Is Captured Automatically

When Elastic AI features are connected to Azure OpenAI, the following is logged to Elastic:

| Metric | Description |
|---|---|
| **Prompt sent** | The full context passed to the model |
| **Response received** | The model's output |
| **Token usage** | Input tokens, output tokens, total cost indicator |
| **Latency** | Time taken for the Azure OpenAI call |
| **Model used** | Which deployment/model was invoked |
| **Timestamp** | When the call was made |

### Step — Enable and Show Agent Builder Tracing

> **Dev Console:** Run the Agent Builder trace queries from the "BONUS" section of the demo script.

1. Go to **Management → AI → GenAI Settings** and enable:
   - **Collect conversation traces**
   - **Include user prompts in traces**
   - **Include LLM responses in traces**
2. Have a conversation with the AI Agent in Kibana
3. In Dev Console, query `traces-agent_builder.otel-default` to show the full trace including token usage

### What to Show the Security Team
- Point to the **GenAI Settings toggles** first — by default, user prompts and LLM responses are NOT captured. This is Zenith Bank's choice to make.
- Then show a conversation trace — prompt sent, response received, token counts, latency — all stored locally in Elastic
- Key message: this audit trail exists inside your Elastic environment. It is independent of whatever Microsoft retains at the Azure OpenAI endpoint.
- This demonstrates that Zenith Bank has **full visibility and auditability** of everything sent to Azure OpenAI — on their own terms, in their own data store

---

## Supporting Context: RBAC and Field-Level Security

The `zenith-otel-analyst` role created in Layer 2 already demonstrates this. If time allows, show it in action via Kibana's role management UI as a visual complement to the Dev Console commands.

1. Go to **Management → Security → Roles** — show the `zenith-otel-analyst` role and its field allowlist
2. Assign it to a test user and log in as that user
3. Run a Discover search — sensitive fields are absent from the field list entirely
4. Log back in as admin — show the full field set (hashes visible, raw values absent due to pipeline)

---

## Suggested Demo Flow for the Security Team Session

> All commands are in `POC - Kibana Dev Console Demo Script.md` — open it alongside this guide during the session.

| Step | Where | What You Show | Key Message |
|---|---|---|---|
| 0 | Dev Console | Accounting logs with full order JSON — real sensitive-looking data in Elastic | "This is your live application data — addresses, amounts, order references" |
| 1 | Dev Console | `PUT _ingest/pipeline` + `_simulate` — card masked, order ID hashed | "The pipeline is the safety net — catches leakage regardless of application behaviour" |
| 2 | Dev Console | `PUT _index_template` — apply pipeline to live indices | "This is now active for all incoming data" |
| 3 | Dev Console | Projected `_search` — only safe fields shown | "This is exactly what the AI model can see — nothing more" |
| 4 | Dev Console | `_field_caps` — prove raw fields don't exist | "There is nothing to find even if someone bypassed access controls" |
| 5 | Agent Builder | Add **Custom Instructions** — behavioural guardrails | "The agent is instructed not to surface sensitive data — second layer of defence" |
| 6 | Agent Builder | Add **Index Search Tool** scoped to sanitized indices, row limit 10 | "The agent can only query the clean data, and only retrieve 10 rows at a time" |
| 7 | Agent Builder | Test: ask for card numbers / raw order IDs — agent declines | "Three questions — one it answers, two it refuses — live in front of the security team" |
| 8 | Dashboards | **Agent Builder — Overview** — token usage, conversation throughput | "Every call to Azure OpenAI is visible here, in your environment" |
| 9 | GenAI Settings | Privacy toggles — off by default | "You decide what gets audited — not Microsoft, not Elastic" |

**Total estimated time:** 25–35 minutes

---

## Open Item: Azure Data Retention Confirmation

The above covers everything **Elastic controls**. The remaining open item is on the **Microsoft side**:

- Confirm **Zero Data Retention (ZDR)** is enabled on Zenith's Azure OpenAI resource
- This must be raised with Zenith's Azure/Microsoft account team
- Once confirmed in writing, the security team has full contractual coverage for both sides of the boundary

---

*Prepared by Jacques Vlaming — Elastic Senior Solutions Architect*

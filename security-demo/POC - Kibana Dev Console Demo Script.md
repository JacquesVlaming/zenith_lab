# Kibana Dev Console — Security Demo Script
**Customer:** Zenith Bank Security Team
**Environment:** Elastic Cloud (ECH) — OTel Demo (GCP K8s)
**Purpose:** Demonstrate the three data control layers between Elastic and Azure OpenAI

> **How to use this:** Open Kibana → Dev Console (Management → Dev Tools). Paste each block and run it with the ▶ button. Walk through each section with the security team.

---

## Step 0 — Orientate: What Data Do We Have?

First, show the security team what indices the OTel demo is producing.

```
# Show the OTel demo indices — traces, per-service logs, and metrics
GET _cat/indices/traces-apm-default,logs-apm.app.payment-default,logs-apm.app.checkout-default,logs-apm.app.frontend-default,metrics-apm.service_transaction.1m-default?h=index,docs.count,store.size&v
```

```
# Show a raw trace from the checkout service — this carries order and transaction references
GET traces-apm-default/_search
{
  "size": 1,
  "query": {
    "bool": {
      "must": [
        { "term": { "service.name": "checkout" } },
        { "term": { "processor.event": "transaction" } }
      ]
    }
  },
  "_source": true
}
```

```
# Show raw logs from the accounting service — this processes full order details
# including shipping addresses, currency amounts, and item costs
# These are the fields that need controlling before they reach an AI model
GET logs-apm.app.accounting-default/_search
{
  "size": 3,
  "query": {
    "match": { "message": "Order details" }
  },
  "_source": [
    "@timestamp",
    "service.name",
    "message",
    "log.level"
  ]
}
```

> **Talking point:** "Look at the accounting service logs — every order that flows through this system is being logged in full: the order ID, the shipping address, the currency, the item costs. This is real application data landing in Elastic right now. In a banking context, these would be your transaction records, customer addresses, and payment amounts. This is exactly what we need to control before it can be reached by an AI model. Let's show you the three layers that do that."

---

## LAYER 1 — Ingest Pipeline: Sensitive Fields Never Enter Elastic

> **Talking point:** "Before data is indexed, it passes through an ingest pipeline. We define which fields are masked, redacted, or dropped entirely. The raw value never touches disk."

### Step 1a — Create the Pipeline

```
# Create the Zenith sensitive field masking pipeline
PUT _ingest/pipeline/zenith-otel-field-masking
{
  "description": "Zenith Bank — mask sensitive fields before indexing OTel data",
  "processors": [
    {
      "gsub": {
        "description": "Mask credit card numbers in the message field",
        "field": "message",
        "pattern": "\\b(?:\\d[ -]?){13,16}\\b",
        "replacement": "[CARD-REDACTED]",
        "ignore_missing": true
      }
    },
    {
      "gsub": {
        "description": "Mask credit card numbers in the body field",
        "field": "body",
        "pattern": "\\b(?:\\d[ -]?){13,16}\\b",
        "replacement": "[CARD-REDACTED]",
        "ignore_missing": true
      }
    },
    {
      "fingerprint": {
        "description": "Hash the order ID — keeps it usable for correlation but unreadable",
        "fields": ["labels.order_id"],
        "method": "SHA-256",
        "target_field": "labels.order_id_hash",
        "ignore_missing": true
      }
    },
    {
      "remove": {
        "description": "Drop the raw order ID after hashing",
        "field": ["labels.order_id"],
        "ignore_missing": true
      }
    },
    {
      "fingerprint": {
        "description": "Hash the payment transaction ID",
        "fields": ["labels.transaction_id"],
        "method": "SHA-256",
        "target_field": "labels.transaction_id_hash",
        "ignore_missing": true
      }
    },
    {
      "remove": {
        "description": "Drop raw payment transaction ID after hashing",
        "field": ["labels.transaction_id"],
        "ignore_missing": true
      }
    }
  ]
}
```

### Step 1b — Simulate the Pipeline (Show Before vs After Live)

> **Note for presenter:** The OTel demo payment service correctly does not log raw card numbers into traces — that is good instrumentation hygiene. The simulate below demonstrates what the pipeline would do if a misconfigured application *did* accidentally leak card data. This is actually a stronger security argument: the pipeline is a safety net that catches mistakes at the infrastructure level, regardless of application behaviour.

```
# Simulate the pipeline with two realistic documents:
# 1. An accounting log (real format from this environment) containing a full order with shipping address
# 2. A misconfigured payment log that accidentally leaked a card number
POST _ingest/pipeline/zenith-otel-field-masking/_simulate
{
  "docs": [
    {
      "_source": {
        "service": { "name": "accounting" },
        "message": "Order details: { \"orderId\": \"6ecba2a4-b25d-11f1-a0de\", \"shippingCost\": { \"currencyCode\": \"USD\", \"units\": \"89\" }, \"shippingAddress\": { \"streetAddress\": \"150 Elgin St\", \"city\": \"Ottawa\", \"state\": \"ON\", \"country\": \"Canada\", \"zipCode\": \"K2P1L4\" }, \"items\": [ { \"item\": { \"productId\": \"L9ECAV7KIM\", \"quantity\": 3 }, \"cost\": { \"currencyCode\": \"USD\", \"units\": \"29\" } } ] }",
        "labels": {
          "order_id": "6ecba2a4-b25d-11f1-a0de",
          "transaction_id": "txn-8f2a1b3c-9d4e-5f6a"
        }
      }
    },
    {
      "_source": {
        "service": { "name": "payment" },
        "message": "Charging card 4539 1488 0343 6467 for order 6ecba2a4-b25d-11f1-a0de — amount USD 89.00",
        "labels": {
          "order_id": "6ecba2a4-b25d-11f1-a0de",
          "transaction_id": "txn-8f2a1b3c-9d4e-5f6a"
        }
      }
    }
  ]
}
```

> **Talking point:** "The payment service itself is well-instrumented and doesn't log card numbers — that's correct behaviour. But what if a developer made a mistake in a future release and accidentally logged card data? This pipeline is your infrastructure-level safety net. It doesn't matter what the application does — if card number patterns appear in any log or span, they are caught and redacted here before they ever reach disk. The order ID and transaction ID are also hashed — those *are* present in real checkout traces, and now they're protected."

### Step 1c — Apply the Pipeline to the Traces Index Template

```
# Apply this pipeline as the default for all incoming OTel trace data
PUT _index_template/traces-apm-zenith
{
  "index_patterns": ["traces-apm-*"],
  "priority": 500,
  "template": {
    "settings": {
      "index": {
        "default_pipeline": "zenith-otel-field-masking"
      }
    }
  }
}
```

---

## LAYER 2 — Field-Level Security: Control Who Sees What Inside Elastic

> **Talking point:** "Even within Elastic, not everyone sees everything. We can define roles that restrict which fields a user — or an AI — can access. Here we create a read-only analyst role that cannot see any remaining sensitive correlation fields."

### Step 2a — Create a Restricted Analyst Role

```
# Create a role for the Zenith security analyst
# Can query OTel data but cannot see hashed IDs or internal system fields
POST /_security/role/zenith-otel-analyst
{
  "indices": [
    {
      "names": ["traces-apm-*", "logs-apm.app.*-default", "metrics-apm.*-default"],
      "privileges": ["read", "view_index_metadata"],
      "field_security": {
        "grant": [
          "@timestamp",
          "service.name",
          "service.version",
          "service.namespace",
          "span.name",
          "span.kind",
          "span.duration.us",
          "http.response.status_code",
          "http.request.method",
          "url.path",
          "Attributes.app.cart.items.count",
          "Attributes.app.shipping.cost",
          "Attributes.app.product.id",
          "Attributes.app.product.quantity",
          "error.message",
          "event.outcome",
          "resource.attributes.k8s.*",
          "resource.attributes.host.*"
        ]
      }
    }
  ]
}
```

### Step 2b — Verify: Query as the Restricted Role

```
# Run a query and confirm sensitive fields are absent from results
GET traces-apm-default/_search
{
  "size": 3,
  "query": {
    "term": { "service.name": "checkout" }
  },
  "_source": [
    "@timestamp",
    "service.name",
    "span.name",
    "Attributes.app.order.id",
    "Attributes.app.order.id.hash",
    "Attributes.app.payment.transaction.id",
    "Attributes.app.cart.items.count"
  ]
}
```

> **Talking point:** "Notice `app.order.id` returns nothing — the raw value was dropped by the pipeline. The hash exists for internal correlation only. The analyst can see the cart count and service name — enough to do their job — but never a raw transaction identifier."

---

## LAYER 3 — LLM Context: What Azure OpenAI Actually Receives

> **Talking point:** "When an analyst asks a question through Elastic AI Assistant, Elastic retrieves relevant documents and constructs a prompt. This shows exactly what gets put in front of the model — and what doesn't."

### Step 3a — Show a Representative AI Context Query

```
# This simulates the retrieval step Elastic performs before calling the LLM
# Only safe fields are projected — this is the maximum the model can see
# Use checkout service — this is where order/transaction references actually live
GET traces-apm-default/_search
{
  "size": 5,
  "query": {
    "bool": {
      "must": [
        { "term": { "service.name": "checkout" } }
      ]
    }
  },
  "_source": [
    "@timestamp",
    "service.name",
    "transaction.name",
    "transaction.duration.us",
    "transaction.result",
    "http.response.status_code",
    "event.outcome",
    "span.name",
    "span.duration.us"
  ]
}
```

> **Talking point:** "This is the context the LLM receives when an analyst asks 'why are checkout transactions failing?' — service name, transaction name, duration, HTTP status, outcome. No order IDs, no transaction references, no customer data. The model can diagnose the issue without ever seeing a reference that could identify a customer or a transaction."

### Step 3b — Show What Fields the Index Does and Does Not Contain

```
# Confirm what transaction/order-related fields exist in the traces index
# Run this BEFORE applying the pipeline — shows raw fields present
GET traces-apm-default/_field_caps?fields=labels.*,transaction.*,span.*,event.*,service.*
```

```
# After applying the pipeline template, new documents will have hashed order IDs
# This query confirms the hash field exists and the raw field does not
# (Run after a few minutes of new data flowing through the pipeline)
GET traces-apm-default/_field_caps?fields=labels.app_order_id,labels.app_order_id_hash,labels.app_payment_transaction_id
```

> **Talking point:** "The field caps API shows us exactly what is stored in this index. We can see all the telemetry fields — span names, durations, outcomes. Once the pipeline is active, any new order or transaction reference comes in as a hash only. There is no raw value to find, even if someone bypassed all access controls."

---

## BONUS — LLM Observability: Every AI Call Is Logged Here

> **Talking point:** "Everything sent to Azure OpenAI — and everything that comes back — is observable inside Elastic. Your security team controls what gets captured, and it never leaves your environment."

### Step 1 — Show the Agent Builder Overview Dashboard

Navigate to **Dashboards → "Agent Builder — Overview"**

This is a live, built-in dashboard showing:
- **Token usage and cost** — every token sent to and received from the LLM model, over time
- **Conversation throughput** — how many AI interactions are happening and when
- **Agent execution** — which agents are running, how long they take
- **Tool call health** — which tools the AI is invoking, success/failure rates
- **Workflow performance** — end-to-end latency for AI-driven workflows

> **Talking point:** "This is your AI control tower. Every call your analysts make to Azure OpenAI is captured here — token counts, latency, tool calls, workflow steps. This dashboard is powered by OpenTelemetry traces stored in your Elastic environment. Microsoft never sees this data. You own it."

### Step 2 — Show the Privacy Controls (GenAI Settings)

Navigate to **Management → AI → GenAI Settings**

Point to the tracing toggles:
- **Collect conversation traces** — master switch
- **Include user prompts in traces** — off by default
- **Include LLM responses in traces** — off by default
- **Include tool call details** — off by default

> **Talking point:** "Notice what is off by default. User prompts and LLM responses are not captured unless you choose to enable them. That is Zenith Bank's decision — not Elastic's, not Microsoft's. You define the audit depth. And whatever you do capture is stored here, in Elastic, in your environment."

### Step 3 — Drill Into a Trace in Dev Console (optional)

```
# Show raw Agent Builder OTel traces — all AI activity stored in Elastic
GET traces-agent_builder.otel-default/_search
{
  "size": 5,
  "sort": [{ "@timestamp": { "order": "desc" } }],
  "_source": [
    "@timestamp",
    "transaction.name",
    "span.name",
    "span.duration.us",
    "service.name",
    "labels"
  ]
}
```

> **Talking point:** "And for the technical members of the security team — this is the raw trace data behind that dashboard. Every AI action is a span in Elastic. You can query it, alert on it, and audit it just like any other operational data in your environment."

---

## AGENT BUILDER — Configuring the AI Agent as a Secure Data Gateway

> **Talking point:** "The ingest pipeline controls what gets stored. The Agent Builder controls how the AI behaves on top of that stored data. Together they give you two independent layers of defence."

### Step 1 — Add Custom Instructions (Behavioural Guardrails)

Navigate to **Agent Builder → Elastic AI Agent → Edit agent settings → Custom Instructions**

Paste the following:

```
You are a security-aware analyst assistant for Zenith Bank.

Data governance rules — follow these strictly:
- Never display, repeat, or reconstruct raw order IDs, transaction IDs, or customer addresses from log or trace data. If you encounter a hash value treat it as an internal reference only — do not show it to the user.
- Do not attempt to identify individual customers or transactions from service telemetry.
- If asked to retrieve or display sensitive financial data, explain that access controls prevent this and suggest the user contact the data governance team.
- Only summarise patterns, error rates, service health, and operational metrics — never enumerate individual records or transactions.
- When querying data, prefer aggregations over raw document retrieval.
```

Click **Save**.

> **Talking point:** "These instructions are baked into every conversation this agent has. Even if the ingest pipeline missed something, the agent is instructed not to surface it. This is the behavioural layer on top of the data layer."

### Step 2 — Add a Scoped Index Search Tool

Navigate to **Agent Builder → Elastic AI Agent → Tools → Add tool → Index search**

Configure it as follows:

| Setting | Value |
|---|---|
| **Tool name** | `Zenith Sanitized Logs` |
| **Index pattern** | `logs-apm.app.*-default` |
| **Row limit** | `10` |
| **Custom instructions** | `Only return aggregated summaries of log patterns, error counts, and service names. Do not return raw log message content or individual document fields.` |

> **Talking point:** "This tool points the agent at exactly one set of indices — the logs that have already been through the masking pipeline. Row limit of 10 means the agent can never bulk-dump data into the LLM context. And the tool-level instructions add a third layer of guardrails specific to how this data source is used."

### Step 3 — Test the Guardrails Live

Ask the agent:
1. *"Show me the raw order IDs from the accounting logs"* → should decline or summarise without IDs
2. *"Which services had the most errors in the last hour?"* → should answer normally using aggregated data
3. *"What is the card number used in the last payment?"* → should decline and explain access controls

> **Talking point:** "Three questions — one the agent answers, two it refuses. The security team can see the guardrails working in real time."

---

## Summary Query — What the Security Team Should Walk Away With

```
# Final summary: show all OTel demo services with error rates and avg duration
# This is the service inventory — the map of everything being observed
GET traces-apm-default/_search
{
  "size": 0,
  "aggs": {
    "services": {
      "terms": {
        "field": "service.name",
        "size": 30
      },
      "aggs": {
        "errors": {
          "filter": { "term": { "event.outcome": "failure" } }
        },
        "avg_duration_us": {
          "avg": { "field": "transaction.duration.us" }
        }
      }
    }
  }
}
```

```
# Cross-index view — logs AND traces across all demo services
GET logs-apm.app.*-default,traces-apm-default/_search
{
  "size": 0,
  "aggs": {
    "by_service": {
      "terms": { "field": "service.name", "size": 30 }
    }
  }
}
```

> **Talking point:** "This is your entire application estate — visible, observable, and under control. Every service, every transaction, every error — all flowing through Elastic. And through everything we've shown today, you decide what stays private, what gets analysed, and what the AI can and cannot see."

---

## Troubleshooting Notes

- Confirmed index names (from live environment):
  - Traces: `traces-apm-default`
  - Logs per service: `logs-apm.app.<service>-default` (e.g. `logs-apm.app.payment-default`)
  - Metrics per service: `metrics-apm.app.<service>-default`
  - Aggregated metrics: `metrics-apm.service_transaction.1m-default`
- The pipeline simulate will work regardless of whether the pipeline is applied to the index — good for showing before/after live without affecting real data
- Field caps queries are fast and visual — good for proving absence of sensitive fields to a sceptical audience
- If a query returns 0 hits, check the time range — default is last 15 minutes; widen to last 24 hours in the top-right time picker

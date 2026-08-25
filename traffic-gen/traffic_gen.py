import os
import time
import random
import logging
import requests
from datetime import datetime

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    datefmt="%Y-%m-%dT%H:%M:%S"
)
log = logging.getLogger(__name__)

TARGET = os.environ.get("TARGET_URL", "http://host.docker.internal:8080").rstrip("/")
MIN_DELAY = float(os.environ.get("MIN_DELAY", "0.5"))
MAX_DELAY = float(os.environ.get("MAX_DELAY", "3.0"))

ISINS = [
    "NGN0001234567",
    "NGN0002345678",
    "NGN0003456789",
    "USD0001234567",
    "NGN0004567890",
    "INVALID-ISIN-999",  # intentional 404
]

SETTLEMENT_IDS = [
    "STL-20260824-001",
    "STL-20260824-002",
    "STL-20260823-001",
    "STL-99999999-999",  # intentional 404
]

SETTLEMENT_TYPES = ["Coupon", "Maturity", "Purchase", "Sale"]
CURRENCIES = ["NGN", "USD"]

def get(path, label):
    url = f"{TARGET}{path}"
    try:
        r = requests.get(url, timeout=10)
        log.info(f"GET {path} -> {r.status_code}")
    except Exception as e:
        log.error(f"GET {path} -> ERROR: {e}")

def post_settlement():
    isin = random.choice([i for i in ISINS if "INVALID" not in i])
    payload = {
        "isin": isin,
        "type": random.choice(SETTLEMENT_TYPES),
        "amount": round(random.uniform(500_000, 50_000_000), 2),
        "currency": random.choice(CURRENCIES),
    }
    url = f"{TARGET}/api/settlements"
    try:
        r = requests.post(url, json=payload, timeout=10)
        log.info(f"POST /api/settlements ({isin}) -> {r.status_code}")
    except Exception as e:
        log.error(f"POST /api/settlements -> ERROR: {e}")

SCENARIOS = [
    lambda: get("/api/bonds", "list bonds"),
    lambda: get(f"/api/bonds/{random.choice(ISINS)}", "get bond"),
    lambda: get("/api/treasurybills", "list t-bills"),
    lambda: get("/api/yield-curve", "yield curve"),
    lambda: get("/api/portfolio/summary", "portfolio summary"),
    lambda: get("/api/settlements", "list settlements"),
    lambda: get(f"/api/settlements/{random.choice(SETTLEMENT_IDS)}", "get settlement"),
    lambda: post_settlement(),
]

# Weights — more reads than writes, portfolio/yield curve less frequent
WEIGHTS = [20, 20, 15, 8, 8, 15, 10, 4]

log.info(f"Starting traffic generator -> {TARGET}")
log.info(f"Delay between requests: {MIN_DELAY}s - {MAX_DELAY}s")

while True:
    scenario = random.choices(SCENARIOS, weights=WEIGHTS, k=1)[0]
    scenario()
    time.sleep(random.uniform(MIN_DELAY, MAX_DELAY))

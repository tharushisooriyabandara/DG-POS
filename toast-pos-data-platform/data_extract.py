import requests
import psycopg2
import json
import time
from datetime import datetime, timedelta

# --- CONFIGURATION ---
CREDENTIALS = {
    "userAccessType": "TOAST_MACHINE_CLIENT",
    "clientId": "8EYQTD7nbNyPGZKgItsTcuQ0E59qTvk6",
    "clientSecret": "mx2zHZyt1UiMSONJopZfosG8WN0L51-YK7fAtJl_MaQ2XAwG0U7GGWuCsHZxJ9Ca"
}
RESTAURANT_ID = "ef5a0ff9-e994-44ca-b8ad-053e4f106c6e"
DB_CONFIG = {
    "dbname": "toast_data",
    "user": "postgres",
    "password": "3121115",
    "host": "localhost",
    "port": "5432"
}


# --- 1. AUTHENTICATION ---
def get_bearer_token():
    print("Authenticating with Toast API...")
    auth_url = "https://ws-api.toasttab.com/authentication/v1/authentication/login"
    response = requests.post(auth_url, json=CREDENTIALS)
    response.raise_for_status()
    return response.json()['token']['accessToken']

# --- 2. DATABASE SETUP ---
def setup_db():
    conn = psycopg2.connect(**DB_CONFIG)
    cur = conn.cursor()
    cur.execute("""
        CREATE TABLE IF NOT EXISTS toast_customer_orders (
            order_guid UUID PRIMARY KEY,
            opened_date TIMESTAMP,
            paid_date TIMESTAMP,
            customer_guid UUID,
            customer_email TEXT,
            customer_name TEXT,
            total_amount NUMERIC(12, 2),
            source TEXT,
            tab_name TEXT,
            dining_option TEXT,
            last4Digits TEXT,
            cardType TEXT,
            tenderTransactionGuid TEXT,
            loyaltyIdentifier TEXT,
            maskedLoyaltyIdentifier TEXT,
            raw_json JSONB
        );
        CREATE INDEX IF NOT EXISTS idx_cust_guid ON toast_customer_orders(customer_guid);
        CREATE INDEX IF NOT EXISTS idx_loyalty_id ON toast_customer_orders(loyaltyIdentifier);
    """)
    conn.commit()
    return conn

# --- 3. DATA EXTRACTION & STORAGE ---
def fetch_and_store_orders(conn, start_date, end_date, token):
    headers = {
        "Authorization": f"Bearer {token}",
        "Toast-Restaurant-External-ID": RESTAURANT_ID,
        "Content-Type": "application/json"
    }

    page = 1
    while True:
        url = "https://ws-api.toasttab.com/orders/v2/ordersBulk"
        params = {
            "startDate": start_date.strftime("%Y-%m-%dT%H:%M:%S.000+0000"),
            "endDate": end_date.strftime("%Y-%m-%dT%H:%M:%S.000+0000"),
            "pageSize": 100,
            "page": page
        }

        res = requests.get(url, headers=headers, params=params)
        if res.status_code == 401:
            token = get_bearer_token()
            headers["Authorization"] = f"Bearer {token}"
            continue

        res.raise_for_status()
        data = res.json()
        if not data: break

        cur = conn.cursor()
        for order in data:
            checks = order.get('checks', [])
            first_check = checks[0] if checks else {}

            # --- New Identification Fields ---
            payments = first_check.get('payments', [])
            first_payment = payments[0] if payments else {}

            last4 = first_payment.get('last4Digits')
            card_type = first_payment.get('cardType')
            tender_guid = first_payment.get('tenderTransactionGuid')

            loyalty_info = first_check.get('appliedLoyaltyInfo') or {}
            loyalty_id = loyalty_info.get('loyaltyIdentifier')
            masked_loyalty = loyalty_info.get('maskedLoyaltyIdentifier')

            # --- Original Logic ---
            customer = order.get('customer') or first_check.get('customer') or {}
            cust_guid = customer.get('guid')
            cust_email = customer.get('email')
            full_name = f"{customer.get('firstName', '')} {customer.get('lastName', '')}".strip() or None

            cur.execute("""
                INSERT INTO toast_customer_orders (
                    order_guid, opened_date, paid_date, customer_guid, 
                    customer_email, customer_name, total_amount, 
                    source, tab_name, dining_option, 
                    last4Digits, cardType, tenderTransactionGuid,
                    loyaltyIdentifier, maskedLoyaltyIdentifier, raw_json
                )
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                ON CONFLICT (order_guid) DO UPDATE SET
                    paid_date = EXCLUDED.paid_date,
                    last4Digits = EXCLUDED.last4Digits,
                    cardType = EXCLUDED.cardType,
                    loyaltyIdentifier = EXCLUDED.loyaltyIdentifier;
            """, (
                order['guid'], order['openedDate'], order.get('paidDate') or first_check.get('paidDate'),
                cust_guid, cust_email, full_name, order.get('totalAmount') or sum(c.get('totalAmount', 0) for c in checks),
                order.get('source', 'UNKNOWN'), first_check.get('tabName'), (order.get('diningOption') or {}).get('guid'),
                last4, card_type, tender_guid, loyalty_id, masked_loyalty, json.dumps(order)
            ))

        conn.commit()
        print(f"Processed page {page} for {start_date.date()}")
        page += 1
        time.sleep(0.2)

def main():
    db_conn = setup_db()
    token = get_bearer_token()
    current_start = datetime.now() - timedelta(days=730) # 2 years
    final_end = datetime.now()

    while current_start < final_end:
        chunk_end = min(current_start + timedelta(days=30), final_end)
        try:
            fetch_and_store_orders(db_conn, current_start, chunk_end, token)
        except Exception as e:
            print(f"Error: {e}")
            token = get_bearer_token()
            time.sleep(5)
        current_start = chunk_end

if __name__ == "__main__":
    main()
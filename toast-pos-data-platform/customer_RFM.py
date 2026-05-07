import json
import hashlib
import pandas as pd
import psycopg2
from datetime import datetime

# --- CONFIGURATION ---
DB_CONFIG = {
    "dbname": "toast_data",
    "user": "postgres",
    "password": "3121115",
    "host": "localhost",
    "port": "5432"
}


def generate_prefixed_customer_id(check, order_guid):
    """
    Extracts or generates a Customer ID with a prefix indicating the source.
    Priority: Loyalty > Profile GUID > Payment/Card > Guest Order Fallback
    """
    # 1. Priority: Loyalty Identifier
    loyalty_info = check.get("appliedLoyaltyInfo")
    if loyalty_info and loyalty_info.get("loyaltyIdentifier"):
        return f"LOYALTY_{loyalty_info.get('loyaltyIdentifier')}"

    # 2. Priority: Customer Profile GUID
    customer = check.get("customer")
    if customer and customer.get("guid"):
        return f"GUID_{customer.get('guid')}"

    # 3. Priority: Payment / Card Fingerprint
    payments = check.get("payments", [])
    if payments:
        first_payment = payments[0]
        # Use Payment GUID if available, otherwise Card Type + Last 4
        p_guid = first_payment.get("guid")
        last4 = first_payment.get("last4Digits")
        card_type = first_payment.get("cardType", "CARD")

        if p_guid:
            return f"PAYMENT_{p_guid}"
        elif last4:
            return f"CARD_{card_type}_{last4}"

    # 4. Fallback: Treat as a unique one-time guest order
    return f"GUEST_ORDER_{order_guid}"


def run_updated_analysis():
    print("Connecting to database...")
    try:
        conn = psycopg2.connect(**DB_CONFIG)
        # Using the raw_json approach from your first script
        query = "SELECT raw_json FROM toast_customer_orders;"
        df_raw = pd.read_sql(query, conn)
        conn.close()

        if df_raw.empty:
            print("No data found in database.")
            return

        order_list = []

        print("Parsing JSON and identifying customers...")
        for _, row in df_raw.iterrows():
            # Handle cases where raw_json might be a string or a dict already
            order = row['raw_json']
            if isinstance(order, str):
                order = json.loads(order)

            order_guid = order.get("guid")
            paid_date = order.get("paidDate")

            # Process each check within the order
            for check in order.get("checks", []):
                unique_id = generate_prefixed_customer_id(check, order_guid)
                amount = check.get("totalAmount", 0.0)

                order_list.append({
                    "order_guid": order_guid,
                    "paid_date": pd.to_datetime(paid_date),
                    "unique_customer_id": unique_id,
                    "total_amount": amount
                })

        # Create DataFrame from parsed orders
        df_orders = pd.DataFrame(order_list)

        # --- Step 1: Export Detailed Order CSV ---
        # This replaces the simple order count with the specific columns requested
        df_orders.to_csv('processed_orders_with_ids.csv', index=False)
        print("Generated: processed_orders_with_ids.csv")

        # --- Step 2: Calculate and Export RFM Matrix ---
        print("Calculating RFM metrics...")

        # Reference date for Recency (1 day after the most recent order)
        snapshot_date = df_orders['paid_date'].max() + pd.Timedelta(days=1)

        rfm = df_orders.groupby('unique_customer_id').agg({
            'paid_date': lambda x: (snapshot_date - x.max()).days,  # Recency
            'order_guid': 'nunique',  # Frequency
            'total_amount': 'sum'  # Monetary
        }).rename(columns={
            'paid_date': 'Recency',
            'order_guid': 'Frequency',
            'total_amount': 'Monetary'
        })

        rfm.to_csv('customer_rfm_matrix.csv')
        print("Generated: customer_rfm_matrix.csv")

        print("\nSuccess! Analysis complete.")

    except Exception as e:
        print(f"Error during processing: {e}")


if __name__ == "__main__":
    run_updated_analysis()
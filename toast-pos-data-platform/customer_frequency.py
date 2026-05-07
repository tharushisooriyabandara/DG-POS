# import psycopg2
# import pandas as pd
# import json
# from datetime import datetime
#
# # --- CONFIGURATION (Ensure these match your setup) ---
# DB_CONFIG = {
#     "dbname": "toast_data",
#     "user": "postgres",
#     "password": "3121115",
#     "host": "localhost",
#     "port": "5432"
# }
#
#
# def generate_unique_customer_id(row):
#     """
#     Implements the fallback logic to identify a customer uniquely.
#     """
#     # 1. Use existing Customer GUID if available
#     if row['customer_guid'] and pd.notnull(row['customer_guid']):
#         return f"GUID_{row['customer_guid']}"
#
#     # 2. Use Loyalty ID if profile is missing
#     if row['loyaltyidentifier'] and pd.notnull(row['loyaltyidentifier']):
#         return f"LOYALTY_{row['loyaltyidentifier']}"
#
#     # 3. Use Card Fingerprint (Card Type + Last 4) for returning guests
#     if row['last4digits'] and row['cardtype']:
#         return f"CARD_{row['cardtype']}_{row['last4digits']}"
#
#     # 4. Ultimate fallback: Treat as a unique one-time guest order
#     return f"GUEST_ORDER_{row['order_guid']}"
#

#
# def run_rfm_analysis():
#     print("Connecting to database...")
#     try:
#         conn = psycopg2.connect(**DB_CONFIG)
#
#         # Load data from the database
#         query = "SELECT order_guid, paid_date, customer_guid, total_amount, last4digits, cardtype, loyaltyidentifier FROM toast_customer_orders"
#         df = pd.read_sql_query(query, conn)
#         conn.close()
#
#         if df.empty:
#             print("No data found in table.")
#             return
#
#         # Ensure dates are in datetime format
#         df['paid_date'] = pd.to_datetime(df['paid_date'])
#
#         # --- Step 1: Generate Unique Customer ID ---
#         print("Identifying customers using fallback logic...")
#         df['unique_customer_id'] = df.apply(generate_unique_customer_id, axis=1)
#
#         # --- Step 2: Calculate RFM Metrics ---
#         print("Calculating RFM metrics...")
#
#         # Reference date for Recency (Day after the last order in the dataset)
#         snapshot_date = df['paid_date'].max() + pd.Timedelta(days=1)
#
#         rfm = df.groupby('unique_customer_id').agg({
#             'paid_date': lambda x: (snapshot_date - x.max()).days,  # Recency
#             'order_guid': 'count',  # Frequency
#             'total_amount': 'sum'  # Monetary
#         }).rename(columns={
#             'paid_date': 'Recency',
#             'order_guid': 'Frequency',
#             'total_amount': 'Monetary'
#         })
#
#         # --- Step 3: Export to CSV ---
#         rfm.to_csv('customer_rfm_matrix.csv')
#         df[['order_guid', 'paid_date', 'unique_customer_id', 'total_amount']].to_csv('processed_orders_with_ids.csv',
#                                                                                      index=False)
#
#         print("\nSuccess! Generated 2 files:")
#         print("- customer_rfm_matrix.csv (Final RFM table)")
#         print("- processed_orders_with_ids.csv (Orders with generated Customer IDs)")
#
#     except Exception as e:
#         print(f"Error: {e}")
#
#
# if __name__ == "__main__":
#     run_rfm_analysis()


import psycopg2
import pandas as pd
from datetime import datetime

# --- CONFIGURATION (Keep your own credentials) ---
DB_CONFIG = {
    "dbname": "toast_data",
    "user": "postgres",
    "password": "3121115",
    "host": "localhost",
    "port": "5432"
}


def generate_unique_customer_id(row):
    """
    Updated customer identification logic following the requested priority:

    Priority 1: Loyalty Identifier (most reliable for repeat customers)
    Priority 2: Customer GUID (from customer profile)
    Priority 3: Card fingerprint (cardtype + last4) — temporary link for guests
    Ultimate fallback: order_guid (treat as true one-time guest)
    """
    # Priority 1 – Loyalty (strongest signal for repeat behavior)
    if pd.notna(row['loyaltyidentifier']) and row['loyaltyidentifier'].strip():
        return f"LOYALTY_{row['loyaltyidentifier'].strip()}"

    # Priority 2 – Customer Profile GUID
    if pd.notna(row['customer_guid']) and row['customer_guid'].strip():
        return f"GUID_{row['customer_guid'].strip()}"

    # Priority 3 – Payment card fingerprint (best effort for guests)
    if pd.notna(row['last4digits']) and pd.notna(row['cardtype']) and \
            row['last4digits'].strip() and row['cardtype'].strip():
        # Normalize to reduce duplicates from case/format differences
        card_type = row['cardtype'].strip().upper()
        last4 = row['last4digits'].strip()
        return f"CARD_{card_type}_{last4}"

    # Ultimate fallback – treat as completely anonymous one-time order
    return f"GUEST_ORDER_{row['order_guid']}"


def run_rfm_analysis():
    print("Connecting to database...")
    try:
        conn = psycopg2.connect(**DB_CONFIG)

        # Load the relevant fields
        query = """
        SELECT 
            order_guid, 
            paid_date, 
            customer_guid, 
            total_amount, 
            last4digits, 
            cardtype, 
            loyaltyidentifier 
        FROM toast_customer_orders
        """
        df = pd.read_sql_query(query, conn)
        conn.close()

        if df.empty:
            print("No data found in table.")
            return

        # Ensure proper datetime format
        df['paid_date'] = pd.to_datetime(df['paid_date'], errors='coerce')

        # Optional: drop rows with invalid/missing paid_date if they exist
        df = df.dropna(subset=['paid_date'])

        print(f"Loaded {len(df):,} orders.")

        # --- Generate stable customer identifiers ---
        print("Assigning customer IDs using new priority logic...")
        df['unique_customer_id'] = df.apply(generate_unique_customer_id, axis=1)

        # Quick diagnostic – see how many customers fall into each bucket
        id_prefixes = df['unique_customer_id'].str.split('_').str[0].value_counts()
        print("\nCustomer ID breakdown by type:")
        for prefix, count in id_prefixes.items():
            print(f"  {prefix:>12}: {count:>6,} ({count / len(df):.1%})")

        # --- RFM Calculation ---
        print("Calculating RFM metrics...")

        snapshot_date = df['paid_date'].max() + pd.Timedelta(days=1)

        rfm = df.groupby('unique_customer_id').agg(
            Recency=('paid_date', lambda x: (snapshot_date - x.max()).days),
            Frequency=('order_guid', 'count'),
            Monetary=('total_amount', 'sum')
        ).reset_index()

        # Sort for readability
        rfm = rfm.sort_values(['Monetary', 'Frequency'], ascending=False)

        # --- Export ---
        rfm.to_csv('customer_rfm_matrix.csv', index=False)
        df[['order_guid', 'paid_date', 'unique_customer_id', 'total_amount',
            'loyaltyidentifier', 'customer_guid', 'cardtype', 'last4digits']] \
            .to_csv('processed_orders_with_ids.csv', index=False)

        print("\nSuccess! Generated files:")
        print("  • customer_rfm_matrix.csv        (RFM table)")
        print("  • processed_orders_with_ids.csv  (source orders + assigned IDs)")

    except Exception as e:
        print(f"Error: {e}")


if __name__ == "__main__":
    run_rfm_analysis()
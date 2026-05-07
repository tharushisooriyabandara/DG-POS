using System;
using System.Globalization;
using System.Windows.Data;
using POS_UI.Models;

namespace POS_UI.Converters
{
    /// <summary>
    /// For incoming table order accept: enables a table if it is Available, or if it is Reserved and
    /// the reserved table's order has the same order_session_id as the incoming order.
    /// When IncomingOrderSessionId is not provided, only Available tables are enabled.
    /// </summary>
    public class TableSelectionTableEnabledConverter : IMultiValueConverter
    {
        /// <summary>Same rules as <see cref="Convert"/> for use from view models (cashier / floor plan table pick).</summary>
        public static bool IsTableSelectable(TableModel? table, int? incomingSessionId)
        {
            if (table == null)
            {
                return false;
            }

            if (table.Status == TableStatus.Unavailable)
            {
                return false;
            }

            if (!incomingSessionId.HasValue)
            {
                return table.Status == TableStatus.Available;
            }

            if (table.Status == TableStatus.Available)
            {
                return true;
            }

            if ((table.Status == TableStatus.Reserved || table.Status == TableStatus.Served) && table.Order != null)
            {
                var tableOrderSessionId = table.Order.OrderSessionId ?? 0;
                return tableOrderSessionId == incomingSessionId.Value;
            }

            return false;
        }

        /// <summary>Legend / swatch color for the table's status (and incoming-session rules for reserved/served).</summary>
        public static string GetStatusIndicatorHex(TableModel? table, int? incomingSessionId)
        {
            if (table == null)
            {
                return "#BDBDBD";
            }

            if (table.Status == TableStatus.Unavailable)
            {
                return "#9E9E9E";
            }

            if (table.Status == TableStatus.Drafted)
            {
                return "#FF9800";
            }

            if (table.Status == TableStatus.Served)
            {
                return "#FF8C00";
            }

            if (table.Status == TableStatus.Reserved)
            {
                if (incomingSessionId.HasValue && table.Order != null
                    && (table.Order.OrderSessionId ?? 0) == incomingSessionId.Value)
                {
                    return "#FBC02D";
                }

                return "#1976D2";
            }

            return "#E0E0E0";
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2) return false;
            var table = values[0] as TableModel;
            int? incomingSessionId = null;
            if (values[1] is int sid)
            {
                incomingSessionId = sid;
            }

            return IsTableSelectable(table, incomingSessionId);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

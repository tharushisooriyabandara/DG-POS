package db

/*

this issue can be fixed by using more parameters in the query as I did in getOrders query
eg:

WHERE
    dp.platform_id IN (sqlc.slice (platform_ids))
    AND (
        sqlc.narg (apply_status) IS NULL
        OR o.status = sqlc.arg (status)
    )
    AND (
        sqlc.narg (apply_outlet_id) IS NULL
        OR o.shop_id = sqlc.arg (outlet_id)
    )
    AND (
        sqlc.narg (apply_start_date) IS NULL
        OR o.created_at >= sqlc.arg (start_date)
    )
    AND (
        sqlc.narg (apply_end_date) IS NULL
        OR o.created_at <= sqlc.arg (end_date)
    )

*/

// const getShiftInfoByShop = `-- name: GetShiftInfoByShop :many
// SELECT
//     act.causer_id,
//       COUNT(act.id) AS orders_created,
//     0 + SUM(
//         CASE
//             WHEN payments.transaction_id IS NOT NULL THEN payments.amount
//             ELSE 0
//         END
//     ) AS card_total,
//     0 + SUM(
//         CASE
//             WHEN payments.transaction_id IS NULL THEN payments.amount
//             ELSE 0
//         END
//     ) AS cash_total,
//     0 + SUM(payments.amount) AS total
// FROM
//     dg_pos_user_activities act
//     --
//     JOIN user_shop us ON act.causer_id = us.user_id
//     AND us.shop_id = ?
//     --
//     JOIN ` + "`" + `order` + "`" + ` odr ON act.subject_id = odr.id
//     AND odr.status = 'COMPLETED'
//     --
//     JOIN dg_pos_payments payments ON odr.id = payments.order_id
// WHERE
//     act.event = 'create'
//     AND act.log_name = 'order'
//     AND act.created_at BETWEEN ? AND ?
// GROUP BY
//     act.causer_id
// `

// type GetShiftInfoByShopParams struct {
// 	ShopID   uint32       `json:"shopId"`
// 	FromDate sql.NullTime `json:"fromDate"`
// 	ToDate   sql.NullTime `json:"toDate"`
// }

// type GetShiftInfoByShopRow struct {
// 	CauserID      int64  `json:"causerId"`
// 	OrdersCreated int64  `json:"ordersCreated"`
// 	CardTotal     string `json:"cardTotal"`
// 	CashTotal     string `json:"cashTotal"`
// 	Total         string `json:"total"`
// }

// GetShiftInfoByShop
//
//	SELECT
//	    act.causer_id,
//	      COUNT(act.id) AS orders_created,
//	    0 + SUM(
//	        CASE
//	            WHEN payments.transaction_id IS NOT NULL THEN payments.amount
//	            ELSE 0
//	        END
//	    ) AS card_total,
//	    0 + SUM(
//	        CASE
//	            WHEN payments.transaction_id IS NULL THEN payments.amount
//	            ELSE 0
//	        END
//	    ) AS cash_total,
//	    0 + SUM(payments.amount) AS total
//	FROM
//	    dg_pos_user_activities act
//	    --
//	    JOIN user_shop us ON act.causer_id = us.user_id
//	    AND us.shop_id = ?
//	    --
//	    JOIN `order` odr ON act.subject_id = odr.id
//	    AND odr.status = 'COMPLETED'
//	    --
//	    JOIN dg_pos_payments payments ON odr.id = payments.order_id
//	WHERE
//	    act.event = 'create'
//	    AND act.log_name = 'order'
//	    AND act.created_at BETWEEN ? AND ?
//	GROUP BY
//	    act.causer_id
// func (q *Queries) GetShiftInfoByShop(ctx context.Context, arg *GetShiftInfoByShopParams) ([]*GetShiftInfoByShopRow, error) {
// 	rows, err := q.db.QueryContext(ctx, getShiftInfoByShop,
// 		arg.ShopID,
// 		arg.FromDate,
// 		arg.ToDate,
// 	)
// 	if err != nil {
// 		return nil, err
// 	}
// 	defer rows.Close()
// 	items := []*GetShiftInfoByShopRow{}
// 	for rows.Next() {
// 		var i GetShiftInfoByShopRow
// 		if err := rows.Scan(
// 			&i.CauserID,
// 			&i.OrdersCreated,
// 			&i.CardTotal,
// 			&i.CashTotal,
// 			&i.Total,
// 		); err != nil {
// 			return nil, err
// 		}
// 		items = append(items, &i)
// 	}
// 	if err := rows.Close(); err != nil {
// 		return nil, err
// 	}
// 	if err := rows.Err(); err != nil {
// 		return nil, err
// 	}
// 	return items, nil
// }

// const getShiftInfoByUser = `-- name: GetShiftInfoByUser :many
// SELECT
//     shifts.id AS shift_id,
//     shifts.login,
//     shifts.logout,
//     COUNT(act.id) AS orders_created,
//     0 + SUM(
//         CASE
//             WHEN payments.transaction_id IS NOT NULL THEN payments.amount
//             ELSE 0
//         END
//     ) AS card_total,
//     0 + SUM(
//         CASE
//             WHEN payments.transaction_id IS NULL THEN payments.amount
//             ELSE 0
//         END
//     ) AS cash_total,
//     0 + SUM(payments.amount) AS total
// FROM
//     dg_pos_user_activities act
//     --
//     JOIN dg_pos_user_shifts shifts ON act.causer_id = shifts.user_id
//     AND act.created_at BETWEEN shifts.login AND COALESCE(shifts.logout, NOW())
//     --
//     JOIN ` + "`" + `order` + "`" + ` odr ON act.subject_id = odr.id
//     AND odr.status = 'COMPLETED'
//     --
//     JOIN dg_pos_payments payments ON odr.id = payments.order_id
// WHERE
//     act.event = 'create'
//     AND act.log_name = 'order'
//     AND act.causer_id = ?
//     AND act.created_at BETWEEN ? AND ?
// GROUP BY
//     shifts.id
// `

// type GetShiftInfoByUserParams struct {
// 	UserID   int64        `json:"userId"`
// 	FromDate sql.NullTime `json:"fromDate"`
// 	ToDate   sql.NullTime `json:"toDate"`
// }

// type GetShiftInfoByUserRow struct {
// 	ShiftID       uint64       `json:"shiftId"`
// 	Login         time.Time    `json:"login"`
// 	Logout        sql.NullTime `json:"logout"`
// 	OrdersCreated int64        `json:"ordersCreated"`
// 	CardTotal     string       `json:"cardTotal"`
// 	CashTotal     string       `json:"cashTotal"`
// 	Total         string       `json:"total"`
// }

// GetShiftInfoByUser
//
//	SELECT
//	    shifts.id AS shift_id,
//	    shifts.login,
//	    shifts.logout,
//	    COUNT(act.id) AS orders_created,
//	    0 + SUM(
//	        CASE
//	            WHEN payments.transaction_id IS NOT NULL THEN payments.amount
//	            ELSE 0
//	        END
//	    ) AS card_total,
//	    0 + SUM(
//	        CASE
//	            WHEN payments.transaction_id IS NULL THEN payments.amount
//	            ELSE 0
//	        END
//	    ) AS cash_total,
//	    0 + SUM(payments.amount) AS total
//	FROM
//	    dg_pos_user_activities act
//	    --
//	    JOIN dg_pos_user_shifts shifts ON act.causer_id = shifts.user_id
//	    AND act.created_at BETWEEN shifts.login AND shifts.logout
//	    --
//	    JOIN `order` odr ON act.subject_id = odr.id
//	    AND odr.status = 'COMPLETED'
//	    --
//	    JOIN dg_pos_payments payments ON odr.id = payments.order_id
//	WHERE
//	    act.event = 'create'
//	    AND act.log_name = 'order'
//	    AND act.causer_id = ?
//	    AND act.created_at BETWEEN ? AND ?
//	GROUP BY
//	    shifts.id

// func (q *Queries) GetShiftInfoByUser(ctx context.Context, arg *GetShiftInfoByUserParams) ([]*GetShiftInfoByUserRow, error) {
// 	rows, err := q.db.QueryContext(ctx, getShiftInfoByUser,
// 		arg.UserID,
// 		arg.FromDate,
// 		arg.ToDate,
// 	)
// 	if err != nil {
// 		return nil, err
// 	}
// 	defer rows.Close()
// 	items := []*GetShiftInfoByUserRow{}
// 	for rows.Next() {
// 		var i GetShiftInfoByUserRow
// 		if err := rows.Scan(
// 			&i.ShiftID,
// 			&i.Login,
// 			&i.Logout,
// 			&i.OrdersCreated,
// 			&i.CardTotal,
// 			&i.CashTotal,
// 			&i.Total,
// 		); err != nil {
// 			return nil, err
// 		}
// 		items = append(items, &i)
// 	}
// 	if err := rows.Close(); err != nil {
// 		return nil, err
// 	}
// 	if err := rows.Err(); err != nil {
// 		return nil, err
// 	}
// 	return items, nil
// }

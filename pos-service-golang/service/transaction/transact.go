package transaction

import (
	"context"
	"database/sql"
	"fmt"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
)

type queryFunc func(ctx context.Context, qtx *db.Queries) error

func Build(queries ...queryFunc) queryFunc {
	return func(ctx context.Context, qtx *db.Queries) error {
		var err error
		for _, query := range queries {
			err = query(ctx, qtx)
			if err != nil {
				return err
			}
		}
		return nil
	}
}

func Exec(ctx context.Context, dbConn *sql.DB, f queryFunc) error {
	tx, err := dbConn.Begin()
	if err != nil {
		return fmt.Errorf("failed to begin transaction: %w", err)
	}

	defer tx.Rollback()

	qtx := db.New(dbConn).WithTx(tx)
	err = f(ctx, qtx)
	if err != nil {
		return err
	}

	if err := tx.Commit(); err != nil {
		return fmt.Errorf("failed to commit transaction: %w", err)
	}

	return nil

}

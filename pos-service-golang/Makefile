include .env
export

DB_URL=mysql://$(DB_USERNAME):$(DB_PASSWORD)@tcp($(DB_HOST):$(DB_PORT))/$(DB_DATABASE)
MIGRATIONS_DIR=database/migrations

.PHONY: run build docker-build

build:
	swag init -g cmd/main.go -o docs
	go build -o bin/app cmd/main.go

run: build
	./bin/app

docker-build:
	docker build -t pos-service:latest .

sqlgen:
	sqlc generate

migrate-create:
	migrate create -ext sql -dir $(MIGRATIONS_DIR) -seq $(name)

migrate-up:
	migrate -database "$(DB_URL)" -path $(MIGRATIONS_DIR) up

migrate-down:
	migrate -database "$(DB_URL)" -path $(MIGRATIONS_DIR) down 1

migrate-down-all:
	migrate -database "$(DB_URL)" -path $(MIGRATIONS_DIR) down

mysql-dump:
	mysqldump -u $(DB_USERNAME) -p $(DB_PASSWORD) -h $(DB_HOST) --no-data $(DB_DATABASE) > database/schema.sql
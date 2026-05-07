# Build stage
FROM golang:1.21-alpine AS builder

# Install build dependencies
RUN apk add --no-cache git curl wget ca-certificates

# Install sqlc
RUN wget -O sqlc.tar.gz https://github.com/sqlc-dev/sqlc/releases/download/v1.25.0/sqlc_1.25.0_linux_amd64.tar.gz && \
    tar -xf sqlc.tar.gz && \
    mv sqlc /usr/local/bin && \
    rm sqlc.tar.gz

# Set working directory
WORKDIR /app

# Copy go mod files first for better caching
COPY go.mod go.sum ./
RUN go mod download

# Copy the rest of the code
COPY . .

# Set environment variables for direct GitHub access
ENV GOPROXY=direct
ENV GOSUMDB=off
ENV GO111MODULE=on

# Clean and download dependencies
RUN rm -f go.sum && \
    go mod tidy && \
    go mod download all && \
    go mod verify

# Generate SQLC code
RUN sqlc generate

# Build the application
RUN CGO_ENABLED=0 GOOS=linux go build -o main ./cmd/main.go

# Final stage
FROM alpine:latest

RUN apk --no-cache add ca-certificates tzdata

WORKDIR /app

# Copy binary from builder
COPY --from=builder /app/main .
COPY --from=builder /app/.env .

# Expose port
EXPOSE 8080

# Run the application
CMD ["./main"] 
# POS Service API

A modern Point of Sale (POS) service API built with Go and Fiber.

## Architecture

The project follows a clean architecture pattern with performance optimizations:

### Layers
1. **Handlers** (`/handlers`)
   - Entry point for HTTP requests
   - Request validation
   - Response formatting
   - Routes HTTP requests to appropriate services

2. **Services** (`/service`)
   - Business logic layer
   - Interface-based design for testability
   - Transaction management
   - Service-level validations

3. **Database** (`/database`)
   - SQLC-generated code for type-safe database operations
   - High-performance database interactions
   - Compile-time SQL validation
   - No ORM overhead

### Key Design Decisions
- Interface-based service layer for easy mocking in tests
- SQLC for type-safe and high-performance database operations
- Clean separation of concerns
- Dependency injection for better testability

## Prerequisites

- Go 1.16 or higher
- MySQL 8.0 or higher
- Make (optional, for using Makefile commands)
- SQLC (for generating database code)

## Environment Variables

Create a `.env` file in the root directory with the following variables:

```env
PORT=8080
DB_HOST=localhost
DB_PORT=3306
DB_USER=root
DB_PASSWORD=your_password
DB_NAME=pos_service
JWT_SECRET=your_secret_key
ENV=development
```

## Setup

1. Clone the repository:
```bash
git clone https://github.com/your-username/pos-service-golang.git
cd pos-service-golang
```

2. Install dependencies:
```bash
go mod download
```

3. Generate database code:
```bash
sqlc generate
```

4. Run the application:
```bash
go run cmd/main.go
```

Or using Make:
```bash
make run
```

## API Endpoints

- `POST /api/v1/login` - User login
- `GET /api/v1/users` - Get all users (requires authentication)
- `GET /api/v1/users/:id` - Get user by ID (requires authentication)
- `POST /api/v1/users` - Create new user (requires authentication)
- `PUT /api/v1/users/:id` - Update user (requires authentication)
- `DELETE /api/v1/users/:id` - Delete user (requires authentication)

## Project Structure

```
.
├── cmd/
│   └── main.go
├── config/
│   └── config.go
├── database/
│   ├── database.go
│   └── sqlc/
│       ├── db.go
│       ├── models.go
│       └── queries/
├── handlers/
│   └── user/
├── middleware/
│   └── auth.go
├── service/
│   └── user_service.go
├── sqlc.yaml
├── .env
├── .gitignore
├── go.mod
├── go.sum
├── Makefile
└── README.md
```

## Contributing

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the LICENSE file for details. 
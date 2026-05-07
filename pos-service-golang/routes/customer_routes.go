package routes

import (
	handlers "github.com/Delivergate-Dev/pos-service-golang/handlers/customer"
	"github.com/Delivergate-Dev/pos-service-golang/middleware"
	"github.com/gofiber/fiber/v2"
)

func setupCustomerRoutes(router fiber.Router) {
	customerHandler := handlers.NewCustomerHandler()
	customers := router.Group(
		"/customers",
		middleware.TenantMiddleware,
		middleware.AuthMiddleware,
	)

	customers.Get("/", customerHandler.GetCustomers)
	customers.Get("/:id", customerHandler.GetCustomerDetails)
	customers.Post("/:id/addresses", customerHandler.CreateCustomerAddress)
	customers.Post("/", customerHandler.CreateCustomer)
	customers.Put("/:id", customerHandler.UpdateCustomer)

}

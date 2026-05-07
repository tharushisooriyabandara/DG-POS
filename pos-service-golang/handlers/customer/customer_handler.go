package handlers

import (
	"errors"

	"github.com/Delivergate-Dev/pos-service-golang/api"
	posErr "github.com/Delivergate-Dev/pos-service-golang/errors"
	"github.com/Delivergate-Dev/pos-service-golang/service"
	"github.com/Delivergate-Dev/pos-service-golang/tenant"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"github.com/Delivergate-Dev/pos-service-golang/validator"
	"github.com/gofiber/fiber/v2"
)

type CustomerHandler struct {
}

func NewCustomerHandler() *CustomerHandler {
	return &CustomerHandler{}
}

func (h *CustomerHandler) GetCustomers(c *fiber.Ctx) error {
	tenant := c.Locals("tenant").(*tenant.Runtime)
	customers, err := tenant.CustomerService.GetCustomers(c.Context())
	if err != nil {
		return err
	}

	return api.Ok(c, "Customers fetched successfully", customers)
}

func (h *CustomerHandler) CreateCustomer(c *fiber.Ctx) error {

	var createCustomerRequest types.CreateCustomerRequest
	if err := c.BodyParser(&createCustomerRequest); err != nil {
		return api.BadRequest("Invalid request body", err.Error())
	}

	if err := validator.Validate(&createCustomerRequest); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	runtime := c.Locals("tenant").(*tenant.Runtime)
	createCustomerRequest.Requestor = c.Locals("user").(types.SessionUser)

	_, err := runtime.CustomerService.CreateCustomer(c.Context(), &createCustomerRequest)
	if err != nil {
		if pe, ok := err.(posErr.SlugError); ok {
			return api.BadRequest(pe.Error(), pe.Slug())
		}
		if errors.Is(err, service.ErrCustomerCreate) {
			return api.BadRequest("Failed to create customer", err.Error())
		}
		return err
	}

	return api.Ok(c, "Customer created successfully", struct{}{})
}

func (h *CustomerHandler) GetCustomerDetails(c *fiber.Ctx) error {
	customerID, err := c.ParamsInt("id")
	if err != nil || customerID == 0 {
		return api.BadRequest("Invalid customer ID", err.Error())
	}

	tenant := c.Locals("tenant").(*tenant.Runtime)
	customer, err := tenant.CustomerService.GetCustomerDetails(c.Context(), uint64(customerID))
	if err != nil {
		return err
	}

	return api.Ok(c, "Customer fetched successfully", customer)
}

func (h *CustomerHandler) CreateCustomerAddress(c *fiber.Ctx) error {

	customerID, err := c.ParamsInt("id")
	if err != nil || customerID == 0 {
		return api.BadRequest("Invalid customer ID")
	}

	var createCustomerAddressRequest types.CreateCustomerAddressRequest

	if err := c.BodyParser(&createCustomerAddressRequest); err != nil {
		return api.BadRequest("Invalid request body", err.Error())
	}

	if err := validator.Validate(&createCustomerAddressRequest); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	createCustomerAddressRequest.CustomerID = int32(customerID)

	runtime := c.Locals("tenant").(*tenant.Runtime)
	if err := runtime.CustomerService.CreateCustomerAddress(c.Context(), &createCustomerAddressRequest); err != nil {
		return err
	}

	return api.Ok(c, "Customer address created successfully", struct{}{})
}

func (h *CustomerHandler) UpdateCustomer(c *fiber.Ctx) error {
	customerID, err := c.ParamsInt("id")
	if err != nil || customerID == 0 {
		return api.BadRequest("Invalid customer ID")
	}

	var updateCustomerRequest types.UpdateCustomerRequest
	if err := c.BodyParser(&updateCustomerRequest); err != nil {
		return api.BadRequest("Invalid request body", err.Error())
	}

	if err := validator.Validate(&updateCustomerRequest); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	updateCustomerRequest.ID = uint64(customerID)
	updateCustomerRequest.Requestor = c.Locals("user").(types.SessionUser)
	runtime := c.Locals("tenant").(*tenant.Runtime)

	if err := runtime.CustomerService.UpdateCustomer(c.Context(), &updateCustomerRequest); err != nil {
		if pe, ok := err.(posErr.SlugError); ok {
			return api.BadRequest(pe.Error(), pe.Slug())
		}
		return err
	}

	return api.Ok(c, "Customer updated successfully", struct{}{})
}

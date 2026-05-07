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

type OrdersHandler struct{}

func NewOrdersHandler() *OrdersHandler { return &OrdersHandler{} }

func (h *OrdersHandler) CreateOrder(c *fiber.Ctx) error {

	var createOrderRequest types.CreateOrderRequest
	if err := c.BodyParser(&createOrderRequest); err != nil {
		return api.BadRequest("Invalid request body", err.Error())
	}

	if err := validator.Validate(&createOrderRequest); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	createOrderRequest.Requestor = c.Locals("user").(types.SessionUser)
	runtime := c.Locals("tenant").(*tenant.Runtime)

	_, err := runtime.OrderService.CreateOrder(c.Context(), &createOrderRequest)
	if err != nil {
		if errors.Is(err, service.ErrOrderCreationFailed) {
			return api.BadRequest("Failed to create order", err.Error())
		}
		if pe, ok := err.(posErr.SlugError); ok {
			return api.BadRequest(pe.Error(), pe.Slug())
		}
		return err
	}

	return api.Ok(c, "Order created successfully", struct{}{})

}

func (h *OrdersHandler) UpdateOrder(c *fiber.Ctx) error {

	orderID, err := c.ParamsInt("id")
	if err != nil || orderID == 0 {
		return api.BadRequest("Invalid order ID")
	}

	var updateOrderRequest types.UpdateOrderRequest
	if err := c.BodyParser(&updateOrderRequest); err != nil {
		return api.BadRequest("Invalid request body", err.Error())
	}

	if err := validator.Validate(&updateOrderRequest); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	updateOrderRequest.Requestor = c.Locals("user").(types.SessionUser)
	runtime := c.Locals("tenant").(*tenant.Runtime)

	_, err = runtime.OrderService.UpdateOrder(c.Context(), uint64(orderID), &updateOrderRequest)
	if err != nil {

		if errors.Is(err, service.ErrOrderNotFound) {
			return api.NotFound("Order not found")
		}

		if errors.Is(err, service.ErrOrderUpdateFailed) {
			return api.BadRequest("Failed to update order", err.Error())
		}
		return err
	}

	return api.Ok(c, "Order updated successfully", struct{}{})
}

func (h *OrdersHandler) CompleteOrderWithPayment(c *fiber.Ctx) error {

	orderID, err := c.ParamsInt("id")
	if err != nil || orderID == 0 {
		return api.BadRequest("Invalid order ID", err.Error())
	}

	var orderPaymentRequest types.CreateOrderPaymentRequest
	if err := c.BodyParser(&orderPaymentRequest); err != nil {
		return api.BadRequest("Invalid request body", err.Error())
	}

	if err := validator.Validate(&orderPaymentRequest); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	orderPaymentRequest.OrderID = uint64(orderID)
	orderPaymentRequest.Requestor = c.Locals("user").(types.SessionUser)
	runtime := c.Locals("tenant").(*tenant.Runtime)

	_, err = runtime.OrderService.UpdateOrderPayment(c.Context(), &orderPaymentRequest)
	if err != nil {
		if errors.Is(err, service.ErrOrderNotFound) {
			return api.NotFound("Order not found")
		}

		if errors.Is(err, service.ErrOrderUpdateFailed) {
			return api.BadRequest("Failed to update order", err.Error())
		}
		return err
	}

	return api.Ok(c, "Order completed successfully", struct{}{})
}

func (h *OrdersHandler) UpdateOrderStatus(c *fiber.Ctx) error {
	orderID, err := c.ParamsInt("id")
	if err != nil || orderID == 0 {
		return api.BadRequest("Invalid order ID", err.Error())
	}

	var updateOrderStatusRequest types.UpdateOrderStatusRequest
	if err := c.BodyParser(&updateOrderStatusRequest); err != nil {
		return api.BadRequest("Invalid request body", err.Error())
	}

	if err := validator.Validate(&updateOrderStatusRequest); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	// update the order status
	updateOrderStatusRequest.OrderID = uint64(orderID)
	updateOrderStatusRequest.Requestor = c.Locals("user").(types.SessionUser)
	runtime := c.Locals("tenant").(*tenant.Runtime)

	_, err = runtime.OrderService.UpdateOrderStatus(c.Context(), &updateOrderStatusRequest)
	if err != nil {
		if errors.Is(err, service.ErrOrderNotFound) {
			return api.NotFound("Order not found")
		}
		return err
	}

	return api.Ok(c, "Order status updated successfully", struct{}{})
}

func (h *OrdersHandler) UpdateOrderUserID(c *fiber.Ctx) error {
	orderID, err := c.ParamsInt("id")
	if err != nil || orderID == 0 {
		return api.BadRequest("Invalid order ID", err.Error())
	}

	var updateOrderUserIDRequest types.UpdateOrderUserIDRequest
	if err := c.BodyParser(&updateOrderUserIDRequest); err != nil {
		return api.BadRequest("Invalid request body", err.Error())
	}

	if err := validator.Validate(&updateOrderUserIDRequest); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	updateOrderUserIDRequest.OrderID = uint64(orderID)

	runtime := c.Locals("tenant").(*tenant.Runtime)
	err = runtime.IncomingOrderService.UpdateOrderUserID(c.Context(), &updateOrderUserIDRequest)
	if err != nil {
		return err
	}

	return api.Ok(c, "Order user ID updated successfully", struct{}{})
}

func (h *OrdersHandler) GetOrders(c *fiber.Ctx) error {

	var getOrdersRequest types.GetOrdersRequest
	if err := c.QueryParser(&getOrdersRequest); err != nil {
		return api.BadRequest("Invalid query params", err.Error())
	}

	if err := validator.Validate(&getOrdersRequest); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	tenant := c.Locals("tenant").(*tenant.Runtime)
	orders, err := tenant.OrderService.GetOrders(c.Context(), &getOrdersRequest)
	if err != nil {
		return err
	}

	return api.Ok(c, "success", orders)
}

func (h *OrdersHandler) GetOrdersExport(c *fiber.Ctx) error {
	var getOrdersRequest types.GetOrdersRequest
	if err := c.QueryParser(&getOrdersRequest); err != nil {
		return api.BadRequest("Invalid query params", err.Error())
	}

	if err := validator.Validate(&getOrdersRequest); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	c.Set("content-type", "text/csv")
	c.Set("content-disposition", "attachment; filename=orders.csv")

	tenant := c.Locals("tenant").(*tenant.Runtime)
	err := tenant.OrderService.GetOrdersAsCsv(c.Context(), &getOrdersRequest, c.Response().BodyWriter())
	if err != nil {
		return err
	}

	return nil
}

func (h *OrdersHandler) GetOrder(c *fiber.Ctx) error {
	orderID, err := c.ParamsInt("id")
	if err != nil || orderID == 0 {
		return api.BadRequest("Invalid order ID", err.Error())
	}

	tenant := c.Locals("tenant").(*tenant.Runtime)
	order, err := tenant.OrderService.GetOrder(c.Context(), uint64(orderID))
	if err != nil {
		if errors.Is(err, service.ErrOrderNotFound) {
			return api.NotFound("Order not found")
		}
		return err
	}

	return api.Ok(c, "success", order)
}

func (h *OrdersHandler) RefundOrder(c *fiber.Ctx) error {
	orderID, err := c.ParamsInt("id")
	if err != nil || orderID == 0 {
		return api.BadRequest("Invalid order ID", err.Error())
	}

	var refundOrderRequest types.RefundOrderRequest
	if err := c.BodyParser(&refundOrderRequest); err != nil {
		return api.BadRequest("Invalid request body", err.Error())
	}
	refundOrderRequest.Requestor = c.Locals("user").(types.SessionUser)
	refundOrderRequest.OrderID = uint64(orderID)

	runtime := c.Locals("tenant").(*tenant.Runtime)
	_, err = runtime.OrderService.RefundOrder(c.Context(), &refundOrderRequest)
	if err != nil {
		return err
	}

	return api.Ok(c, "success", struct{}{})
}

package api

import (
	"net/http"

	"github.com/Delivergate-Dev/pos-service-golang/logger"
	"github.com/gofiber/fiber/v2"
	"go.uber.org/zap"
)

type SuccessResponse[T any] struct {
	Code    int    `json:"code"`
	Message string `json:"message"`
	Data    T      `json:"data,omitzero"`
}

func NewSuccessResponse[T any](code int, message string, data ...T) *SuccessResponse[T] {
	var respData T
	if len(data) > 0 {
		respData = data[0]
	}
	return &SuccessResponse[T]{
		Code:    code,
		Message: message,
		Data:    respData,
	}
}

func Ok[T any](c *fiber.Ctx, message string, data T) error {
	return sendResponse(c, NewSuccessResponse(http.StatusOK, message, data))
}

func Accepted[T any](c *fiber.Ctx, message string, data T) error {
	return sendResponse(c, NewSuccessResponse(http.StatusAccepted, message, data))
}

func Created[T any](c *fiber.Ctx, message string, data T) error {
	return sendResponse(c, NewSuccessResponse(http.StatusCreated, message, data))
}

func sendResponse[T any](c *fiber.Ctx, data *SuccessResponse[T]) error {
	err := c.Status(data.Code).JSON(data)
	if err != nil {
		logger.Error("Error sending the request", zap.Error(err))
	}
	return err
}

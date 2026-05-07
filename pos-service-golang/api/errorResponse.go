package api

import (
	"github.com/gofiber/fiber/v2"
)

type ErrorResponse struct {
	Code    int    `json:"code"`
	Message string `json:"message"`
	Errors  any    `json:"errors,omitempty"`
}

// Error implements the error interface
func (e *ErrorResponse) Error() string {
	return e.Message
}

// NewErrorResponse creates a new Response
func NewErrorResponse(code int, message string, errors ...any) *ErrorResponse {
	var err any
	if len(errors) > 0 {
		err = errors[0]
	}
	return &ErrorResponse{
		Code:    code,
		Message: message,
		Errors:  err,
	}
}

// Common error constructors
func BadRequest(message string, errors ...any) *ErrorResponse {
	return NewErrorResponse(fiber.StatusBadRequest, message, errors...)
}

func Unauthorized(message string, errors ...any) *ErrorResponse {
	return NewErrorResponse(fiber.StatusUnauthorized, message, errors...)
}

func Forbidden(message string, errors ...any) *ErrorResponse {
	return NewErrorResponse(fiber.StatusForbidden, message, errors...)
}

func NotFound(message string, errors ...any) *ErrorResponse {
	return NewErrorResponse(fiber.StatusNotFound, message, errors...)
}

// ErrorHandler is the global error handler for the application
func ErrorHandler(c *fiber.Ctx, err error) error {
	// Check if it's a Response
	if appErr, ok := err.(*ErrorResponse); ok {
		return c.Status(appErr.Code).JSON(appErr)
	}

	// Handle validation errors
	if validationErr, ok := err.(*fiber.Error); ok {
		return c.Status(validationErr.Code).JSON(&ErrorResponse{
			Code:    validationErr.Code,
			Message: validationErr.Message,
		})
	}

	// Handle unknown errors
	return c.Status(fiber.StatusInternalServerError).JSON(&ErrorResponse{
		Code:    fiber.StatusInternalServerError,
		Message: "Internal server error",
		Errors:  err.Error(),
	})
}

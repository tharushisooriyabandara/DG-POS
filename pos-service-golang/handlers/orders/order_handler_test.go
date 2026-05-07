package handlers

import (
	"net/http/httptest"
	"strings"
	"testing"

	"github.com/Delivergate-Dev/pos-service-golang/api"
	"github.com/Delivergate-Dev/pos-service-golang/validator"
	"github.com/gofiber/fiber/v2"
	"github.com/stretchr/testify/assert"
)

func setupTest(t *testing.T) *fiber.App {
	t.Helper()

	validator.Init()

	app := fiber.New(fiber.Config{
		ErrorHandler: api.ErrorHandler,
	})

	handler := NewOrdersHandler()
	app.Post("/orders", handler.CreateOrder)

	return app

}

func TestCreateOrder(t *testing.T) {
	app := setupTest(t)

	t.Run("should return 400 for invalid request body", func(t *testing.T) {
		req := httptest.NewRequest("POST", "/orders", strings.NewReader(""))
		req.Header.Add("Content-Type", "application/json")

		resp, err := app.Test(req)
		assert.Nil(t, err)

		assert.Equal(t, 400, resp.StatusCode)
	})

}

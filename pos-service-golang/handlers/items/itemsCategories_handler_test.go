package handlers

import (
	"context"
	"encoding/json"
	"net/http/httptest"
	"testing"

	"github.com/Delivergate-Dev/pos-service-golang/api"
	"github.com/Delivergate-Dev/pos-service-golang/tenant"
	"github.com/gofiber/fiber/v2"
	"github.com/stretchr/testify/assert"
)

type mockedItemCategoryService struct{}

func (m *mockedItemCategoryService) GetItemCategoriesByID(ctx context.Context, shopId, brandId, mainMenuId uint64) (json.RawMessage, error) {
	return json.RawMessage(`{"tenant": "test", "mainMenuId": 1, "brandId": 1, "shopId": 1}`), nil
}

func setupTest(t *testing.T, itemCategoryService *mockedItemCategoryService) *fiber.App {
	t.Helper()

	app := fiber.New(fiber.Config{
		ErrorHandler: api.ErrorHandler,
	})

	app.Use(func(c *fiber.Ctx) error {
		c.Locals("tenant", &tenant.Runtime{
			MenuService: itemCategoryService,
		})
		return c.Next()
	})

	handler := NewItemCategoriesHandler()
	app.Get("/main-menu/:mainMenuId/categories/webshop-brand/:brandId/shop/:shopId", handler.GetItemCategories)

	return app

}

func TestGetItemCategories(t *testing.T) {
	app := setupTest(t, new(mockedItemCategoryService))

	t.Run("should return 200 for valid request", func(t *testing.T) {
		req := httptest.NewRequest("GET", "/main-menu/1/categories/webshop-brand/1/shop/1", nil)
		req.Header.Add("Content-Type", "application/json")

		resp, err := app.Test(req)
		assert.Nil(t, err)

		var items api.SuccessResponse[fiber.Map]
		err = json.NewDecoder(resp.Body).Decode(&items)
		assert.Nil(t, err)

		assert.Equal(t, 200, resp.StatusCode)
		assert.Equal(t, "Items fetched successfully", items.Message)
		assert.Equal(t, "test", items.Data["tenant"])
		assert.Equal(t, float64(1), items.Data["mainMenuId"])
		assert.Equal(t, float64(1), items.Data["brandId"])
		assert.Equal(t, float64(1), items.Data["shopId"])
	})

	t.Run("should return 400 for invalid main menu id", func(t *testing.T) {
		req := httptest.NewRequest("GET", "/main-menu/invalid/categories/webshop-brand/1/shop/1", nil)
		req.Header.Add("Content-Type", "application/json")

		resp, err := app.Test(req)
		assert.Nil(t, err)

		assert.Equal(t, 400, resp.StatusCode)
	})

	t.Run("should return 400 for invalid brand id", func(t *testing.T) {
		req := httptest.NewRequest("GET", "/main-menu/1/categories/webshop-brand/invalid/shop/1", nil)
		req.Header.Add("Content-Type", "application/json")

		resp, err := app.Test(req)
		assert.Nil(t, err)

		assert.Equal(t, 400, resp.StatusCode)
	})

	t.Run("should return 400 for invalid shop id", func(t *testing.T) {
		req := httptest.NewRequest("GET", "/main-menu/1/categories/webshop-brand/1/shop/invalid", nil)
		req.Header.Add("Content-Type", "application/json")

		resp, err := app.Test(req)
		assert.Nil(t, err)

		assert.Equal(t, 400, resp.StatusCode)
	})

}

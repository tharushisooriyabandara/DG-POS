package database

import "github.com/gofiber/fiber/v2"

type Cache interface {
	fiber.Storage
}

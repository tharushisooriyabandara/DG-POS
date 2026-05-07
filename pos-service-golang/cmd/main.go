package main

import (
	"os"
	"os/signal"
	"syscall"

	"github.com/Delivergate-Dev/pos-service-golang/api"
	awskms "github.com/Delivergate-Dev/pos-service-golang/cryptography/aws-kms"
	"github.com/Delivergate-Dev/pos-service-golang/database"
	"github.com/Delivergate-Dev/pos-service-golang/env"
	"github.com/Delivergate-Dev/pos-service-golang/logger"
	awssqs "github.com/Delivergate-Dev/pos-service-golang/mq/aws-sqs"
	"github.com/Delivergate-Dev/pos-service-golang/routes"
	"github.com/Delivergate-Dev/pos-service-golang/tenant"
	"github.com/Delivergate-Dev/pos-service-golang/validator"
	"github.com/gofiber/fiber/v2"
	"go.uber.org/zap"
)

func main() {
	// configs,loggers and validator
	env.MustLoad()
	logger.Init()
	validator.Init()

	// must connect db
	database.MustConnectMasterDB()

	// initialize aws
	awskms.InitKMSClient()
	awskms.InitKeyManager()
	awssqs.InitQueueManager()

	// initialize tenant factory
	tenant.InitializeTenantFactory()

	app := setupServer()
	go startServer(app)

	waitAndShutdown(app)
}

func setupServer() *fiber.App {
	app := fiber.New(fiber.Config{
		ErrorHandler: api.ErrorHandler,
	})

	// Setup routes and middleware
	routes.Setup(app)

	app.Hooks().OnListen(func(listenData fiber.ListenData) error {
		logger.Info("Server started",
			zap.String("port", listenData.Port),
		)
		return nil
	})

	app.Hooks().OnShutdown(func() error {
		logger.Info("Server shutting down")
		return nil
	})

	return app
}

func startServer(app *fiber.App) {
	if err := app.Listen(":" + env.Config.Port); err != nil {
		logger.Fatal("Failed to start server", zap.Error(err))
	}
}

func waitAndShutdown(app *fiber.App) {
	c := make(chan os.Signal, 1)
	signal.Notify(c, os.Interrupt, syscall.SIGTERM)
	<-c
	logger.Info("Shutdown signal received")

	if err := app.Shutdown(); err != nil {
		logger.Error("Failed to shutdown server", zap.Error(err))
	}

	tenant.ShutdownFactory()
	database.GetMasterDB().Close()

	logger.Info("Server stopped")
}

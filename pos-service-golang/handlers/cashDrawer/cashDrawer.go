package handlers

import (
	"github.com/Delivergate-Dev/pos-service-golang/api"
	posErr "github.com/Delivergate-Dev/pos-service-golang/errors"
	"github.com/Delivergate-Dev/pos-service-golang/handlers/httpErr"
	"github.com/Delivergate-Dev/pos-service-golang/service"
	"github.com/Delivergate-Dev/pos-service-golang/tenant"
	"github.com/Delivergate-Dev/pos-service-golang/types"
	"github.com/Delivergate-Dev/pos-service-golang/validator"
	"github.com/gofiber/fiber/v2"
)

type CashDrawerHandler struct {
}

func NewCashDrawerHandler() *CashDrawerHandler {
	return &CashDrawerHandler{}
}

func (h *CashDrawerHandler) CreateCashDrawer(c *fiber.Ctx) error {
	createCashDrawerRequest := types.CreateCashDrawerRequest{
		Requestor: c.Locals("user").(types.SessionUser),
	}

	runtime := c.Locals("tenant").(*tenant.Runtime)
	if err := runtime.CashDrawerService.CreateCashDrawer(c.Context(), createCashDrawerRequest); err != nil {
		return err
	}

	return api.Ok(c, "Cash drawer created successfully", struct{}{})
}

func (h *CashDrawerHandler) GetActiveCashDrawerSessionInfo(c *fiber.Ctx) error {
	var req types.GetActiveCashDrawerSessionInfoRequest
	req.Requestor = c.Locals("user").(types.SessionUser)
	if err := c.QueryParser(&req); err != nil {
		return api.BadRequest("Invalid request", err.Error())
	}

	runtime := c.Locals("tenant").(*tenant.Runtime)
	resp, err := runtime.CashDrawerService.GetActiveCashDrawerSessionInfo(c.Context(), req)
	if err != nil {
		if err == service.ErrNoActiveCashDrawerSession {
			return api.NotFound("No active cash drawer session found")
		}
		return err
	}

	return api.Ok(c, "Active cash drawer session fetched successfully", resp)
}

func (h *CashDrawerHandler) GetCashDrawerTransactionHistory(c *fiber.Ctx) error {
	var req types.GetCashDrawerTransactionHistoryRequest
	if err := c.QueryParser(&req); err != nil {
		return api.BadRequest("Invalid request", err.Error())
	}

	if err := validator.Validate(&req); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	req.Requestor = c.Locals("user").(types.SessionUser)
	runtime := c.Locals("tenant").(*tenant.Runtime)
	transactions, err := runtime.CashDrawerService.GetCashDrawerTransactionHistory(c.Context(), req)
	if err != nil {
		return err
	}

	return api.Ok(c, "Cash drawer transaction history fetched successfully", transactions)
}

func (h *CashDrawerHandler) GetCashDrawerSession(c *fiber.Ctx) error {
	sessionID, err := c.ParamsInt("id")
	if err != nil || sessionID == 0 {
		return api.BadRequest("Invalid session ID", err.Error())
	}

	runtime := c.Locals("tenant").(*tenant.Runtime)
	session, err := runtime.CashDrawerService.GetCashDrawerSession(c.Context(), uint64(sessionID))
	if err != nil {
		return err
	}

	return api.Ok(c, "Cash drawer session fetched successfully", session)
}

func (h *CashDrawerHandler) GetCashDrawerSessions(c *fiber.Ctx) error {
	var req types.GetCashDrawerSessionsRequest
	if err := c.QueryParser(&req); err != nil {
		return api.BadRequest("Invalid request", err.Error())
	}

	if err := validator.Validate(&req); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	req.Requestor = c.Locals("user").(types.SessionUser)
	runtime := c.Locals("tenant").(*tenant.Runtime)
	sessions, err := runtime.CashDrawerService.GetCashDrawerSessions(c.Context(), req)
	if err != nil {
		return err
	}

	return api.Ok(c, "Cash drawer sessions fetched successfully", sessions)

}

func (h *CashDrawerHandler) OpenCashDrawerSession(c *fiber.Ctx) error {
	var openCashDrawerSessionRequest types.OpenCashDrawerSessionRequest
	if err := c.BodyParser(&openCashDrawerSessionRequest); err != nil {
		return api.BadRequest("Invalid request", err.Error())
	}

	if err := validator.Validate(&openCashDrawerSessionRequest); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	openCashDrawerSessionRequest.Requestor = c.Locals("user").(types.SessionUser)
	runtime := c.Locals("tenant").(*tenant.Runtime)

	if err := runtime.CashDrawerService.OpenCashDrawerSession(c.Context(), openCashDrawerSessionRequest); err != nil {
		return err
	}

	return api.Ok(c, "Cash drawer session opened successfully", struct{}{})
}

func (h *CashDrawerHandler) CloseCashDrawerSession(c *fiber.Ctx) error {
	var closeCashDrawerSessionRequest types.CloseCashDrawerSessionRequest
	if err := c.BodyParser(&closeCashDrawerSessionRequest); err != nil {
		return api.BadRequest("Invalid request", err.Error())
	}

	if err := validator.Validate(&closeCashDrawerSessionRequest); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	closeCashDrawerSessionRequest.Requestor = c.Locals("user").(types.SessionUser)
	runtime := c.Locals("tenant").(*tenant.Runtime)
	if err := runtime.CashDrawerService.CloseCashDrawerSession(c.Context(), closeCashDrawerSessionRequest); err != nil {
		if _, ok := err.(service.CannotEndSessionError); ok {
			return api.BadRequest("Cannot End Shift", err.Error())
		}
		return err
	}

	return api.Ok(c, "Cash drawer session closed successfully", struct{}{})
}

func (h *CashDrawerHandler) RecordCashMovement(c *fiber.Ctx) error {
	var recordCashMovementRequest types.RecordCashMovementRequest
	if err := c.BodyParser(&recordCashMovementRequest); err != nil {
		return api.BadRequest("Invalid request", err.Error())
	}

	if err := validator.Validate(&recordCashMovementRequest); err != nil {
		return api.BadRequest("Validation failed", validator.TranslateErrors(err))
	}

	recordCashMovementRequest.Requestor = c.Locals("user").(types.SessionUser)
	recordCashMovementRequest.IsAPIRequest = true
	runtime := c.Locals("tenant").(*tenant.Runtime)
	if err := runtime.CashDrawerService.RecordCashMovement(c.Context(), recordCashMovementRequest); err != nil {
		if err, ok := err.(posErr.SlugError); ok {
			return httpErr.RespondWithSlugError(err)
		}
		return err
	}

	return api.Ok(c, "Cash movement recorded successfully", struct{}{})
}

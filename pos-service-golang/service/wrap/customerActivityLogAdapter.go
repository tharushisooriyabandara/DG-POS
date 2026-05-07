package wrap

import (
	"context"
	"fmt"

	"github.com/Delivergate-Dev/pos-service-golang/types"
)

type customerActivityLogAdapter struct {
	customerService
	activityLogService
}

func NewCustomerActivityLogger(activityLog activityLogService, service customerService) customerService {
	return &customerActivityLogAdapter{
		activityLogService: activityLog,
		customerService:    service,
	}
}

func (a *customerActivityLogAdapter) CreateCustomer(ctx context.Context, createCustomerRequest *types.CreateCustomerRequest) (*types.GetCustomersResponse, error) {

	customer, err := a.customerService.CreateCustomer(ctx, createCustomerRequest)
	if err != nil {
		return nil, err
	}

	a.activityLogService.CreateActivity(ctx, &types.LogActivityRequest{
		Requestor:   createCustomerRequest.Requestor,
		Event:       "create",
		Subject:     "customer",
		SubjectId:   customer.ID,
		Description: fmt.Sprintf("Created customer %s %s", customer.FirstName, customer.LastName),
	})

	return customer, nil
}

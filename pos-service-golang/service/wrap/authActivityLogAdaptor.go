package wrap

import (
	"context"

	"github.com/Delivergate-Dev/pos-service-golang/types"
)

type userActivityLogAdapter struct {
	authService
	activityLogService
}

func NewUserActivityLogger(activityLog activityLogService, service authService) authService {
	return &userActivityLogAdapter{
		activityLogService: activityLog,
		authService:        service,
	}
}

func (a *userActivityLogAdapter) Authenticate(ctx context.Context, loginRequest types.LoginRequest) (*types.SessionUser, string, string, error) {
	sessionUser, accessToken, refreshToken, err := a.authService.Authenticate(ctx, loginRequest)
	if err != nil {
		return nil, "", "", err
	}

	a.activityLogService.CreateActivity(ctx, &types.LogActivityRequest{
		Requestor:   *sessionUser,
		Event:       "login",
		Subject:     "user",
		SubjectId:   sessionUser.ID,
		Description: "Logged in",
	})

	return sessionUser, accessToken, refreshToken, nil
}

func (a *userActivityLogAdapter) InvalidateRefreshToken(ctx context.Context, user types.SessionUser) error {
	err := a.authService.InvalidateRefreshToken(ctx, user)
	if err != nil {
		return err
	}

	a.activityLogService.CreateActivity(ctx, &types.LogActivityRequest{
		Requestor:   user,
		Event:       "logout",
		Subject:     "user",
		SubjectId:   user.ID,
		Description: "Logged out",
	})

	return nil
}

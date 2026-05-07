package convert

import (
	"strconv"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/types"
)

func DBUserToUserResp(users []*db.GetUsersRow) []*types.GetUsersResponse {
	userResponses := make([]*types.GetUsersResponse, len(users))
	for i, user := range users {
		userResponses[i] = &types.GetUsersResponse{
			ID:        user.ID,
			FirstName: user.Name,
			LastName:  user.LastName.String,
			ContactNo: user.ContactNo.String,
			Email:     user.Email,
			Address:   user.Address.String,
			Role:      user.RoleName.String,
			RoleId:    strconv.Itoa(int(user.RoleID.Int32)),
		}
	}

	return userResponses
}

func DBUserToUser(user *db.GetUserByIDRow, reportServiceToken string) *types.GetUserResponse {
	return &types.GetUserResponse{
		ID:                 user.ID,
		FirstName:          user.Name,
		LastName:           user.LastName.String,
		Email:              user.Email,
		Address:            user.Address.String,
		ContactNo:          user.ContactNo.String,
		Status:             user.Status.String,
		Role:               user.RoleName.String,
		RoleId:             strconv.Itoa(int(user.RoleID.Int32)),
		ReportServiceToken: reportServiceToken,
		CreatedAt:          user.CreatedAt.Time,
		UpdatedAt:          user.UpdatedAt.Time,
	}
}

package httpErr

import (
	"github.com/Delivergate-Dev/pos-service-golang/api"
	posErr "github.com/Delivergate-Dev/pos-service-golang/errors"
)

func RespondWithSlugError(err posErr.SlugError) error {
	switch err.ErrorType() {
	case posErr.ErrorTypeAuthorization:
		return api.Unauthorized(err.Error(), err.Slug())
	case posErr.ErrorTypeIncorrectInput:
		return api.BadRequest(err.Error(), err.Slug())
	}
	return err
}

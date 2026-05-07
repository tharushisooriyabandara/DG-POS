package validator

import (
	"maps"
	"slices"
	"strings"

	"github.com/go-playground/locales/en"
	ut "github.com/go-playground/universal-translator"
	"github.com/go-playground/validator/v10"
	en_translations "github.com/go-playground/validator/v10/translations/en"
)

var (
	validate     *validator.Validate
	enTranslator ut.Translator
)

func Init() {
	validate = validator.New(validator.WithRequiredStructEnabled())
	en := en.New()
	uni := ut.New(en, en)

	enTranslator, _ = uni.GetTranslator("en")
	en_translations.RegisterDefaultTranslations(validate, enTranslator)
}

// Validate validates a struct and returns validation errors
func Validate(i interface{}) validator.ValidationErrors {
	if err := validate.Struct(i); err != nil {
		errs := err.(validator.ValidationErrors)
		return errs
	}
	return nil
}

func TranslateErrors(errs validator.ValidationErrors) string {
	valuesIter := maps.Values(errs.Translate(enTranslator))
	values := slices.Collect(valuesIter)

	return strings.Join(values, ", ") + "."
}

// RegisterCustomValidation registers a custom validation function
func RegisterCustomValidation(tag string, fn validator.Func) error {
	return validate.RegisterValidation(tag, fn)
}

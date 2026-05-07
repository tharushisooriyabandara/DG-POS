package env

import (
	"log"
	"os"
	"strconv"
	"time"

	"github.com/joho/godotenv"
)

var Config *config

type config struct {
	Host                    string
	Port                    string
	MasterDBHost            string
	MasterDBPort            string
	MasterDBName            string
	MasterDbUsername        string
	MasterDbPassword        string
	JWTSecret               string
	AccessTokenExp          string
	RefreshTokenExp         string
	Environment             string
	ErrorLogFile            string
	ErrorLogMaxSize         string
	ErrorLogMaxAge          string
	ErrorLogMaxBackups      string
	ErrorLogCompress        string
	CacheExpiry             string
	PosPlatformID           string
	AwsRegion               string
	AwsAccessKeyId          string
	AwsSecretAccessKey      string
	AwsSqsPrefix            string
	AwsSqsOrderQueue        string
	AwsSqsCustomerQueue     string
	AwsSqsTransactionQueue  string
	UserServiceUrl          string
	UserServiceClientId     string
	UserServiceClientSecret string
}

// MustLoad loads the configuration from the environment variables. panics if any required variables are not set
func MustLoad() {

	err := godotenv.Load()
	if err != nil {
		log.Fatal("Error loading .env file: ", err)
	}

	config := &config{
		Host:                    getEnv("API_HOST", "localhost"),
		Port:                    getEnv("API_PORT", "8000"),
		MasterDBHost:            getEnv("MASTER_DB_HOST", "localhost"),
		MasterDBPort:            getEnv("MASTER_DB_PORT", "3306"),
		MasterDBName:            getEnv("MASTER_DB_NAME", "dg_developer"),
		MasterDbUsername:        getEnv("MASTER_USERNAME", "root"),
		MasterDbPassword:        getEnv("MASTER_PASSWORD", ""),
		JWTSecret:               getEnv("JWT_SECRET", ""),
		AccessTokenExp:          getEnv("JWT_ACCESS_TOKEN_EXPIRY", "24h"),
		RefreshTokenExp:         getEnv("JWT_REFRESH_TOKEN_EXPIRY", "168h"),
		Environment:             getEnv("ENV", "development"),
		ErrorLogFile:            getEnv("ERROR_LOG_FILE", "error-log.json"),
		ErrorLogMaxSize:         getEnv("ERROR_LOG_MAX_SIZE", "10"),
		ErrorLogMaxAge:          getEnv("ERROR_LOG_MAX_AGE", "28"),
		ErrorLogMaxBackups:      getEnv("ERROR_LOG_MAX_BACKUPS", "3"),
		ErrorLogCompress:        getEnv("ERROR_LOG_COMPRESS", "true"),
		CacheExpiry:             getEnv("CACHE_EXPIRY", "5m"),
		PosPlatformID:           getEnv("POS_PLATFORM_ID", "9"),
		AwsRegion:               getEnv("AWS_REGION", "eu-west-2"),
		AwsAccessKeyId:          getEnv("AWS_ACCESS_KEY_ID", ""),
		AwsSecretAccessKey:      getEnv("AWS_SECRET_ACCESS_KEY", ""),
		AwsSqsPrefix:            getEnv("AWS_SQS_PREFIX", ""),
		AwsSqsOrderQueue:        getEnv("AWS_SQS_ORDER_QUEUE", ""),
		AwsSqsCustomerQueue:     getEnv("AWS_SQS_CUSTOMER_QUEUE", ""),
		AwsSqsTransactionQueue:  getEnv("AWS_SQS_TRANSACTION_QUEUE", ""),
		UserServiceUrl:          getEnv("USER_SERVICE_URL", ""),
		UserServiceClientId:     getEnv("USER_SERVICE_CLIENT_ID", ""),
		UserServiceClientSecret: getEnv("USER_SERVICE_CLIENT_SECRET", ""),
	}

	mustParseTimeDurations(
		config.AccessTokenExp,
		config.RefreshTokenExp,
		config.CacheExpiry,
	)
	mustParseIntegers(
		config.ErrorLogMaxSize,
		config.ErrorLogMaxAge,
		config.ErrorLogMaxBackups,
		config.PosPlatformID,
	)
	mustParseBool(config.ErrorLogCompress)

	Config = config
}

func getEnv(key, defaultValue string) string {
	value, exists := os.LookupEnv(key)

	// value doesn't exist, and no default value is provided
	if !exists && defaultValue == "" {
		panic("Required environment variable '" + key + "' is not set")
	}

	if value == "" {
		return defaultValue
	}

	return value
}

func mustParseTimeDurations(values ...string) {
	for _, value := range values {
		_, err := time.ParseDuration(value)
		if err != nil {
			panic("Failed to parse time duration: " + err.Error())
		}
	}
}

func mustParseIntegers(values ...string) {
	for _, value := range values {
		_, err := strconv.Atoi(value)
		if err != nil {
			panic("Failed to parse integer: " + err.Error())
		}
	}
}

func mustParseBool(values ...string) {
	for _, value := range values {
		_, err := strconv.ParseBool(value)
		if err != nil {
			panic("Failed to parse boolean: " + err.Error())
		}
	}
}

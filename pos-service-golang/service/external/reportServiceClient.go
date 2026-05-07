package external

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"net/url"
	"time"

	db "github.com/Delivergate-Dev/pos-service-golang/database/sqlc"
	"github.com/Delivergate-Dev/pos-service-golang/env"
)

func GenerateReportServiceBearerToken(tenantCode string, reportServiceData db.ReportService) (string, error) {

	accessToken, err := getAccessToken(tenantCode)
	if err != nil {
		return "", err
	}

	reportServiceUrl, err := url.JoinPath(reportServiceData.Dns.String, "oauth/token")
	if err != nil {
		return "", fmt.Errorf("generating report service token: error joining url: %w", err)
	}
	reportServiceReqBody := map[string]string{
		"grant_type":    "client_credentials",
		"client_id":     reportServiceData.ServerClientID.String,
		"client_secret": reportServiceData.ServerClientSecret.String,
	}
	jsonBody, err := json.Marshal(reportServiceReqBody)
	if err != nil {
		return "", fmt.Errorf("generating report service token: error marshalling request body: %w", err)
	}

	req, err := http.NewRequest(http.MethodPost, reportServiceUrl, bytes.NewBuffer(jsonBody))
	if err != nil {
		return "", fmt.Errorf("generating report service token: error creating request: %w", err)
	}
	req.Header.Add("X-Tenant-Code", tenantCode)
	req.Header.Add("Authorization", fmt.Sprintf("Bearer %s", accessToken))
	req.Header.Add("Content-Type", "application/json")

	client := http.Client{
		Timeout: 10 * time.Second,
	}

	resp, err := client.Do(req)
	if err != nil {
		return "", fmt.Errorf("generating report service token: error sending request: %w", err)
	}

	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		body, err := io.ReadAll(resp.Body)
		if err != nil {
			return "", fmt.Errorf("generating report service token: error reading response body: %w", err)
		}
		fmt.Println("resp body: ", string(body))

		fmt.Println("access token: ", accessToken)
		fmt.Println("report service url: ", reportServiceUrl)
		fmt.Println("report service req body: ", string(jsonBody))
		fmt.Println("headers: ", req.Header)

		return "", fmt.Errorf("generating report service token: error fetching access token: %s", resp.Status)
	}

	var tokenResponse struct {
		AccessToken string `json:"access_token"`
	}
	if err := json.NewDecoder(resp.Body).Decode(&tokenResponse); err != nil {
		return "", fmt.Errorf("generating report service token: error decoding response: %w", err)
	}

	return tokenResponse.AccessToken, nil
}

func getAccessToken(tenantCode string) (string, error) {

	userServiceUrl, err := url.JoinPath(env.Config.UserServiceUrl, "oauth/token")
	if err != nil {
		return "", fmt.Errorf("getting access token: error joining url: %w", err)
	}
	userServiceReqBody := map[string]string{
		"grant_type":    "client_credentials",
		"client_id":     env.Config.UserServiceClientId,
		"client_secret": env.Config.UserServiceClientSecret,
	}

	jsonBody, err := json.Marshal(userServiceReqBody)
	if err != nil {
		return "", fmt.Errorf("getting access token: error marshalling request body: %w", err)
	}

	req, err := http.NewRequest(http.MethodPost, userServiceUrl, bytes.NewBuffer(jsonBody))
	if err != nil {
		return "", fmt.Errorf("getting access token: error creating request: %w", err)
	}
	req.Header.Add("X-Tenant-Code", tenantCode)
	req.Header.Add("Content-Type", "application/json")

	client := http.Client{
		Timeout: 10 * time.Second,
	}

	resp, err := client.Do(req)
	if err != nil {
		return "", fmt.Errorf("getting access token: error sending request: %w", err)
	}

	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		return "", fmt.Errorf("getting access token: error fetching access token: %s", resp.Status)
	}

	var tokenResponse struct {
		AccessToken string `json:"access_token"`
	}
	if err := json.NewDecoder(resp.Body).Decode(&tokenResponse); err != nil {
		return "", fmt.Errorf("getting access token: error decoding response: %w", err)
	}

	return tokenResponse.AccessToken, nil
}

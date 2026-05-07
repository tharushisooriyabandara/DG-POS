package awskms

import (
	"context"
	"errors"
	"fmt"

	"github.com/Delivergate-Dev/pos-service-golang/env"
	"github.com/aws/aws-sdk-go-v2/config"
	"github.com/aws/aws-sdk-go-v2/credentials"
	"github.com/aws/aws-sdk-go-v2/service/kms"
)

var kmsClient *Client

type Client struct {
	*kms.Client
}

func InitKMSClient() {
	cfg, err := config.LoadDefaultConfig(context.Background(),
		config.WithRegion(env.Config.AwsRegion),
		config.WithCredentialsProvider(
			credentials.NewStaticCredentialsProvider(
				env.Config.AwsAccessKeyId,
				env.Config.AwsSecretAccessKey,
				"",
			),
		),
	)
	if err != nil {
		panic(fmt.Errorf("failed to load aws config: %w", err))
	}

	kmsClient = &Client{
		Client: kms.NewFromConfig(cfg),
	}
}

func decryptDEK(ctx context.Context, encryptedDEK []byte) ([]byte, error) {
	resp, err := kmsClient.Decrypt(ctx, &kms.DecryptInput{
		CiphertextBlob: encryptedDEK,
	})
	if err != nil {
		return nil, fmt.Errorf("failed to decrypt DEK: %w", err)
	}

	if len(resp.Plaintext) != 32 {
		return nil, errors.New("unexpected DEK size (need 32 bytes for AES-256)")
	}

	return resp.Plaintext, nil
}

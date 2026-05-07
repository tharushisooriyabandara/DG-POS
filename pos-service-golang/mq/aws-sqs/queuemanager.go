package awssqs

import (
	"context"
	"fmt"

	"github.com/Delivergate-Dev/pos-service-golang/env"
	"github.com/Delivergate-Dev/pos-service-golang/logger"
	"github.com/aws/aws-sdk-go-v2/aws"
	"github.com/aws/aws-sdk-go-v2/config"
	"github.com/aws/aws-sdk-go-v2/credentials"
	"github.com/aws/aws-sdk-go-v2/service/sqs"
	"go.uber.org/zap"
)

var queueManager *QueueManager

type QueueManager struct {
	client      *sqs.Client
	connections map[string]string
	logger      *zap.Logger
}

func InitQueueManager() {
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

	queueManager = &QueueManager{
		client: sqs.NewFromConfig(cfg),
		logger: logger.With(zap.String("module", "aws-sqs")),
		connections: map[string]string{
			"orders":       fmt.Sprintf("%s/%s", env.Config.AwsSqsPrefix, env.Config.AwsSqsOrderQueue),
			"customers":    fmt.Sprintf("%s/%s", env.Config.AwsSqsPrefix, env.Config.AwsSqsCustomerQueue),
			"transactions": fmt.Sprintf("%s/%s", env.Config.AwsSqsPrefix, env.Config.AwsSqsTransactionQueue),
		},
	}
}

type Message struct {
	QueueName       string
	MessageBody     string
	MessageGroupId  string
	DeduplicationID string
}

func Send(ctx context.Context, msg Message) {
	_, err := queueManager.client.SendMessage(
		ctx,
		&sqs.SendMessageInput{
			QueueUrl:               aws.String(queueManager.connections[msg.QueueName]),
			MessageBody:            aws.String(msg.MessageBody),
			MessageGroupId:         aws.String(msg.MessageGroupId),
			MessageDeduplicationId: aws.String(msg.DeduplicationID),
		},
	)
	if err != nil {
		queueManager.logger.Error("Failed to send message", zap.Error(err))
	}
}

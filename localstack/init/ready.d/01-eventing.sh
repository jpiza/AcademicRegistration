#!/bin/sh
set -eu

EVENT_BUS_NAME="academic-registration"
QUEUE_NAME="academic-registration-notifications"
DLQ_NAME="academic-registration-notifications-dlq"
RULE_NAME="route-student-notifications"
EVENT_PATTERN='{"source":["academic-registration.api"],"detail-type":["student.registered","student.enrollment.changed"]}'

awslocal events create-event-bus \
  --name "$EVENT_BUS_NAME" >/dev/null 2>&1 || true

DLQ_URL="$(awslocal sqs create-queue \
  --queue-name "$DLQ_NAME" \
  --query QueueUrl \
  --output text)"

DLQ_ARN="$(awslocal sqs get-queue-attributes \
  --queue-url "$DLQ_URL" \
  --attribute-names QueueArn \
  --query 'Attributes.QueueArn' \
  --output text)"

REDRIVE_POLICY="{\"deadLetterTargetArn\":\"$DLQ_ARN\",\"maxReceiveCount\":\"3\"}"

QUEUE_URL="$(awslocal sqs create-queue \
  --queue-name "$QUEUE_NAME" \
  --attributes RedrivePolicy="$REDRIVE_POLICY" \
  --query QueueUrl \
  --output text)"

QUEUE_ARN="$(awslocal sqs get-queue-attributes \
  --queue-url "$QUEUE_URL" \
  --attribute-names QueueArn \
  --query 'Attributes.QueueArn' \
  --output text)"

QUEUE_POLICY="$(cat <<EOF
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "AllowEventBridgeSendMessage",
      "Effect": "Allow",
      "Principal": {
        "Service": "events.amazonaws.com"
      },
      "Action": "sqs:SendMessage",
      "Resource": "$QUEUE_ARN"
    }
  ]
}
EOF
)"

awslocal sqs set-queue-attributes \
  --queue-url "$QUEUE_URL" \
  --attributes Policy="$QUEUE_POLICY"

awslocal events put-rule \
  --event-bus-name "$EVENT_BUS_NAME" \
  --name "$RULE_NAME" \
  --event-pattern "$EVENT_PATTERN" >/dev/null

awslocal events put-targets \
  --event-bus-name "$EVENT_BUS_NAME" \
  --rule "$RULE_NAME" \
  --targets "Id=student-notifications,Arn=$QUEUE_ARN" >/dev/null

echo "Academic Registration local eventing is ready: $EVENT_BUS_NAME -> $QUEUE_NAME"

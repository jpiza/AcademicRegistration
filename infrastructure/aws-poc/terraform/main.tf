data "aws_availability_zones" "available" {
  state = "available"
}

data "aws_caller_identity" "current" {}

data "aws_cloudfront_cache_policy" "caching_disabled" {
  name = "Managed-CachingDisabled"
}

data "aws_cloudfront_origin_request_policy" "all_viewer_except_host_header" {
  name = "Managed-AllViewerExceptHostHeader"
}

data "aws_ec2_managed_prefix_list" "cloudfront_origin_facing" {
  name = "com.amazonaws.global.cloudfront.origin-facing"
}

locals {
  name = "${var.project_name}-${var.environment}"

  azs = slice(data.aws_availability_zones.available.names, 0, var.az_count)

  db_engine_normalized = lower(var.db_engine)
  use_mysql            = local.db_engine_normalized == "mysql"
  rds_engine           = local.use_mysql ? "mysql" : "sqlserver-ex"
  rds_identifier       = "${local.name}-${local.use_mysql ? "mysql" : "sqlserver"}"
  rds_port             = local.use_mysql ? 3306 : 1433
  rds_instance_class   = var.db_instance_class != "" ? var.db_instance_class : (local.use_mysql ? "db.t3.micro" : "db.t3.small")
  app_db_provider      = local.use_mysql ? "MySQL" : "SQL"
  app_db_secret_name   = local.use_mysql ? "Configuracion__CadenasConexion__ConexionMySQL" : "Configuracion__CadenasConexion__ConexionSQL"

  tags = merge(
    {
      Project     = var.project_name
      Environment = var.environment
      ManagedBy   = "terraform"
    },
    var.tags
  )
}

resource "random_id" "suffix" {
  byte_length = 3
}

resource "random_password" "db_password" {
  length           = 24
  special          = true
  override_special = "!#$%&*()-_=+[]{}<>:?"
}

resource "aws_kms_key" "app" {
  description             = "KMS key for ${local.name} PoC secrets and storage"
  deletion_window_in_days = 7
  enable_key_rotation     = true
}

resource "aws_kms_alias" "app" {
  name          = "alias/${local.name}"
  target_key_id = aws_kms_key.app.key_id
}

resource "aws_vpc" "this" {
  cidr_block           = var.vpc_cidr
  enable_dns_hostnames = true
  enable_dns_support   = true

  tags = {
    Name = local.name
  }
}

resource "aws_internet_gateway" "this" {
  vpc_id = aws_vpc.this.id

  tags = {
    Name = local.name
  }
}

resource "aws_subnet" "public" {
  count = length(local.azs)

  vpc_id                  = aws_vpc.this.id
  availability_zone       = local.azs[count.index]
  cidr_block              = cidrsubnet(var.vpc_cidr, 4, count.index)
  map_public_ip_on_launch = true

  tags = {
    Name = "${local.name}-public-${count.index + 1}"
    Tier = "public"
  }
}

resource "aws_subnet" "private" {
  count = length(local.azs)

  vpc_id            = aws_vpc.this.id
  availability_zone = local.azs[count.index]
  cidr_block        = cidrsubnet(var.vpc_cidr, 4, count.index + 8)

  tags = {
    Name = "${local.name}-private-${count.index + 1}"
    Tier = "private"
  }
}

resource "aws_route_table" "public" {
  vpc_id = aws_vpc.this.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.this.id
  }

  tags = {
    Name = "${local.name}-public"
  }
}

resource "aws_route_table_association" "public" {
  count = length(aws_subnet.public)

  subnet_id      = aws_subnet.public[count.index].id
  route_table_id = aws_route_table.public.id
}

resource "aws_security_group" "alb" {
  name        = "${local.name}-alb"
  description = "Public ALB access"
  vpc_id      = aws_vpc.this.id
}

resource "aws_security_group_rule" "alb_http_in" {
  type              = "ingress"
  security_group_id = aws_security_group.alb.id
  from_port         = 80
  to_port           = 80
  protocol          = "tcp"
  cidr_blocks       = var.restrict_alb_to_cloudfront ? null : ["0.0.0.0/0"]
  prefix_list_ids   = var.restrict_alb_to_cloudfront ? [data.aws_ec2_managed_prefix_list.cloudfront_origin_facing.id] : null
}

resource "aws_security_group_rule" "alb_egress" {
  type              = "egress"
  security_group_id = aws_security_group.alb.id
  from_port         = 0
  to_port           = 0
  protocol          = "-1"
  cidr_blocks       = ["0.0.0.0/0"]
}

resource "aws_security_group" "ecs" {
  name        = "${local.name}-ecs"
  description = "ECS task network access"
  vpc_id      = aws_vpc.this.id
}

resource "aws_security_group_rule" "ecs_gateway_from_alb" {
  type                     = "ingress"
  security_group_id        = aws_security_group.ecs.id
  from_port                = 5080
  to_port                  = 5080
  protocol                 = "tcp"
  source_security_group_id = aws_security_group.alb.id
}

resource "aws_security_group_rule" "ecs_service_discovery_self" {
  type              = "ingress"
  security_group_id = aws_security_group.ecs.id
  from_port         = 5081
  to_port           = 5081
  protocol          = "tcp"
  self              = true
}

resource "aws_security_group_rule" "ecs_egress" {
  type              = "egress"
  security_group_id = aws_security_group.ecs.id
  from_port         = 0
  to_port           = 0
  protocol          = "-1"
  cidr_blocks       = ["0.0.0.0/0"]
}

resource "aws_security_group" "rds" {
  name        = "${local.name}-rds"
  description = "RDS database access from ECS"
  vpc_id      = aws_vpc.this.id
}

resource "aws_security_group_rule" "rds_from_ecs" {
  type                     = "ingress"
  security_group_id        = aws_security_group.rds.id
  from_port                = local.rds_port
  to_port                  = local.rds_port
  protocol                 = "tcp"
  source_security_group_id = aws_security_group.ecs.id
}

resource "aws_security_group_rule" "rds_egress" {
  type              = "egress"
  security_group_id = aws_security_group.rds.id
  from_port         = 0
  to_port           = 0
  protocol          = "-1"
  cidr_blocks       = ["0.0.0.0/0"]
}

resource "aws_db_subnet_group" "this" {
  name       = local.name
  subnet_ids = aws_subnet.private[*].id
}

moved {
  from = aws_db_instance.sqlserver
  to   = aws_db_instance.database
}

resource "aws_db_instance" "database" {
  identifier              = local.rds_identifier
  engine                  = local.rds_engine
  instance_class          = local.rds_instance_class
  allocated_storage       = var.db_allocated_storage
  storage_type            = var.db_storage_type
  storage_encrypted       = true
  kms_key_id              = aws_kms_key.app.arn
  username                = var.db_username
  password                = random_password.db_password.result
  db_name                 = local.use_mysql ? var.db_name : null
  db_subnet_group_name    = aws_db_subnet_group.this.name
  vpc_security_group_ids  = [aws_security_group.rds.id]
  publicly_accessible     = false
  backup_retention_period = 1
  deletion_protection     = var.db_deletion_protection
  skip_final_snapshot     = var.skip_final_snapshot
  license_model           = local.use_mysql ? null : "license-included"
  apply_immediately       = true
}

resource "aws_secretsmanager_secret" "db_connection_string" {
  name                    = "${local.name}/api/db-connection-string"
  kms_key_id              = aws_kms_key.app.arn
  recovery_window_in_days = 0
}

resource "aws_secretsmanager_secret_version" "db_connection_string" {
  secret_id = aws_secretsmanager_secret.db_connection_string.id
  secret_string = join("", local.use_mysql ? [
    "server=",
    aws_db_instance.database.address,
    ";port=3306;database=",
    var.db_name,
    ";user=",
    var.db_username,
    ";password=",
    random_password.db_password.result
    ] : [
    "Server=",
    aws_db_instance.database.address,
    ",1433;Database=",
    var.db_name,
    ";User Id=",
    var.db_username,
    ";Password=",
    random_password.db_password.result,
    ";TrustServerCertificate=True;MultipleActiveResultSets=true"
  ])
}

resource "aws_secretsmanager_secret" "smtp_password" {
  name                    = "${local.name}/notifications/smtp-password"
  kms_key_id              = aws_kms_key.app.arn
  recovery_window_in_days = 0
}

resource "aws_secretsmanager_secret_version" "smtp_password" {
  secret_id     = aws_secretsmanager_secret.smtp_password.id
  secret_string = var.smtp_password
}

resource "aws_cloudwatch_log_group" "api" {
  name              = "/ecs/${local.name}/api"
  retention_in_days = var.log_retention_days
}

resource "aws_cloudwatch_log_group" "gateway" {
  name              = "/ecs/${local.name}/gateway"
  retention_in_days = var.log_retention_days
}

resource "aws_cloudwatch_log_group" "worker" {
  name              = "/ecs/${local.name}/notifications-worker"
  retention_in_days = var.log_retention_days
}

resource "aws_cloudwatch_log_group" "xray" {
  name              = "/ecs/${local.name}/xray"
  retention_in_days = var.log_retention_days
}

resource "aws_ecr_repository" "api" {
  name         = "${local.name}/api"
  force_delete = var.force_delete_ecr

  image_scanning_configuration {
    scan_on_push = true
  }
}

resource "aws_ecr_repository" "gateway" {
  name         = "${local.name}/gateway"
  force_delete = var.force_delete_ecr

  image_scanning_configuration {
    scan_on_push = true
  }
}

resource "aws_ecr_repository" "notifications" {
  name         = "${local.name}/notifications"
  force_delete = var.force_delete_ecr

  image_scanning_configuration {
    scan_on_push = true
  }
}

resource "aws_lb" "app" {
  name               = substr(replace(local.name, "_", "-"), 0, 32)
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets            = aws_subnet.public[*].id
}

resource "aws_lb_target_group" "gateway" {
  name        = substr("${replace(local.name, "_", "-")}-gw", 0, 32)
  port        = 5080
  protocol    = "HTTP"
  target_type = "ip"
  vpc_id      = aws_vpc.this.id

  health_check {
    enabled             = true
    path                = "/health"
    matcher             = "200"
    healthy_threshold   = 2
    unhealthy_threshold = 3
    interval            = 30
    timeout             = 5
  }
}

resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.app.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.gateway.arn
  }
}

resource "aws_service_discovery_private_dns_namespace" "this" {
  name = "${local.name}.local"
  vpc  = aws_vpc.this.id
}

resource "aws_service_discovery_service" "api" {
  name = "api"

  dns_config {
    namespace_id = aws_service_discovery_private_dns_namespace.this.id

    dns_records {
      ttl  = 10
      type = "A"
    }

    routing_policy = "MULTIVALUE"
  }

  health_check_custom_config {
    failure_threshold = 1
  }
}

resource "aws_ecs_cluster" "this" {
  name = local.name

  setting {
    name  = "containerInsights"
    value = "enabled"
  }
}

data "aws_iam_policy_document" "ecs_tasks_assume_role" {
  statement {
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["ecs-tasks.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "execution" {
  name               = "${local.name}-ecs-execution"
  assume_role_policy = data.aws_iam_policy_document.ecs_tasks_assume_role.json
}

resource "aws_iam_role_policy_attachment" "execution" {
  role       = aws_iam_role.execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

resource "aws_iam_role_policy" "execution_secrets" {
  name = "${local.name}-ecs-secrets"
  role = aws_iam_role.execution.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "secretsmanager:GetSecretValue"
        ]
        Resource = [
          aws_secretsmanager_secret.db_connection_string.arn,
          aws_secretsmanager_secret.smtp_password.arn
        ]
      },
      {
        Effect = "Allow"
        Action = [
          "kms:Decrypt"
        ]
        Resource = aws_kms_key.app.arn
      }
    ]
  })
}

resource "aws_iam_role" "api_task" {
  name               = "${local.name}-api-task"
  assume_role_policy = data.aws_iam_policy_document.ecs_tasks_assume_role.json
}

resource "aws_iam_role" "gateway_task" {
  name               = "${local.name}-gateway-task"
  assume_role_policy = data.aws_iam_policy_document.ecs_tasks_assume_role.json
}

resource "aws_iam_role" "worker_task" {
  name               = "${local.name}-worker-task"
  assume_role_policy = data.aws_iam_policy_document.ecs_tasks_assume_role.json
}

resource "aws_iam_role_policy" "api_task" {
  name = "${local.name}-api"
  role = aws_iam_role.api_task.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect   = "Allow"
        Action   = ["events:PutEvents"]
        Resource = aws_cloudwatch_event_bus.academic_registration.arn
      },
      {
        Effect = "Allow"
        Action = [
          "xray:PutTraceSegments",
          "xray:PutTelemetryRecords"
        ]
        Resource = "*"
      }
    ]
  })
}

resource "aws_iam_role_policy" "gateway_task" {
  name = "${local.name}-gateway"
  role = aws_iam_role.gateway_task.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "xray:PutTraceSegments",
          "xray:PutTelemetryRecords"
        ]
        Resource = "*"
      }
    ]
  })
}

resource "aws_iam_role_policy" "worker_task" {
  name = "${local.name}-worker"
  role = aws_iam_role.worker_task.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "sqs:ReceiveMessage",
          "sqs:DeleteMessage",
          "sqs:ChangeMessageVisibility",
          "sqs:GetQueueAttributes"
        ]
        Resource = [
          aws_sqs_queue.notifications.arn,
          aws_sqs_queue.notifications_dlq.arn
        ]
      },
      {
        Effect = "Allow"
        Action = [
          "xray:PutTraceSegments",
          "xray:PutTelemetryRecords"
        ]
        Resource = "*"
      }
    ]
  })
}

resource "aws_cloudwatch_event_bus" "academic_registration" {
  name = local.name
}

resource "aws_sqs_queue" "notifications_dlq" {
  name                      = "${local.name}-notifications-dlq"
  message_retention_seconds = 1209600
}

resource "aws_sqs_queue" "notifications" {
  name                       = "${local.name}-notifications"
  visibility_timeout_seconds = 30
  message_retention_seconds  = 345600
  receive_wait_time_seconds  = 20

  redrive_policy = jsonencode({
    deadLetterTargetArn = aws_sqs_queue.notifications_dlq.arn
    maxReceiveCount     = 3
  })
}

resource "aws_cloudwatch_event_rule" "student_notifications" {
  name           = "${local.name}-student-notifications"
  event_bus_name = aws_cloudwatch_event_bus.academic_registration.name

  event_pattern = jsonencode({
    source        = ["academic-registration.api"]
    "detail-type" = ["student.registered", "student.enrollment.changed"]
  })
}

data "aws_iam_policy_document" "notifications_queue" {
  statement {
    sid     = "AllowEventBridgeSendMessage"
    effect  = "Allow"
    actions = ["sqs:SendMessage"]

    principals {
      type        = "Service"
      identifiers = ["events.amazonaws.com"]
    }

    resources = [aws_sqs_queue.notifications.arn]

    condition {
      test     = "ArnEquals"
      variable = "aws:SourceArn"
      values   = [aws_cloudwatch_event_rule.student_notifications.arn]
    }
  }
}

resource "aws_sqs_queue_policy" "notifications" {
  queue_url = aws_sqs_queue.notifications.id
  policy    = data.aws_iam_policy_document.notifications_queue.json
}

resource "aws_cloudwatch_event_target" "student_notifications" {
  event_bus_name = aws_cloudwatch_event_bus.academic_registration.name
  rule           = aws_cloudwatch_event_rule.student_notifications.name
  target_id      = "notifications-sqs"
  arn            = aws_sqs_queue.notifications.arn
}

resource "aws_s3_bucket" "frontend" {
  bucket        = "${local.name}-frontend-${random_id.suffix.hex}"
  force_destroy = true
}

resource "aws_s3_bucket_public_access_block" "frontend" {
  bucket                  = aws_s3_bucket.frontend.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_server_side_encryption_configuration" "frontend" {
  bucket = aws_s3_bucket.frontend.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_cloudfront_origin_access_control" "frontend" {
  name                              = "${local.name}-frontend"
  description                       = "OAC for ${local.name} frontend bucket"
  origin_access_control_origin_type = "s3"
  signing_behavior                  = "always"
  signing_protocol                  = "sigv4"
}

resource "aws_wafv2_ip_set" "allowed_ipv4" {
  provider = aws.us_east_1
  count    = var.enable_waf && var.enable_waf_ip_allowlist ? 1 : 0

  name               = "${local.name}-allowed-ipv4"
  description        = "IPv4 CIDRs allowed to access ${local.name} through CloudFront"
  scope              = "CLOUDFRONT"
  ip_address_version = "IPV4"
  addresses          = var.waf_allowed_ipv4_cidrs
}

resource "aws_wafv2_web_acl" "frontend" {
  provider = aws.us_east_1
  count    = var.enable_waf ? 1 : 0

  name  = "${local.name}-cloudfront"
  scope = "CLOUDFRONT"

  default_action {
    allow {}
  }

  dynamic "rule" {
    for_each = var.enable_waf_ip_allowlist ? [1] : []

    content {
      name     = "BlockRequestsNotFromAllowedIps"
      priority = 0

      action {
        block {}
      }

      statement {
        not_statement {
          statement {
            ip_set_reference_statement {
              arn = aws_wafv2_ip_set.allowed_ipv4[0].arn
            }
          }
        }
      }

      visibility_config {
        cloudwatch_metrics_enabled = true
        metric_name                = "${local.name}-ip-allowlist"
        sampled_requests_enabled   = true
      }
    }
  }

  rule {
    name     = "AWSManagedRulesCommonRuleSet"
    priority = 1

    override_action {
      none {}
    }

    statement {
      managed_rule_group_statement {
        name        = "AWSManagedRulesCommonRuleSet"
        vendor_name = "AWS"
      }
    }

    visibility_config {
      cloudwatch_metrics_enabled = true
      metric_name                = "${local.name}-common-rules"
      sampled_requests_enabled   = true
    }
  }

  visibility_config {
    cloudwatch_metrics_enabled = true
    metric_name                = "${local.name}-waf"
    sampled_requests_enabled   = true
  }

  lifecycle {
    precondition {
      condition     = !var.enable_waf_ip_allowlist || length(var.waf_allowed_ipv4_cidrs) > 0
      error_message = "waf_allowed_ipv4_cidrs must include at least one CIDR when enable_waf_ip_allowlist is true."
    }
  }

  depends_on = [aws_wafv2_ip_set.allowed_ipv4]
}

resource "aws_cloudfront_distribution" "frontend" {
  enabled             = true
  is_ipv6_enabled     = true
  default_root_object = "index.html"
  web_acl_id          = var.enable_waf && var.attach_waf_to_cloudfront ? aws_wafv2_web_acl.frontend[0].arn : null

  origin {
    origin_id                = "frontend-s3"
    domain_name              = aws_s3_bucket.frontend.bucket_regional_domain_name
    origin_access_control_id = aws_cloudfront_origin_access_control.frontend.id
  }

  origin {
    origin_id   = "gateway-alb"
    domain_name = aws_lb.app.dns_name

    custom_origin_config {
      http_port              = 80
      https_port             = 443
      origin_protocol_policy = "http-only"
      origin_ssl_protocols   = ["TLSv1.2"]
    }
  }

  default_cache_behavior {
    target_origin_id       = "frontend-s3"
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD", "OPTIONS"]
    cached_methods         = ["GET", "HEAD"]
    compress               = true

    forwarded_values {
      query_string = false

      cookies {
        forward = "none"
      }
    }
  }

  ordered_cache_behavior {
    path_pattern             = "/api/*"
    target_origin_id         = "gateway-alb"
    viewer_protocol_policy   = "redirect-to-https"
    allowed_methods          = ["DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT"]
    cached_methods           = ["GET", "HEAD"]
    cache_policy_id          = data.aws_cloudfront_cache_policy.caching_disabled.id
    origin_request_policy_id = data.aws_cloudfront_origin_request_policy.all_viewer_except_host_header.id
    compress                 = true
  }

  custom_error_response {
    error_code         = 403
    response_code      = 200
    response_page_path = "/index.html"
  }

  custom_error_response {
    error_code         = 404
    response_code      = 200
    response_page_path = "/index.html"
  }

  restrictions {
    geo_restriction {
      restriction_type = "none"
    }
  }

  viewer_certificate {
    cloudfront_default_certificate = true
  }

  depends_on = [aws_wafv2_web_acl.frontend]
}

data "aws_iam_policy_document" "frontend_bucket" {
  statement {
    sid     = "AllowCloudFrontRead"
    actions = ["s3:GetObject"]

    resources = [
      "${aws_s3_bucket.frontend.arn}/*"
    ]

    principals {
      type        = "Service"
      identifiers = ["cloudfront.amazonaws.com"]
    }

    condition {
      test     = "StringEquals"
      variable = "AWS:SourceArn"
      values   = [aws_cloudfront_distribution.frontend.arn]
    }
  }
}

resource "aws_s3_bucket_policy" "frontend" {
  bucket = aws_s3_bucket.frontend.id
  policy = data.aws_iam_policy_document.frontend_bucket.json
}

locals {
  api_container = {
    name      = "api"
    image     = "${aws_ecr_repository.api.repository_url}:${var.image_tag}"
    essential = true
    portMappings = [
      {
        containerPort = 5081
        hostPort      = 5081
        protocol      = "tcp"
      }
    ]
    environment = [
      { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
      { name = "ASPNETCORE_URLS", value = "http://+:5081" },
      { name = "Configuracion__Conexion", value = local.app_db_provider },
      { name = "EventBridge__EventBusName", value = aws_cloudwatch_event_bus.academic_registration.name },
      { name = "EventBridge__Source", value = "academic-registration.api" },
      { name = "EventBridge__Region", value = var.aws_region },
      { name = "EventBridge__ServiceUrl", value = "" },
      { name = "Outbox__BatchSize", value = "20" },
      { name = "Outbox__PollingIntervalSeconds", value = "5" },
      { name = "Outbox__MaxRetries", value = "0" },
      { name = "Database__ApplyMigrationsOnStartup", value = "true" },
      { name = "Cors__AllowedOrigins__0", value = "https://${aws_cloudfront_distribution.frontend.domain_name}" },
      { name = "Tracing__XRay__Enabled", value = tostring(var.enable_xray) },
      { name = "AWS_XRAY_DAEMON_ADDRESS", value = "127.0.0.1:2000" }
    ]
    secrets = [
      {
        name      = local.app_db_secret_name
        valueFrom = aws_secretsmanager_secret.db_connection_string.arn
      }
    ]
    logConfiguration = {
      logDriver = "awslogs"
      options = {
        awslogs-group         = aws_cloudwatch_log_group.api.name
        awslogs-region        = var.aws_region
        awslogs-stream-prefix = "api"
      }
    }
  }

  gateway_container = {
    name      = "gateway"
    image     = "${aws_ecr_repository.gateway.repository_url}:${var.image_tag}"
    essential = true
    portMappings = [
      {
        containerPort = 5080
        hostPort      = 5080
        protocol      = "tcp"
      }
    ]
    environment = [
      { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
      { name = "ASPNETCORE_URLS", value = "http://+:5080" },
      { name = "Cors__AllowedOrigins__0", value = "https://${aws_cloudfront_distribution.frontend.domain_name}" },
      { name = "ReverseProxy__Clusters__academic-registration-api__Destinations__api__Address", value = "http://api.${aws_service_discovery_private_dns_namespace.this.name}:5081/" },
      { name = "Tracing__XRay__Enabled", value = tostring(var.enable_xray) },
      { name = "AWS_XRAY_DAEMON_ADDRESS", value = "127.0.0.1:2000" }
    ]
    logConfiguration = {
      logDriver = "awslogs"
      options = {
        awslogs-group         = aws_cloudwatch_log_group.gateway.name
        awslogs-region        = var.aws_region
        awslogs-stream-prefix = "gateway"
      }
    }
  }

  worker_container = {
    name      = "notifications-worker"
    image     = "${aws_ecr_repository.notifications.repository_url}:${var.image_tag}"
    essential = true
    environment = [
      { name = "DOTNET_ENVIRONMENT", value = "Production" },
      { name = "Sqs__QueueUrl", value = aws_sqs_queue.notifications.url },
      { name = "Sqs__Region", value = var.aws_region },
      { name = "Sqs__ServiceUrl", value = "" },
      { name = "Sqs__MaxNumberOfMessages", value = "10" },
      { name = "Sqs__WaitTimeSeconds", value = "20" },
      { name = "Sqs__VisibilityTimeoutSeconds", value = "30" },
      { name = "Sqs__EmptyQueueDelaySeconds", value = "2" },
      { name = "Email__Enabled", value = tostring(var.email_enabled) },
      { name = "Email__From", value = var.smtp_from },
      { name = "Email__FromName", value = var.smtp_from_name },
      { name = "Email__Host", value = var.smtp_host },
      { name = "Email__Port", value = tostring(var.smtp_port) },
      { name = "Email__UserName", value = var.smtp_username },
      { name = "Email__EnableSsl", value = tostring(var.smtp_enable_ssl) },
      { name = "Email__RequireAuthentication", value = tostring(var.smtp_require_authentication) },
      { name = "Tracing__XRay__Enabled", value = tostring(var.enable_xray) },
      { name = "AWS_XRAY_DAEMON_ADDRESS", value = "127.0.0.1:2000" }
    ]
    secrets = [
      {
        name      = "Email__Password"
        valueFrom = aws_secretsmanager_secret.smtp_password.arn
      }
    ]
    logConfiguration = {
      logDriver = "awslogs"
      options = {
        awslogs-group         = aws_cloudwatch_log_group.worker.name
        awslogs-region        = var.aws_region
        awslogs-stream-prefix = "worker"
      }
    }
  }

  xray_container = {
    name      = "xray-daemon"
    image     = "amazon/aws-xray-daemon:latest"
    essential = false
    portMappings = [
      {
        containerPort = 2000
        hostPort      = 2000
        protocol      = "udp"
      }
    ]
    logConfiguration = {
      logDriver = "awslogs"
      options = {
        awslogs-group         = aws_cloudwatch_log_group.xray.name
        awslogs-region        = var.aws_region
        awslogs-stream-prefix = "xray"
      }
    }
  }
}

resource "aws_ecs_task_definition" "api" {
  family                   = "${local.name}-api"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.container_cpu
  memory                   = var.container_memory
  execution_role_arn       = aws_iam_role.execution.arn
  task_role_arn            = aws_iam_role.api_task.arn

  container_definitions = jsonencode(
    concat(
      [local.api_container],
      var.enable_xray ? [local.xray_container] : []
    )
  )
}

resource "aws_ecs_task_definition" "gateway" {
  family                   = "${local.name}-gateway"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.container_cpu
  memory                   = var.container_memory
  execution_role_arn       = aws_iam_role.execution.arn
  task_role_arn            = aws_iam_role.gateway_task.arn

  container_definitions = jsonencode(
    concat(
      [local.gateway_container],
      var.enable_xray ? [local.xray_container] : []
    )
  )
}

resource "aws_ecs_task_definition" "worker" {
  family                   = "${local.name}-notifications-worker"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.container_cpu
  memory                   = var.container_memory
  execution_role_arn       = aws_iam_role.execution.arn
  task_role_arn            = aws_iam_role.worker_task.arn

  container_definitions = jsonencode(
    concat(
      [local.worker_container],
      var.enable_xray ? [local.xray_container] : []
    )
  )
}

resource "aws_ecs_service" "api" {
  name            = "api"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.api.arn
  desired_count   = var.api_desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = aws_subnet.public[*].id
    security_groups  = [aws_security_group.ecs.id]
    assign_public_ip = true
  }

  service_registries {
    registry_arn = aws_service_discovery_service.api.arn
  }

  depends_on = [
    aws_iam_role_policy_attachment.execution,
    aws_iam_role_policy.execution_secrets
  ]
}

resource "aws_ecs_service" "gateway" {
  name            = "gateway"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.gateway.arn
  desired_count   = var.gateway_desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = aws_subnet.public[*].id
    security_groups  = [aws_security_group.ecs.id]
    assign_public_ip = true
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.gateway.arn
    container_name   = "gateway"
    container_port   = 5080
  }

  depends_on = [
    aws_lb_listener.http,
    aws_iam_role_policy_attachment.execution,
    aws_iam_role_policy.execution_secrets
  ]
}

resource "aws_ecs_service" "worker" {
  name            = "notifications-worker"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.worker.arn
  desired_count   = var.worker_desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = aws_subnet.public[*].id
    security_groups  = [aws_security_group.ecs.id]
    assign_public_ip = true
  }

  depends_on = [
    aws_iam_role_policy_attachment.execution,
    aws_iam_role_policy.execution_secrets
  ]
}

resource "aws_cloudwatch_metric_alarm" "queue_age" {
  alarm_name          = "${local.name}-notifications-queue-age"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 2
  metric_name         = "ApproximateAgeOfOldestMessage"
  namespace           = "AWS/SQS"
  period              = 60
  statistic           = "Maximum"
  threshold           = 300
  alarm_description   = "Notification queue backlog is older than 5 minutes."

  dimensions = {
    QueueName = aws_sqs_queue.notifications.name
  }
}

resource "aws_cloudwatch_metric_alarm" "alb_5xx" {
  alarm_name          = "${local.name}-alb-5xx"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 2
  metric_name         = "HTTPCode_Target_5XX_Count"
  namespace           = "AWS/ApplicationELB"
  period              = 60
  statistic           = "Sum"
  threshold           = 5
  alarm_description   = "ALB target group returned repeated 5xx responses."

  dimensions = {
    LoadBalancer = aws_lb.app.arn_suffix
  }
}

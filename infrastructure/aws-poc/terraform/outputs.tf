output "frontend_url" {
  description = "CloudFront URL for the Angular frontend."
  value       = "https://${aws_cloudfront_distribution.frontend.domain_name}"
}

output "api_base_url" {
  description = "API base URL routed through CloudFront and the gateway."
  value       = "https://${aws_cloudfront_distribution.frontend.domain_name}/api"
}

output "alb_url" {
  description = "Direct ALB URL for troubleshooting."
  value       = "http://${aws_lb.app.dns_name}"
}

output "api_ecr_repository_url" {
  value = aws_ecr_repository.api.repository_url
}

output "gateway_ecr_repository_url" {
  value = aws_ecr_repository.gateway.repository_url
}

output "notifications_ecr_repository_url" {
  value = aws_ecr_repository.notifications.repository_url
}

output "frontend_bucket" {
  value = aws_s3_bucket.frontend.bucket
}

output "cloudfront_distribution_id" {
  value = aws_cloudfront_distribution.frontend.id
}

output "waf_web_acl_arn" {
  description = "CloudFront WAF Web ACL ARN when WAF is enabled."
  value       = var.enable_waf ? aws_wafv2_web_acl.frontend[0].arn : null
}

output "waf_allowed_ipv4_ip_set_arn" {
  description = "WAF IPv4 allowlist IP set ARN when IP allowlisting is enabled."
  value       = var.enable_waf && var.enable_waf_ip_allowlist ? aws_wafv2_ip_set.allowed_ipv4[0].arn : null
}

output "ecs_cluster_name" {
  value = aws_ecs_cluster.this.name
}

output "event_bus_name" {
  value = aws_cloudwatch_event_bus.academic_registration.name
}

output "notifications_queue_url" {
  value = aws_sqs_queue.notifications.url
}

output "notifications_dlq_url" {
  value = aws_sqs_queue.notifications_dlq.url
}

output "rds_endpoint" {
  value = aws_db_instance.database.endpoint
}

output "db_connection_string_secret_name" {
  value = aws_secretsmanager_secret.db_connection_string.name
}

output "smtp_password_secret_name" {
  value = aws_secretsmanager_secret.smtp_password.name
}

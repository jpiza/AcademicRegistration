variable "project_name" {
  description = "Short name used to prefix AWS resources."
  type        = string
  default     = "academic-registration"
}

variable "environment" {
  description = "Environment name for this PoC."
  type        = string
  default     = "poc"
}

variable "aws_region" {
  description = "AWS region for regional resources."
  type        = string
  default     = "us-east-1"
}

variable "access_key" {
  description = "access_key"
  type        = string
  sensitive   = true
}

variable "secret_key" {
  description = "secret_key"
  type        = string
  sensitive   = true
}

variable "vpc_cidr" {
  description = "CIDR block for the PoC VPC."
  type        = string
  default     = "10.42.0.0/16"
}

variable "az_count" {
  description = "Number of availability zones to use."
  type        = number
  default     = 2
}

variable "image_tag" {
  description = "Container image tag deployed by ECS services."
  type        = string
  default     = "latest"
}

variable "api_desired_count" {
  description = "Desired number of API tasks."
  type        = number
  default     = 1
}

variable "gateway_desired_count" {
  description = "Desired number of gateway tasks."
  type        = number
  default     = 1
}

variable "worker_desired_count" {
  description = "Desired number of notification worker tasks."
  type        = number
  default     = 1
}

variable "container_cpu" {
  description = "Fargate CPU units per task."
  type        = number
  default     = 512
}

variable "container_memory" {
  description = "Fargate memory in MiB per task."
  type        = number
  default     = 1024
}

variable "db_name" {
  description = "Application database name."
  type        = string
  default     = "AcademicRegistrationDb"
}

variable "db_username" {
  description = "RDS master username."
  type        = string
  default     = "academicadmin"
}

variable "db_engine" {
  description = "RDS engine for the PoC. Use mysql for free-plan-friendly accounts, or sqlserver if your AWS account allows it."
  type        = string
  default     = "mysql"

  validation {
    condition     = contains(["mysql", "sqlserver"], lower(var.db_engine))
    error_message = "db_engine must be mysql or sqlserver."
  }
}

variable "db_instance_class" {
  description = "RDS instance class. Leave empty to use a free-plan-friendly default per engine."
  type        = string
  default     = ""
}

variable "db_allocated_storage" {
  description = "RDS storage in GiB."
  type        = number
  default     = 20
}

variable "db_storage_type" {
  description = "RDS storage type."
  type        = string
  default     = "gp2"
}

variable "db_deletion_protection" {
  description = "Enable RDS deletion protection."
  type        = bool
  default     = false
}

variable "skip_final_snapshot" {
  description = "Skip final DB snapshot when destroying the PoC."
  type        = bool
  default     = true
}

variable "email_enabled" {
  description = "Enable real SMTP delivery in the notification worker."
  type        = bool
  default     = false
}

variable "smtp_from" {
  description = "SMTP sender email."
  type        = string
  default     = "no-reply@academic-registration.local"
}

variable "smtp_from_name" {
  description = "SMTP sender display name."
  type        = string
  default     = "Academic Registration"
}

variable "smtp_host" {
  description = "SMTP host."
  type        = string
  default     = ""
}

variable "smtp_port" {
  description = "SMTP port."
  type        = number
  default     = 587
}

variable "smtp_username" {
  description = "SMTP username."
  type        = string
  default     = ""
}

variable "smtp_password" {
  description = "SMTP password or app password stored in Secrets Manager."
  type        = string
  sensitive   = true
  default     = "replace-me"
}

variable "smtp_enable_ssl" {
  description = "Enable SSL/TLS for SMTP."
  type        = bool
  default     = true
}

variable "smtp_require_authentication" {
  description = "Require SMTP authentication."
  type        = bool
  default     = true
}

variable "enable_waf" {
  description = "Create and attach a basic AWS managed WAF ACL to CloudFront."
  type        = bool
  default     = false
}

variable "attach_waf_to_cloudfront" {
  description = "Associate the WAF Web ACL to CloudFront. Set false first when detaching WAF before deleting it."
  type        = bool
  default     = true
}

variable "enable_waf_ip_allowlist" {
  description = "When true, WAF blocks every CloudFront request whose source IP is not in waf_allowed_ipv4_cidrs."
  type        = bool
  default     = false
}

variable "waf_allowed_ipv4_cidrs" {
  description = "IPv4 CIDR blocks allowed by the CloudFront WAF allowlist. Use /32 for individual public IPs."
  type        = list(string)
  default     = []

  validation {
    condition     = alltrue([for cidr in var.waf_allowed_ipv4_cidrs : can(cidrnetmask(cidr))])
    error_message = "waf_allowed_ipv4_cidrs must contain valid IPv4 CIDR blocks, for example 203.0.113.10/32."
  }
}

variable "restrict_alb_to_cloudfront" {
  description = "When true, the public ALB security group only accepts HTTP traffic from the AWS-managed CloudFront origin-facing prefix list."
  type        = bool
  default     = false
}

variable "enable_xray" {
  description = "Enable X-Ray middleware and daemon sidecar in ECS tasks."
  type        = bool
  default     = true
}

variable "log_retention_days" {
  description = "CloudWatch log retention."
  type        = number
  default     = 14
}

variable "force_delete_ecr" {
  description = "Allow Terraform destroy to delete ECR repositories with images."
  type        = bool
  default     = true
}

variable "tags" {
  description = "Additional tags applied to all resources."
  type        = map(string)
  default     = {}
}
